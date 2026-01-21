using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

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

            if (_http.BaseAddress == null)
                throw new InvalidOperationException("HttpClient.BaseAddress deve ser configurado para o serviço TPL.");
        }

        public async Task<(TplOrderInfo? info, List<TplShippingEvent>? shippingevents, int code, string? message)> ObterDadosBrutosAsync(
            string orderNumber,
            int? orderId,
            CancellationToken ct = default)
        {
            try
            {
                //BUSCA O TOKEN
                var auth = await EnsureAuthAsync(ct);

                // 1) Tenta por order.number (cd_rastreio)
                var resNumber = await PostOrderDetailAsync(auth, number: orderNumber, id: null, ct);
                if (resNumber.success && resNumber.response?.order is not null)
                {
                    var order = resNumber.response.order;
                    return (order.info, order.shippingevents, order.code ?? 200, order.message);
                }

                var code = resNumber.code ?? 502;
                throw new HttpRequestException(
                    $"TPL orderdetail falhou (code {code}).",
                    null,
                    MapToHttpStatus(code));
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // Timeout do HttpClient
                throw new HttpRequestException("TPL timeout", ex, HttpStatusCode.RequestTimeout);
            }
            catch (OperationCanceledException)
            {
                // Cancelamento solicitado pelo chamador
                throw;
            }
        }

        private async Task<(bool success, int? code, TplOrderDetailResponse? response)>
            PostOrderDetailAsync(string auth, string? number, string? id, CancellationToken ct)
        {

            var bodyObj = new { auth, order = new { number = number } };
            var bodyJson = JsonSerializer.Serialize(bodyObj);

            var _tplBaseUrl = "https://oms.tpl.com.br/api";
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_tplBaseUrl}/get/orderdetail")
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
            };


            var resp = await _http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();


            if (!resp.IsSuccessStatusCode)
                return (false, (int)resp.StatusCode, null);

            if (string.IsNullOrWhiteSpace(text))
                return (false, 502, null);

            TplOrderDetailResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize<TplOrderDetailResponse>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return (false, 502, null);
            }

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
            var bodyJson = JsonSerializer.Serialize(body);

            //var req = new HttpRequestMessage(HttpMethod.Post, "/get/auth")
            //{
            //    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
            //};

            var _tplBaseUrl = "https://oms.tpl.com.br/api";
            var req = new HttpRequestMessage(HttpMethod.Post, $"{_tplBaseUrl}/get/auth")
            {
                Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
            };

            using var http = new HttpClient();
            var resp = await http.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"TPL AUTH {(int)resp.StatusCode}: {text}", null, resp.StatusCode);

            if (string.IsNullOrWhiteSpace(text))
                throw new HttpRequestException("Resposta AUTH vazia", null, HttpStatusCode.BadGateway);

            TplAuthResponse? authDoc;
            try
            {
                authDoc = JsonSerializer.Deserialize<TplAuthResponse>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException ex)
            {
                throw new HttpRequestException($"TPL retornou JSON inválido: {text}", ex, HttpStatusCode.BadGateway);
            }

            if (authDoc is null)
                throw new HttpRequestException($"Payload AUTH inválido: {text}", null, HttpStatusCode.BadGateway);

            if (string.IsNullOrWhiteSpace(authDoc.token))
                throw new HttpRequestException($"Token não retornado pela TPL. Payload: {text}", null, HttpStatusCode.BadGateway);

            _cachedAuth = authDoc.token;
            _authExpiresUtc = DateTime.UtcNow.AddMinutes(59);
            return _cachedAuth!;
        }
        private static async Task<string> ReadStartOfStream(Stream stream, CancellationToken ct)
        {
            try
            {
                stream.Position = 0;
                var buffer = new byte[200];
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                return Encoding.UTF8.GetString(buffer, 0, bytesRead);
            }
            catch
            {
                return "[não foi possível ler o stream]";
            }
        }

        private static HttpStatusCode MapToHttpStatus(int? tplCode) =>
            tplCode switch
            {
                404 => HttpStatusCode.NotFound,
                500 => HttpStatusCode.Unauthorized,
                400 => HttpStatusCode.BadRequest,
                402 => HttpStatusCode.BadRequest,
                _ => HttpStatusCode.BadGateway
            };

        private class TplOrderRequest
        {
            public string? number { get; set; }
            public string? id { get; set; }
        }
    }

    // ===== Payloads TPL (conforme doc) =====
    public class TplAuthResponse
    {
        public string? token { get; set; }
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
        public string? final{ get; set; }
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