using System;
using System.Collections.Generic;
using System.Text;

namespace FCanteen.Data.Entities
{
    public class TicketLine
    {
        public int TicketLineId { get; set; }

        public int OrderTicketId { get; set; }

        public int MenuItemId { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public string? Note { get; set; }

        public OrderTicket OrderTicket { get; set; } = null!;

        public MenuItem MenuItem { get; set; } = null!;
    }
}
