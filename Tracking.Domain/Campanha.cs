using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Tracking.Domain;

public class Campanha
{
    // Tabela: TRKG_CAMPANHA
    public int IdCampanha { get; set; }          // id_campanha
    public int IdEmpresa { get; set; }           // id_empresa
    public string? DescCampanha { get; set; }    // desc_campanha
    public DateTime? DtInicio { get; set; }      // dt_inicio
    public DateTime? DtFim { get; set; }         // dt_fim
    public string? CdStatus { get; set; }        // cd_status
    public DateTime? DtRegistro { get; set; }    // dt_registro
    public DateTime? DtAtualizacao { get; set; } // dt_atualizacao

    public Empresa Empresa { get; set; } = null!;
    public ICollection<ResgateBrinde> Resgates { get; set; } = new List<ResgateBrinde>();
}

