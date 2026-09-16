using System;
using System.Collections.Generic;
using System.Text;

namespace FCanteen.Data.Entities
{
    public class OrderTicket
    {
        public int OrderTicketId { get; set; }

        public string CounterName { get; set; } = string.Empty;

        public string BranchCode { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Status { get; set; } = string.Empty;

        public ICollection<TicketLine> TicketLines { get; set; }
            = new List<TicketLine>();
    }
}