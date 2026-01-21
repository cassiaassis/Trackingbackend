using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tracking.Domain;

namespace Tracking.Infrastructure.Repositories;

public interface IRastreioRepository
{
    Task<List<RastreioConsultaDto>> BuscarPorCpfOuEmailAsync(string identificador, CancellationToken ct = default);
}

