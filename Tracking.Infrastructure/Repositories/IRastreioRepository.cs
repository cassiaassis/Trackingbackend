using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracking.Domain;

namespace Tracking.Infrastructure.Repositories;

public interface IRastreioRepository
{
    /// <summary>
    /// Busca resgate e último rastreio relacionado por CPF ou e‑mail.
    /// Retorna null se nada encontrado.
    /// </summary>
    Task<(ResgateBrinde resgate, RastreioResgate? rastreio)?> BuscarPorCpfOuEmailAsync(
        string identificador,
        CancellationToken ct = default);
}

