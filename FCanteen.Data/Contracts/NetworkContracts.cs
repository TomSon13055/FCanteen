namespace FCanteen.Data.Contracts
{
    public class OrderRequest
    {
        public string CounterName { get; set; } = string.Empty;
        public List<OrderLineRequest> Lines { get; set; } = new();
    }

    public class OrderLineRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
        public string? Note { get; set; }
    }

    public class OrderResponse
    {
        public bool Success { get; set; }
        public int? OrderTicketId { get; set; }
        public decimal TotalAmount { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SoldOutMessage
    {
        public string Type { get; set; } = "SOLD_OUT";
        public int MenuItemId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class PriceSyncItem
    {
        public int MenuItemId { get; set; }
        public decimal Price { get; set; }
    }
}