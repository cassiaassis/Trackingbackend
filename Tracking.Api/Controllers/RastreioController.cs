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
public class RastreioController : ControllerBase
{
    private readonly IClienteService _clientes;

    public RastreioController(IClienteService clientes)
    {
        _clientes = clientes;
    }

    [HttpGet("{identificador}")]
    public async Task<IActionResult> BuscarPorCpfOuEmail(string identificador, CancellationToken ct)
    {
        var result = await _clientes.ConsultarAsync(identificador, ct);

        if (result == null)
            return NotFound();

        return Ok(result); // Serialização automática para JSON
    }

    [HttpPost]
    public async Task<IActionResult> BuscarPorCpfOuEmailPost([FromBody] RastreioRequest request, CancellationToken ct)
    {
        var result = await _clientes.ConsultarAsync(request.Identificador, ct);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

}

