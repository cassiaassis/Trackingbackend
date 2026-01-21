using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Tracking.Application.Dto;

// DTO para requisição (opcional, se for usar POST)
public record RastreioRequest(string Identificador);

// DTO para eventos de rastreio (ajuste os campos conforme o que realmente retorna do banco)
public class RastreioEventoResponse
{
    public int? IdTimeline { get; set; }
    public string? StatusTimeline { get; set; }
    public string? Dstimeline { get; set; }
    public DateTime? Final { get; set; }
}

// DTO principal de resposta para o frontend
public class RastreioResponse
{
    public string? Cpf { get; set; }
    public string? Email { get; set; }
    public string? CdRastreio { get; set; }
    public DateTime? Prediction { get; set; }
    public List<RastreioEventoResponse> Eventos { get; set; } = [];
}

// DTOs extras (caso use em outros fluxos)
public record EventoDto(string Id, DateTime Date, string Name, string Description);
public record TransportadoraDto(string Nome, string CodigoExterno);