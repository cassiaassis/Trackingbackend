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

        public async Task<List<RastreioConsultaDto>> BuscarPorCpfOuEmailAsync(string identificador, CancellationToken ct = default)
        {
            var results = new List<RastreioConsultaDto>();
            var conn = _db.Database.GetDbConnection();

            // Ajuste o filtro conforme sua necessidade (por CPF ou Email)
            var filtro = identificador.Trim().ToLowerInvariant();
            var isCpf = filtro.All(char.IsDigit) && filtro.Length == 11;
            var where = isCpf ? "c.cpf = @identificador" : "LOWER(LTRIM(RTRIM(c.email))) COLLATE Latin1_General_CI_AI = @identificador";

            var sql = $@"
                        select distinct
                            c.cpf, c.email, a.cd_rastreio, a.prediction, d.id_timeline, d.status_timeline, d.ds_timeline, cast(b.dtshipping as date)  as final
                        from TRKG_TPLOrderInfo a (nolock)
                        inner join TRKG_TplShippingEvent b (nolock)
                            on a.id = b.info_id
                        inner join TRKG_RASTREIO_RESGATE c (nolock)
                            on a.cd_rastreio = c.cd_rastreio
                        left outer join DBO.TRKG_RASTREIO_STATUS d (nolock)
                            on b.internalcode = d.internalcode_TPL
                        where {where}
                        group by c.cpf, c.email, a.cd_rastreio, a.prediction, d.id_timeline, d.status_timeline, d.ds_timeline, b.dtshipping
                        order by d.id_timeline desc
                    ";

            await using (var command = conn.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandType = System.Data.CommandType.Text;

                var param = command.CreateParameter();
                param.ParameterName = "@identificador";
                param.Value = filtro;
                command.Parameters.Add(param);

                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync(ct);

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    results.Add(new RastreioConsultaDto
                    {
                        Cpf = reader["cpf"] as string,
                        Email = reader["email"] as string,
                        CdRastreio = reader["cd_rastreio"] as string,
                        Prediction = reader["prediction"] as DateTime?,
                        IdTimeline = reader["id_timeline"] as int?,
                        StatusTimeline = reader["status_timeline"] as string,
                        Dstimeline = reader["ds_timeline"] as string,
                        Final = reader["final"] as DateTime?
                    });
                }
            }

            return results;
        }


    }


    public class RastreioConsultaDto
    {
        public string? Cpf { get; set; }
        public string? Email { get; set; }
        public string? CdRastreio { get; set; }
        public DateTime? Prediction { get; set; }
        public int? IdTimeline { get; set; }
        public string? StatusTimeline { get; set; }
        public string? Dstimeline { get; set; }
        public DateTime? Final { get; set; }
    }


}
