using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tracking.Application
{
    public class TplService : ITplService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _cfg;

        // Cache simples do auth por ~1 hora (conforme doc TPL)
        private static string? _cachedAuth;
        private static DateTime _authExpiresUtc;

        public TplService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _cfg = cfg;
        }

        public async Task<TplRastreioResult> ObterDetalhePedidoAsync(
            string orderNumber,
            int? orderId,
            CancellationToken ct = default)
        {
            var auth = await EnsureAuthAsync(ct);

            // 1) Tenta por order.number (cd_rastreio)
            var resNumber = await PostOrderDetailAsync(auth, number: orderNumber, id: null, ct);
            if (resNumber.success)
                return MapToResult(orderNumber, resNumber.response!);

            // 2) Fallback por order.id (id_resgate), se fornecido
            if (orderId.HasValue)
            {
                var resId = await PostOrderDetailAsync(auth, number: null, id: orderId.Value.ToString(), ct);
                if (resId.success)
                    return MapToResult(orderNumber, resId.response!);
            }

            var code = resNumber.code ?? 502;
            throw new HttpRequestException(
                $"TPL orderdetail falhou (code {code}).",
                null,
                MapToHttpStatus(code));
        }

        public async Task<(TplOrderInfo? info, List<TplShippingEvent>? shippingevents, int code, string? message)> ObterDadosBrutosAsync(
    string orderNumber,
    int? orderId,
    CancellationToken ct = default)
{
    var auth = await EnsureAuthAsync(ct);

    // 1) Tenta por order.number (cd_rastreio)
    var resNumber = await PostOrderDetailAsync(auth, number: orderNumber, id: null, ct);
    if (resNumber.success && resNumber.response?.order is not null)
    {
        var order = resNumber.response.order;
        return (order.info, order.shippingevents, order.code ?? 200, order.message);
    }

    // 2) Fallback por order.id (id_resgate), se fornecido
    if (orderId.HasValue)
    {
        var resId = await PostOrderDetailAsync(auth, number: null, id: orderId.Value.ToString(), ct);
        if (resId.success && resId.response?.order is not null)
        {
            var order = resId.response.order;
            return (order.info, order.shippingevents, order.code ?? 200, order.message);
        }
    }

    var code = resNumber.code ?? 502;
    throw new HttpRequestException(
        $"TPL orderdetail falhou (code {code}).",
        null,
        MapToHttpStatus(code));
}

        private async Task<(bool success, int? code, TplOrderDetailResponse? response)>
            PostOrderDetailAsync(string auth, string? number, string? id, CancellationToken ct)
        {


            var orderReq = new TplOrderRequest { number = number, id = id };

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // IGNORA valores nulos no JSON
            };

            var requestBodyJson = JsonSerializer.Serialize(
                new { auth, order = orderReq },
                serializerOptions);

            var req = new HttpRequestMessage(HttpMethod.Post, "/get/orderdetail")
            {
                Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json")
            };


            var resp = await _http.SendAsync(req, ct);
            var bodyText = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                return (false, (int)resp.StatusCode, null);

            var payload = JsonSerializer.Deserialize<TplOrderDetailResponse>(
                bodyText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload is null) return (false, 502, null);
            if (payload.code != 200 || payload.order is null) return (false, payload.code, payload);

            return (true, 200, payload);
        }

        private async Task<string> EnsureAuthAsync(CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            if (_cachedAuth is not null && now < _authExpiresUtc) return _cachedAuth;

            var apiKey = _cfg["Tpl:ApiKey"] ?? throw new InvalidOperationException("Tpl:ApiKey não configurado.");
            var token = _cfg["Tpl:Token"] ?? throw new InvalidOperationException("Tpl:Token não configurado.");
            var email = _cfg["Tpl:Email"] ?? throw new InvalidOperationException("Tpl:Email não configurado.");

            var body = new { apikey = apiKey, token, email };
            var req = new HttpRequestMessage(HttpMethod.Post, "/get/auth")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };

            var resp = await _http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"TPL AUTH {(int)resp.StatusCode}: {text}", null, resp.StatusCode);

            var authResp = JsonSerializer.Deserialize<TplAuthResponse>(
                text,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Resposta AUTH inválida.");

            if (string.IsNullOrWhiteSpace(authResp.token))
                throw new HttpRequestException($"TPL AUTH code: {authResp.code}", null, MapToHttpStatus(authResp.code));

            _cachedAuth = authResp.token;
            _authExpiresUtc = DateTime.UtcNow.AddMinutes(59);
            return _cachedAuth!;
        }

        private static HttpStatusCode MapToHttpStatus(int? tplCode) =>
            tplCode switch
            {
                404 => HttpStatusCode.NotFound,
                500 => HttpStatusCode.Unauthorized, // auth inválido
                400 => HttpStatusCode.BadRequest,   // já há um auth em uso (<1h)
                402 => HttpStatusCode.BadRequest,   // dados inválidos
                _ => HttpStatusCode.BadGateway
            };

        private static DateTime? ParseDate(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (DateTime.TryParse(s, out var dt)) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return null;
        }

        private static string MapearTituloPorCodigo(int? internalCode, string? microCode, string? info) =>
            internalCode switch
            {
                90 => "Pedido entregue",
                75 => "Saiu para entrega",
                70 => "Em trânsito",
                60 => "Coletado pela transportadora",
                50 => "Despachado",
                100 => "Falha na entrega",
                1020 => "Destinatário ausente",
                1040 => "Aguardando retirada",
                1100 => "Objeto não procurado",
                400 => "Objeto extraviado",
                411 => "Roubo de carga",
                _ => info ?? (string.IsNullOrWhiteSpace(microCode) ? "Atualização" : microCode)
            };

        private static TplRastreioResult MapToResult(string orderNumber, TplOrderDetailResponse payload)
        {
            var eventos = new List<TplEvento>();
            if (payload.order?.shippingevents is not null)
            {
                foreach (var e in payload.order.shippingevents)
                {
                    eventos.Add(new TplEvento
                    {
                        Id = e.code,
                        Data = ParseDate(e.date) ?? DateTime.UtcNow,
                        CodigoStatus = e.internalCode?.ToString() ?? e.code,
                        Titulo = MapearTituloPorCodigo(e.internalCode, e.code, e.info),
                        Descricao = e.info
                    });
                }
            }

            eventos = eventos.OrderByDescending(x => x.Data).ToList();

            return new TplRastreioResult
            {
                CodigoRastreio = orderNumber,
                Eventos = eventos,
                StatusMacro = payload.order?.code,
                MensagemMacro = payload.order?.message,
                TransportadoraApelido = payload.order?.shippment?.nick,
                TransportadoraTracker = payload.order?.shippment?.tracker,
                TransportadoraUrl = payload.order?.shippment?.url,
                TrackerUrl = payload.order?.shippment?.trackerUrl
            };
        }

        private class TplOrderRequest
        {
            public string? number { get; set; }  // cd_rastreio (order.number)
            public string? id { get; set; }      // id_resgate   (order.id)
        }
    }

    // ===== Payloads TPL (conforme doc) =====
    public class TplAuthResponse
    {
        public string? token { get; set; }  // ✅ Usar "token" em vez de "auth"
        public int? id { get; set; }
        public int? code { get; set; }
    }

    public class TplOrderDetailResponse
    {
        public int code { get; set; }
        public string? message { get; set; }
        public TplOrderDetailOrder? order { get; set; }
    }

    public class TplOrderDetailOrder
    {
        public int? code { get; set; }
        public string? message { get; set; }
        public TplOrderInfo? info { get; set; }
        public TplShipmentInfo? shippment { get; set; }
        public List<TplShippingEvent>? shippingevents { get; set; }
        public List<TplSummaryByVolume>? summarybyvolume { get; set; }
        public TplInternalEvents? internalevents { get; set; }
    }

    public class TplOrderInfo
    {
        public string? id { get; set; }
        public string? number { get; set; }
        public string? date { get; set; }
        public string? prediction { get; set; }
        public string? iderp { get; set; }
        public string? note { get; set; }
    }

    public class TplShipmentInfo
    {
        public string? nick { get; set; }
        public string? method { get; set; }
        public string? vol { get; set; }
        public string? tracker { get; set; }
        public string? trackerUrl { get; set; }
        public string? url { get; set; }
    }

    public class TplShippingEvent
    {
        public int? internalCode { get; set; }
        public string? code { get; set; }
        public string? info { get; set; }
        public string? complement { get; set; }
        public string? date { get; set; }
        public string? final { get; set; }
        public string? volume { get; set; }
    }

    public class TplSummaryByVolume
    {
        public string? tracking { get; set; }
        public int? vol { get; set; }
        public string? created { get; set; }
        public string? deliveryforecast { get; set; }
        public string? outfordelivery { get; set; }
        public string? intransit { get; set; }
        public string? delivered { get; set; }
        public string? deliveryfailure { get; set; }
    }

    public class TplInternalEvents
    {
        public string? created { get; set; }
        public string? os { get; set; }
        public string? withoutBalance { get; set; }
        public string? invoice { get; set; }
        public string? startPicking { get; set; }
        public string? endPicking { get; set; }
        public string? startCheckout { get; set; }
        public string? endCheckout { get; set; }
        public string? dispatched { get; set; }
        public string? in_transit { get; set; }
        public string? out_for_delivery { get; set; }
        public string? delivered { get; set; }
        public string? fail { get; set; }
        public string? cancelled { get; set; }
        public string? volume { get; set; }
    }
}
