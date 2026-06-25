using System;

namespace AITradingSystem.Models
{
    public class DailyCalendarItem
    {
        public string Date { get; set; } = string.Empty;
        public string DayOfWeek { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string CumulativePnL { get; set; } = string.Empty;
        public string ActualAction { get; set; } = string.Empty;
    }
}
