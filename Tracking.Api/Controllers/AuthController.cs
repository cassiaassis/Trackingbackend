using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Tracking.Api.Controllers;

/// <summary>
/// Gerenciamento de autenticação
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Requisição de autenticação
    /// </summary>
    /// <param name="Identifier">CPF (somente números) ou e-mail do cliente</param>
    public record AuthRequest(string Identifier);

    /// <summary>
    /// Resposta com token JWT
    /// </summary>
    /// <param name="access_token">Token JWT para uso nas requisições protegidas</param>
    /// <param name="expires_at">Data/hora de expiração do token (UTC)</param>
    public record TokenResponse(string access_token, DateTime expires_at);

    /// <summary>
    /// Autentica um usuário e retorna um token JWT
    /// </summary>
    /// <param name="req">Identificador (CPF ou e-mail)</param>
    /// <returns>Token JWT válido por 30 minutos</returns>
    /// <response code="200">Token gerado com sucesso</response>
    /// <response code="400">Identificador não informado</response>
    [HttpPost("authenticate")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Authenticate([FromBody] AuthRequest req)
    {
        // Valida se o identificador foi informado
        if (string.IsNullOrWhiteSpace(req.Identifier))
            return BadRequest("E-mail ou CPF é obrigatório.");

        // ⚠️ Gera token para qualquer identificador válido
        // A validação real será feita no endpoint /api/rastreio

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var key = _configuration["Jwt:Key"];
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!));

        var lifetimeMin = int.TryParse(_configuration["Jwt:AccessTokenLifetimeMinutes"], out var minutes)
            ? minutes : 30;

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, req.Identifier),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim("identifier", req.Identifier)
        };

        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(lifetimeMin);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new TokenResponse(jwt, expires));
    }
}
