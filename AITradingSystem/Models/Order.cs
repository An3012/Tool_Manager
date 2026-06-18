using System;
using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string OrderType { get; set; } = "BUY"; // BUY, SELL
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "PENDING"; // PENDING, FILLED, REJECTED
        public string Rationale { get; set; } = string.Empty; // Lý do Agent đặt lệnh
    }
}
