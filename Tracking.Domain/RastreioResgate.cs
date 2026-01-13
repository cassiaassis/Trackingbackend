using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Tracking.Domain;

public class RastreioResgate
{
    // Tabela: TRKG_RASTREIO_RESGATE
    public int IdRastreio { get; set; }             // id_rastreio (PK)
    public int IdResgate { get; set; }              // id_resgate (FK -> ResgateBrinde)

    public string? Cpf { get; set; }                // cpf
    public string? Email { get; set; }              // email
    public string? CodigoRastreio { get; set; }     // cd_rastreio

    public DateTime? DtRegistro { get; set; }       // dt_registro
    public DateTime? DtAtualizacao { get; set; }    // dt_atualizacao

    public ResgateBrinde Resgate { get; set; } = null!;
}
