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
        private readonly IRastreioRepository _rastreioRepository;

        public ClienteService(IRastreioRepository rastreioRepository)
        {
            _rastreioRepository = rastreioRepository;
        }

        public async Task<RastreioResponse?> ConsultarAsync(string identificador, CancellationToken ct = default)
        {
            var lista = await _rastreioRepository.BuscarPorCpfOuEmailAsync(identificador, ct);

            if (lista == null || lista.Count == 0)
                return null;

            var response = new RastreioResponse
            {
                Cpf = lista[0].Cpf,
                Email = lista[0].Email,
                CdRastreio = lista[0].CdRastreio,
                Prediction = lista[0].Prediction,
                Eventos = lista.Select(x => new RastreioEventoResponse
                {
                    IdTimeline = x.IdTimeline,
                    StatusTimeline = x.StatusTimeline,
                    DsTimeline = x.Dstimeline,
                    Final = x.Final
                }).ToList()
            };

            return response;
        }

    }

    // DTOs de resposta (ajuste conforme sua necessidade)
    public class RastreioResponse
    {
        public string? Cpf { get; set; }
        public string? Email { get; set; }
        public string? CdRastreio { get; set; }
        public DateTime? Prediction { get; set; }
        public List<RastreioEventoResponse> Eventos { get; set; } = [];
    }

    public class RastreioEventoResponse
    {
        public int? IdTimeline { get; set; }
        public string? StatusTimeline { get; set; }
        public string? DsTimeline { get; set; }

        public DateTime? Final { get; set; }
    }
}