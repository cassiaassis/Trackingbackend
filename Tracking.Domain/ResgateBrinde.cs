using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Tracking.Domain;

public class ResgateBrinde
{
    // Tabela: TRKG_RESGATE_BRINDES
    public int IdResgate { get; set; }              // id_resgate (PK)
    public int IdCampanha { get; set; }             // id_campanha (FK)
    public DateTime? DataResgate { get; set; }      // data_resgate

    public string? Nome { get; set; }               // nome
    public string? Email { get; set; }              // email
    public string? Cpf { get; set; }                // cpf
    public string? Telefone { get; set; }           // telefone
    public string? Endereco { get; set; }           // endereco
    public string? Numero { get; set; }             // numero
    public string? Complemento { get; set; }        // complemento
    public string? Bairro { get; set; }             // bairro
    public string? Cep { get; set; }                // cep
    public string? Uf { get; set; }                 // uf
    public string? Cidade { get; set; }             // cidade
    public string? KitDescricao { get; set; }       // kit_descricao
    public string? Lote { get; set; }               // lote
    public string? Localidade { get; set; }         // localidade
    public string? ValorNf { get; set; }            // valor_nf
    public DateTime? DtRegistro { get; set; }       // dt_registro
    public DateTime? DtAtualizacao { get; set; }    // dt_atualizacao

    public Campanha Campanha { get; set; } = null!;
    public ICollection<RastreioResgate> Rastreios { get; set; } = new List<RastreioResgate>();
}

