namespace Tracking.Application.Dto;

public record RastreioTplResponse(
    int code,
    string? message,
    OrderInfoDto? info,
    List<ShippingEventDto> shippingevents
);

public record OrderInfoDto(
    string? id,
    string? number,
    string? date,
    string? prediction,
    string? iderp
);

public record ShippingEventDto(
    string? code,
    string? dscode,
    string? message,
    string? detalhe,
    string? complement,
    string? dtshipping,
    int? internalcode
);