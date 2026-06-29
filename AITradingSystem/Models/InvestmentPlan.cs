using System;
using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    public class InvestmentPlan
    {
        [Key]
        public int Id { get; set; }
        public DateTime RunDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Capital { get; set; }
        public decimal TargetProfit { get; set; }
        public decimal ActualProfit { get; set; }
        public decimal RemainingProfitNeeded { get; set; }
        public int DaysRemainingAtRun { get; set; }
        public decimal SuccessProbability { get; set; }
        public string? Status { get; set; } = "Pending";
        public decimal? FinalProfit { get; set; }
        public string? DailyCalendarJson { get; set; } = string.Empty;
    }
}
