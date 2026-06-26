using System;
using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    public class AiOrder
    {
        [Key]
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string OrderType { get; set; } = "BUY";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "PENDING";
        public string? Rationale { get; set; }
    }
}
