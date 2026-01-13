using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tracking.Application.Dto;
using Tracking.Application.Mappers;
using Tracking.Infrastructure.Repositories;

namespace Tracking.Application.Services
{
    public class ClienteService : IClienteService
    {
        private readonly IRastreioRepository _repo;
        private readonly ITplService _tpl;

        public ClienteService(IRastreioRepository repo, ITplService tpl)
        {
            _repo = repo;
            _tpl = tpl;
        }

        public async Task<RastreioResponse?> ConsultarAsync(string identificador, CancellationToken ct = default)
        {
            var data = await _repo.BuscarPorCpfOuEmailAsync(identificador, ct);
            if (data is null) return null;

            var resgate = data.Value.resgate;
            var rastreio = data.Value.rastreio;
            var numeroPedido = resgate.Lote ?? string.Empty;
            var idPresente = resgate.KitDescricao ?? string.Empty;
            var codigo = rastreio?.CodigoRastreio;
            var orderId = resgate.IdResgate;

            var baseResponse = new RastreioResponse(
                cliente: new { nome = resgate.Nome ?? "", email = resgate.Email ?? "", cpf = resgate.Cpf ?? "" },
                numeroPedido: numeroPedido,
                idPresente: idPresente,
                codigoRastreio: codigo,
                transportadora: null,
                eventos: new List<EventoDto>()
            );

            if (string.IsNullOrWhiteSpace(codigo))
                return baseResponse;

            var tpl = await _tpl.ObterDetalhePedidoAsync(codigo!, orderId, ct);

            var eventos = tpl.Eventos
                .OrderByDescending(e => e.Data)
                .Select(e => new EventoDto(
                    id: e.Id ?? Guid.NewGuid().ToString("N"),
                    date: e.Data,
                    name: e.Titulo ?? "Atualização",
                    description: e.Descricao ?? string.Empty
                ))
                .ToList();

            return baseResponse with
            {
                transportadora = new TransportadoraDto(
                    tpl.TransportadoraApelido ?? "TPL",
                    tpl.TransportadoraTracker ?? codigo!),
                eventos = eventos
            };
        }

        public async Task<RastreioTplResponse?> ConsultarTplAsync(string identificador, CancellationToken ct = default)
        {
            // 🎭 MOCK: CPFs especiais para testes no frontend
            var cpfLimpo = new string(identificador.Where(char.IsDigit).ToArray());

            // Mock 3: CPF não localizado (404) - NÃO MEXER
            if (cpfLimpo == "22676652801")
            {
                return new RastreioTplResponse(
                    code: 404,
                    message: "CPF ou e-mail não localizado.",
                    info: new OrderInfoDto(
                        id: string.Empty,
                        number: string.Empty,
                        date: string.Empty,
                        prediction: string.Empty,
                        iderp: null
                    ),
                    shippingevents: new List<ShippingEventDto>
                    {
                        new ShippingEventDto(
                            code: string.Empty,
                            dscode: string.Empty,
                            message: string.Empty,
                            detalhe: string.Empty,
                            complement: null,
                            dtshipping: string.Empty,
                            internalcode: null
                        )
                    }
                );
            }

            // Mock 2: Em preparação (cd_rastreio NULL)
            if (cpfLimpo == "12676652800")
            {
                var statusPrep = StatusTimelineMapper.ObterStatusPreparacao();

                return new RastreioTplResponse(
                    code: 200,
                    message: "OK",
                    info: new OrderInfoDto(
                        id: string.Empty,
                        number: string.Empty,
                        date: string.Empty,
                        prediction: string.Empty,
                        iderp: null
                    ),
                    shippingevents: new List<ShippingEventDto>
                    {
                        new ShippingEventDto(
                            code: statusPrep.CodTimeline,
                            dscode: statusPrep.StatusTimeline,
                            message: statusPrep.DsTimeline,
                            detalhe: string.Empty,
                            complement: null,
                            dtshipping: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                            internalcode: 5
                        )
                    }
                );
            }

            // Mock 1: Pedido completo com rastreio da TPL
            if (cpfLimpo == "32676652800")
            {
                return CarregarMockTplJson();
            }

            // 🔄 Fluxo normal (não é mock)
            var data = await _repo.BuscarPorCpfOuEmailAsync(identificador, ct);

            // CPF não encontrado (404) - NÃO MEXER
            if (data is null)
            {
                return new RastreioTplResponse(
                    code: 404,
                    message: "CPF ou e-mail não localizado.",
                    info: new OrderInfoDto(
                        id: string.Empty,
                        number: string.Empty,
                        date: string.Empty,
                        prediction: string.Empty,
                        iderp: null
                    ),
                    shippingevents: new List<ShippingEventDto>
                    {
                        new ShippingEventDto(
                            code: string.Empty,
                            dscode: string.Empty,
                            message: string.Empty,
                            detalhe: string.Empty,
                            complement: null,
                            dtshipping: string.Empty,
                            internalcode: null
                        )
                    }
                );
            }

            var resgate = data.Value.resgate;
            var rastreio = data.Value.rastreio;
            var codigo = rastreio?.CodigoRastreio;
            var orderId = resgate.IdResgate;

            // Cenário 2: cd_rastreio é NULL (Em preparação)
            if (string.IsNullOrWhiteSpace(codigo))
            {
                var statusPrep = StatusTimelineMapper.ObterStatusPreparacao();
                var dtRegistro = rastreio?.DtRegistro?.ToString("yyyy-MM-ddTHH:mm:ss")
                    ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");

                return new RastreioTplResponse(
                    code: 200,
                    message: "OK",
                    info: new OrderInfoDto(
                        id: string.Empty,
                        number: string.Empty,
                        date: string.Empty,
                        prediction: string.Empty,
                        iderp: null
                    ),
                    shippingevents: new List<ShippingEventDto>
                    {
                        new ShippingEventDto(
                            code: statusPrep.CodTimeline,
                            dscode: statusPrep.StatusTimeline,
                            message: statusPrep.DsTimeline,
                            detalhe: string.Empty,
                            complement: null,
                            dtshipping: dtRegistro,
                            internalcode: 5
                        )
                    }
                );
            }

            // Cenário 1: cd_rastreio existe - Aplicar DE/PARA + Filtrar não mapeados
            var (info, shippingevents, code, message) = await _tpl.ObterDadosBrutosAsync(codigo!, orderId, ct);

            var infoDto = info is not null
                ? new OrderInfoDto(info.id, info.number, info.date, info.prediction, info.iderp)
                : null;

            // ✅ Filtrar apenas eventos mapeados + Remover duplicados por internalCode
            var eventosDto = (shippingevents ?? new List<TplShippingEvent>())
                .Where(e => StatusTimelineMapper.EstaMapeado(e.internalCode)) // ✅ FILTRAR NÃO MAPEADOS
                .GroupBy(e => e.internalCode)
                .Select(g => g.First())
                .Select(e =>
                {
                    var mapped = StatusTimelineMapper.MapearPorInternalCode(e.internalCode)!; // ! porque já foi validado no Where

                    return new ShippingEventDto(
                        code: mapped.CodTimeline,
                        dscode: mapped.StatusTimeline,
                        message: mapped.DsTimeline,
                        detalhe: e.info,
                        complement: e.complement,
                        dtshipping: e.date,
                        internalcode: e.internalCode
                    );
                })
                .ToList();

            return new RastreioTplResponse(code, message, infoDto, eventosDto);
        }

