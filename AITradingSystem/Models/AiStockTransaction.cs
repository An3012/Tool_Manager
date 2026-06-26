using System;
using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    public class AiStockTransaction
    {
        [Key]
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string TransactionType { get; set; } = "BUY";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal? Fee { get; set; }
        public decimal? Tax { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? PnlAmount { get; set; }
        public string Source { get; set; } = "AI_SIMULATION";
        public int? PositionId { get; set; }
        public string? Notes { get; set; }
        public decimal? PriceHighSinceBuy { get; set; }
        public decimal? PriceLowSinceBuy { get; set; }
        public decimal? TimingScore { get; set; }
    }
}
