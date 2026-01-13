using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Tracking.Domain;
using Tracking.Infrastructure.Data;

namespace Tracking.Infrastructure.Repositories
{
    public class RastreioRepository : IRastreioRepository
    {
        private readonly AppDbContext _db;
        public RastreioRepository(AppDbContext db) => _db = db;

        public async Task<(ResgateBrinde resgate, RastreioResgate? rastreio)?> BuscarPorCpfOuEmailAsync(
            string identificador,
            CancellationToken ct = default)
        {
            var raw = identificador.Trim();
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            bool isCpf = digits.Length == 11;

            if (isCpf)
            {
                // CPF prioritário em TRKG_RASTREIO_RESGATE
                var item = await _db.Rastreios
                    .AsNoTracking()
                    .Include(r => r.Resgate)
                    .Where(r => r.Cpf == digits)
                    .OrderByDescending(r => r.DtAtualizacao ?? r.DtRegistro ?? DateTime.MinValue)
                    .ThenByDescending(r => r.IdRastreio)
                    .FirstOrDefaultAsync(ct);

                if (item is null) return null;
                return (item.Resgate, item);
            }
            else
            {
                // EMAIL prioritário em TRKG_RASTREIO_RESGATE
                var item = await _db.Rastreios
                    .AsNoTracking()
                    .Include(r => r.Resgate)
                    .Where(r => r.Email != null && r.Email.ToLower() == raw.ToLower())
                    .OrderByDescending(r => r.DtAtualizacao ?? r.DtRegistro ?? DateTime.MinValue)
                    .ThenByDescending(r => r.IdRastreio)
                    .FirstOrDefaultAsync(ct);

                if (item is not null)
                    return (item.Resgate, item);

                // Fallback: resgate por e‑mail (pode não ter rastreio ainda)
                var resgate = await _db.Resgates
                    .AsNoTracking()
                    .Include(r => r.Rastreios)
                    .Where(r => r.Email != null && r.Email.ToLower() == raw.ToLower())
                    .OrderByDescending(r => r.DtAtualizacao ?? r.DtRegistro ?? DateTime.MinValue)
                    .ThenByDescending(r => r.IdResgate)
                    .FirstOrDefaultAsync(ct);

                if (resgate is null) return null;

                var rastreio = resgate.Rastreios
                    .OrderByDescending(x => x.DtAtualizacao ?? x.DtRegistro ?? DateTime.MinValue)
                    .ThenByDescending(x => x.IdRastreio)
                    .FirstOrDefault(); // pode ser null => "preparando"

                return (resgate, rastreio);
            }
        }
    }
}
