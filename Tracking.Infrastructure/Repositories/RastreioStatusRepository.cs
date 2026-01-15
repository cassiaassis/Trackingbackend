using Microsoft.EntityFrameworkCore;
using Tracking.Domain;
using Tracking.Infrastructure.Data;

namespace Tracking.Infrastructure.Repositories;

public class RastreioStatusRepository : IRastreioStatusRepository
{
    private readonly AppDbContext _db;

    public RastreioStatusRepository(AppDbContext db) => _db = db;

    public async Task<RastreioStatus?> BuscarPorInternalCodeAsync(int internalCodeTpl, CancellationToken ct = default)
    {
        return await _db.RastreiosStatus
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.InternalCodeTpl == internalCodeTpl, ct);
    }
}
