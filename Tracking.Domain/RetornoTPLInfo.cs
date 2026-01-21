using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tracking.Domain
{

    public class RetornoTPLInfo
    {
        public int Id { get; set; }
        public int Info { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime? Prediction { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime DataAtualizacao { get; set; }

        // Relacionamento: ShippingEvents
        public ICollection<RetornoTPLShippingEvent> ShippingEvents { get; set; } = new List<RetornoTPLShippingEvent>();
    }
    
}
