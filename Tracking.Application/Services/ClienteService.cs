using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Tracking.Application.Dto;
using Tracking.Infrastructure.Repositories;

namespace Tracking.Application.Services
{
    public class ClienteService : IClienteService
    {
        private readonly IRastreioRepository _repo;
        private readonly ITplService _tpl;
        private readonly IRastreioStatusRepository _statusRepo;

        public ClienteService(IRastreioRepository repo, ITplService tpl, IRastreioStatusRepository statusRepo)
        {
            _repo = repo;
            _tpl = tpl;
            _statusRepo = statusRepo;
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

            var (info, shippingevents, code, message) = await _tpl.ObterDadosBrutosAsync(codigo!, orderId, ct);

            var eventos = new List<EventoDto>();
            foreach (var e in shippingevents ?? new List<TplShippingEvent>())
            {
                if (!e.internalCode.HasValue) continue;

                var status = await _statusRepo.BuscarPorInternalCodeAsync(e.internalCode.Value, ct);
                if (status is null) continue;

                eventos.Add(new EventoDto(
                    id: status.IdTimeline.ToString(),
                    date: DateTime.TryParse(e.date, out var dt) ? dt : DateTime.UtcNow,
                    name: status.StatusTimeline ?? "Atualização",
                    description: status.DsTimeline ?? string.Empty
                ));
            }

            // Agrupar por id_timeline e pegar o mais recente de cada grupo
            eventos = eventos
                .GroupBy(e => e.id)
                .Select(g => g.OrderByDescending(x => x.date).First())
                .OrderByDescending(e => e.date)
                .ToList();

            return baseResponse with
            {
                transportadora = new TransportadoraDto("TPL", codigo!),
                eventos = eventos
            };
        }

        public async Task<RastreioTplResponse?> ConsultarTplAsync(string identificador, CancellationToken ct = default)
        {
            var cpfLimpo = new string(identificador.Where(char.IsDigit).ToArray());

            // Mock 3: CPF não localizado (404)
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
                    shippingevents: new List<ShippingEventDto>()
                );
            }

            // Mock 2: Em preparação (cd_rastreio NULL)
            if (cpfLimpo == "12676652800")
            {
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
                            code: "1",
                            dscode: "Em preparação",
                            message: "Estamos preparando seu presente com carinho.",
                            dtshipping: DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")
                        )
                    }
                );
            }

            // Mock 1: Pedido completo com rastreio da TPL
            if (cpfLimpo == "32676652800")
            {
                return CarregarMockTplJson();
            }

            // Fluxo normal (não é mock)
            var data = await _repo.BuscarPorCpfOuEmailAsync(identificador, ct);

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
                    shippingevents: new List<ShippingEventDto>()
                );
            }

            var resgate = data.Value.resgate;
            var rastreio = data.Value.rastreio;
            var codigo = rastreio?.CodigoRastreio;
            var orderId = resgate.IdResgate;

            // cd_rastreio é NULL (Em preparação)
            if (string.IsNullOrWhiteSpace(codigo))
            {
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
                            code: "1",
                            dscode: "Em preparação",
                            message: "Estamos preparando seu pedido",
                            dtshipping: dtRegistro
                        )
                    }
                );
            }

            // cd_rastreio existe - Buscar na tabela TRKG_RASTREIO_STATUS
            var (info, shippingevents, code, message) = await _tpl.ObterDadosBrutosAsync(codigo!, orderId, ct);

            var infoDto = info is not null
                ? new OrderInfoDto(info.id, info.number, info.date, info.prediction, info.iderp)
                : null;

            var eventosDto = new List<ShippingEventDto>();

            foreach (var e in shippingevents ?? new List<TplShippingEvent>())
            {
                if (!e.internalCode.HasValue) continue;

                var status = await _statusRepo.BuscarPorInternalCodeAsync(e.internalCode.Value, ct);
                if (status is null) continue;

                eventosDto.Add(new ShippingEventDto(
                    code: status.IdTimeline.ToString(),
                    dscode: status.StatusTimeline,
                    message: status.DsTimeline,
                    dtshipping: e.date
                ));
            }

            // Agrupar por id_timeline e mostrar o mais recente de cada grupo
            eventosDto = eventosDto
                .OrderByDescending(e => DateTime.TryParse(e.dtshipping, out var dt) ? dt : DateTime.MinValue)
                .GroupBy(e => e.code)
                .Select(g => g.First())
                .ToList();

            return new RastreioTplResponse(code, message, infoDto, eventosDto);
        }

        private RastreioTplResponse CarregarMockTplJson()
        {
            // Mock simplificado, sem uso de Mapper
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
                        code: "7",
                        dscode: "Entregue",
                        message: "Seu pedido foi entregue com sucesso!",
                        dtshipping: "2026-01-15T14:30:00"
                    )
                }
            );
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