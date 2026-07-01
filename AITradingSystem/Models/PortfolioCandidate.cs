namespace AITradingSystem.Models
{
    public class PortfolioCandidate
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal ExpectedReturn { get; set; }
        public decimal ExpectedRisk { get; set; } // Represented as volatility or score
        public double ConfidenceScore { get; set; }
        public double LiquidityScore { get; set; }
        public double TrendScore { get; set; }
        public double RSIScore { get; set; }
        public double AISignalScore { get; set; }
        public double RewardRiskRatio { get; set; }
        public string Sector { get; set; } = "General";
        public bool IsHolding { get; set; }
        public bool CanBuy { get; set; }
        public bool CanSell { get; set; }
    }
}
