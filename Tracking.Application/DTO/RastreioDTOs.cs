using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Tracking.Application.Dto;

public record RastreioRequest(string Identificador);

public record EventoDto(string id, DateTime date, string name, string description);
public record TransportadoraDto(string nome, string codigoExterno);

public record RastreioResponse(
    object cliente,
    string numeroPedido,
    string idPresente,
    string? codigoRastreio,
    TransportadoraDto? transportadora,
    List<EventoDto> eventos
);

