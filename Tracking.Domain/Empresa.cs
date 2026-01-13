using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Tracking.Domain;

public class Empresa
{
    // Tabela: TRKG_EMPRESA
    public int IdEmpresa { get; set; }       // id_empresa
    public string? RazaoSocial { get; set; } // razaosocial
    public string? NomeFantasia { get; set; }// nomefantasia
    public string? Cnpj { get; set; }        // cnpj
    public string? Email { get; set; }       // email
    public string? Responsavel { get; set; } // responsavel
    public string? Telefone { get; set; }    // telefone
    public string? CdStatus { get; set; }    // cd_status
    public DateTime? DtRegistro { get; set; }// dt_registro
    public DateTime? DtAtualizacao { get; set; } // dt_atualizacao

    public ICollection<Campanha> Campanhas { get; set; } = new List<Campanha>();
}


