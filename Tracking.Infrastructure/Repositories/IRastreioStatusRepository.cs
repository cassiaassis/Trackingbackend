using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracking.Domain;

namespace Tracking.Infrastructure.Repositories
{
    public interface IRastreioStatusRepository
    {
        /// <summary>
        /// Busca status de rastreio por internalCode da TPL
        /// </summary>
        /// <param name="internalCodeTpl">Código interno da TPL (internalCode)</param>
        /// <param name="ct">Token de cancelamento</param>
        /// <returns>Status encontrado ou null</returns>
        Task<RastreioStatus?> BuscarPorInternalCodeAsync(int internalCodeTpl, CancellationToken ct = default);
    }
}
