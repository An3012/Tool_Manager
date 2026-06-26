using System;
using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    public class AiTradeEpisode
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string MarketContext { get; set; } = string.Empty;
        public string ActionTaken { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string LessonLearned { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
