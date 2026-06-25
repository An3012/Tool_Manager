using System;
using System.Collections.Generic;

namespace AITradingSystem.Services
{
    public class GlobalPortfolioPlanResult
    {
        public int SuccessProbability { get; set; } = 50;
        public string PlanSummary { get; set; } = string.Empty;
        public List<PlanAction> Actions { get; set; } = new List<PlanAction>();
        public List<ExpectedContribution> ExpectedContributions { get; set; } = new List<ExpectedContribution>();
        public string Rationale { get; set; } = string.Empty;
        public List<AITradingSystem.Models.DailyCalendarItem> DailyCalendar { get; set; } = new List<AITradingSystem.Models.DailyCalendarItem>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int RemainingDays { get; set; }
    }

    public class PlanAction
    {
        public string Ticker { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // MUA, BÁN, GIỮ, CHUYỂN VỐN
        public string Description { get; set; } = string.Empty;
    }

    public class ExpectedContribution
    {
        public string Ticker { get; set; } = string.Empty;
        public decimal ExpectedProfit { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