        private RastreioTplResponse CarregarMockTplJson()
        {
            try
            {
                var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Services", "JSON_TPL.json");

                if (!File.Exists(jsonPath))
                {
                    jsonPath = @"D:\2LOG\Projetos\TrackingBackend\Tracking.Application\Services\JSON_TPL.json";
                }

                if (!File.Exists(jsonPath))
                {
                    throw new FileNotFoundException($"Arquivo JSON mock não encontrado: {jsonPath}");
                }

                var jsonContent = File.ReadAllText(jsonPath);
                var mockData = JsonSerializer.Deserialize<MockTplResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (mockData?.order == null)
                {
                    throw new InvalidOperationException("JSON mock inválido");
                }

                var order = mockData.order;
                var infoDto = order.info != null
                    ? new OrderInfoDto(
                        order.info.id,
                        order.info.number,
                        order.info.date,
                        order.info.prediction,
                        order.info.iderp)
                    : null;

                // ✅ Filtrar apenas eventos mapeados + Remover duplicados por internalCode
                var eventosDto = (order.shippingevents ?? Array.Empty<MockShippingEvent>())
                    .Where(e => StatusTimelineMapper.EstaMapeado(e.internalCode)) // ✅ FILTRAR NÃO MAPEADOS
                    .GroupBy(e => e.internalCode)
                    .Select(g => g.First())
                    .Select(e =>
                    {
                        var mapped = StatusTimelineMapper.MapearPorInternalCode(e.internalCode)!;

                        return new ShippingEventDto(
                            code: mapped.CodTimeline,
                            dscode: mapped.StatusTimeline,
                            message: mapped.DsTimeline,
                            detalhe: e.info,
                            complement: e.complement,
                            dtshipping: e.date,
                            internalcode: e.internalCode
                        );
                    })
                    .ToList();

                return new RastreioTplResponse(
                    mockData.code,
                    mockData.message,
                    infoDto,
                    eventosDto
                );
            }
            catch (Exception)
            {
                // Fallback hardcoded com mapeamento
                var statusEntregue = StatusTimelineMapper.MapearPorInternalCode(90);

                return new RastreioTplResponse(
                    code: 200,
                    message: "OK (Mock Fallback)",
                    info: new OrderInfoDto(
                        id: "8064892",
                        number: "ENX8064892-1",
                        date: "10/01/2026",
                        prediction: "15/01/2026",
                        iderp: "PED-2026-001"
                    ),
                    shippingevents: new List<ShippingEventDto>
                    {
                        new ShippingEventDto(
                            code: statusEntregue?.CodTimeline ?? "7",
                            dscode: statusEntregue?.StatusTimeline ?? "Entregue",
                            message: statusEntregue?.DsTimeline ?? "Seu pedido foi entregue com sucesso!",
                            detalhe: "Objeto entregue ao destinatário",
                            complement: "Entregue para JOANNA",
                            dtshipping: "2026-01-15T14:30:00",
                            internalcode: 90
                        )
                    }
                );
            }
        }

        // Classes auxiliares para deserializar o JSON mock
        private class MockTplResponse
        {
            public int code { get; set; }
            public string? message { get; set; }
            public MockOrder? order { get; set; }
        }

        private class MockOrder
        {
            public MockOrderInfo? info { get; set; }
            public MockShippingEvent[]? shippingevents { get; set; }
        }

        private class MockOrderInfo
        {
            public string? id { get; set; }
            public string? number { get; set; }
            public string? date { get; set; }
            public string? prediction { get; set; }
            public string? iderp { get; set; }
        }

        private class MockShippingEvent
        {
            public int? internalCode { get; set; }
            public string? code { get; set; }
            public string? info { get; set; }
            public string? complement { get; set; }
            public string? date { get; set; }
        }
    }
}