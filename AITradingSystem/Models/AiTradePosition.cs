using System;
using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    public class AiTradePosition
    {
        [Key]
        public int Id { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal? ExitPrice { get; set; }
        public DateTime EntryDate { get; set; } = DateTime.UtcNow;
        public DateTime? ExitDate { get; set; }
        public string Status { get; set; } = "OPEN";
        public decimal PnL { get; set; }
        public decimal? StopLossPrice { get; set; }
        public decimal? TakeProfitPrice { get; set; }
        public decimal? TargetProfitAmount { get; set; }
        public decimal? InvestedAmount { get; set; }
        public decimal? BudgetAmount { get; set; }
        public int? ExpectedHoldDays { get; set; }
    }
}
