using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracking.Application.Dto;

namespace Tracking.Application.Services;

public interface IClienteService
{
    /// <summary>
    /// Consulta por CPF ou e?mail e retorna o objeto pronto para o frontend.
    /// Se não houver cd_rastreio, retorna eventos vazios (frontend mostra "preparando").
    /// </summary>
    Task<RastreioResponse?> ConsultarAsync(string identificador, CancellationToken ct = default);


}