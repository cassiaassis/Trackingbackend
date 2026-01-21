using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracking.Domain
{
    public class RetornoTPLShippingEvent
    {
        public int Id { get; set; }
        public int InfoId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? Detalhe { get; set; }
        public string? Complement { get; set; }
        public DateTime DtShipping { get; set; }
        public int InternalCode { get; set; }
        public DateTime DataCriacao { get; set; }

        // Relacionamento: Info
        public RetornoTPLInfo Info { get; set; } = null!;
    }

}
