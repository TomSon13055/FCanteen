using System;
using System.Collections.Generic;
using System.Text;

namespace FCanteen.Data.Entities
{
    public class MenuItem
    {
        public int MenuItemId { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string Unit { get; set; } = string.Empty;

        public bool IsAvailable { get; set; }

        public ICollection<TicketLine> TicketLines { get; set; }
            = new List<TicketLine>();
    }
}