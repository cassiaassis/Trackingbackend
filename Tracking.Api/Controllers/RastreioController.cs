using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Tracking.Application.Dto;
using Tracking.Application.Services;

namespace Tracking.Api.Controllers;

/// <summary>
/// Consulta de rastreamento de pedidos
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RastreioController : ControllerBase
{
    private readonly IClienteService _clientes;
    
    public RastreioController(IClienteService clientes) => _clientes = clientes;

    /// <summary>
    /// Consulta informações de rastreamento por CPF ou e-mail
    /// </summary>
    /// <param name="req">CPF (somente números) ou e-mail do cliente</param>
    /// <param name="ct">Token de cancelamento</param>
    /// <returns>Informações de rastreamento do pedido</returns>
    /// <remarks>
    /// Exemplo de requisição:
    /// 
    ///     POST /api/rastreio
    ///     {
    ///       "identificador": "11211311411"
    ///     }
    /// 
    /// Possíveis cenários de resposta:
    /// - CPF/e-mail não encontrado: HTTP 404
    /// - Pedido em preparação (sem código de rastreio): dscode = "Em preparação"
    /// - Pedido com rastreio: retorna eventos da transportadora TPL
    /// </remarks>
    /// <response code="200">Consulta realizada com sucesso</response>
    /// <response code="400">Identificador não informado</response>
    /// <response code="401">Token JWT inválido ou expirado</response>
    /// <response code="404">CPF ou e-mail não localizado</response>
    /// <response code="502">Serviço TPL indisponível</response>
    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(RastreioTplResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Post([FromBody] RastreioRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Identificador))
            return BadRequest("Identificador vazio.");

        try
        {
            var result = await _clientes.ConsultarTplAsync(req.Identificador, ct);
            
            // Se CPF não encontrado, retornar 404
            if (result?.message == "CPF ou e-mail não localizado.")
            {
                return NotFound(new { message = result.message });
            }
            
            return Ok(result);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is HttpStatusCode.BadGateway
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.NotFound)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "TPL indisponível.");
        }
    }
}

