using System;

namespace AITradingSystem.Models
{
    public class TradeEpisode
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string MarketContext { get; set; } = string.Empty; // JSON or Text describing context
        public string ActionTaken { get; set; } = string.Empty; // BUY/SELL
        public string Rationale { get; set; } = string.Empty; // Lý do
        public string Result { get; set; } = string.Empty; // Kết quả PnL
        public string LessonLearned { get; set; } = string.Empty; // Bài học sinh ra bởi Critic Agent
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
