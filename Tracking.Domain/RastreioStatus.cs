using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracking.Domain;

/// <summary>
/// Entidade para mapeamento de status de rastreio
/// Tabela: TRKG_RASTREIO_STATUS
/// </summary>
public class RastreioStatus
{
    public int Id { get; set; }                         // id (PK)
    public int InternalCodeTpl { get; set; }            // internalcode_TPL
    public int IdTimeline { get; set; }                 // id_timeline
    public string? StatusTimeline { get; set; }         // status_timeline
    public string? DsTimeline { get; set; }             // ds_timeline
    public DateTime DtRegistro { get; set; }            // dt_registro
    public DateTime? DtAtualizacao { get; set; }        // dt_atualizacao
}
