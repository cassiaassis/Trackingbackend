using System.Text.Json.Serialization;

namespace Tracking.Application.Dto;

public record RastreioTplResponse(
    [property: JsonPropertyName("code")]
    int code,
    
    [property: JsonPropertyName("message")]
    string? message,
    
    [property: JsonPropertyName("info")]
    OrderInfoDto? info,
    
    [property: JsonPropertyName("shippingevents")]
    List<ShippingEventDto>? shippingevents
);

public record OrderInfoDto(
    [property: JsonPropertyName("id")]
    string? id,
    
    [property: JsonPropertyName("number")]
    string? number,
    
    [property: JsonPropertyName("date")]
    string? date,
    
    [property: JsonPropertyName("prediction")]
    string? prediction,
    
    [property: JsonPropertyName("iderp")]
    string? iderp
);

public record ShippingEventDto(
    [property: JsonPropertyName("code")]
    string? code,
    
    [property: JsonPropertyName("dscode")]
    string? dscode,
    
    [property: JsonPropertyName("message")]
    string? message,
    
    [property: JsonPropertyName("dtshipping")]
    string? dtshipping
);