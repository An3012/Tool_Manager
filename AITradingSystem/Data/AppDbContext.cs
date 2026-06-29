using AITradingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AITradingSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Asset>? Assets { get; set; }
        public DbSet<Order>? Orders { get; set; }
        public DbSet<TradePosition>? TradePositions { get; set; }
        public DbSet<TradeEpisode>? TradeEpisodes { get; set; }
        public DbSet<UserPreference>? UserPreferences { get; set; }
        public DbSet<TradingStrategy>? TradingStrategies { get; set; }
        public DbSet<StockTransaction>? StockTransactions { get; set; }

        // AI-specific DbSets (separated storage)
        public DbSet<AiStockTransaction>? AiStockTransactions { get; set; }
        public DbSet<AiTradeEpisode>? AiTradeEpisodes { get; set; }
        public DbSet<AiTradePosition>? AiTradePositions { get; set; }
        public DbSet<AiOrder>? AiOrders { get; set; }

        // AI Learning & Planning DbSets
        public DbSet<AiPlanPrediction>? AiPlanPredictions { get; set; }
        public DbSet<AiLearningKnowledge>? AiLearningKnowledge { get; set; }
        public DbSet<UserAiGoal>? UserAiGoals { get; set; }
        public DbSet<InvestmentPlan>? InvestmentPlans { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Precision for decimals
            modelBuilder.Entity<Order>()
                .Property(o => o.Price)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<TradePosition>()
                .Property(t => t.EntryPrice)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<TradePosition>()
                .Property(t => t.ExitPrice)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<TradePosition>()
                .Property(t => t.PnL)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<TradePosition>()
                .Property(t => t.StopLossPrice)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<TradePosition>()
                .Property(t => t.TakeProfitPrice)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<TradePosition>()
                .Property(t => t.TargetProfitAmount)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<TradePosition>()
                .Property(t => t.InvestedAmount)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<TradePosition>()
                .Property(t => t.BudgetAmount)
                .HasColumnType("decimal(18,4)");

            // StockTransaction decimal precision
            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.Price)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.Fee)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.Tax)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.TotalAmount)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.PnlAmount)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.PriceHighSinceBuy)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.PriceLowSinceBuy)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.TimingScore)
                .HasColumnType("decimal(18,4)");

            // AI table decimal precision
            modelBuilder.Entity<AiStockTransaction>()
                .Property(a => a.Price)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiStockTransaction>()
                .Property(a => a.Fee)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiStockTransaction>()
                .Property(a => a.Tax)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiStockTransaction>()
                .Property(a => a.TotalAmount)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiStockTransaction>()
                .Property(a => a.PnlAmount)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiStockTransaction>()
                .Property(a => a.PriceHighSinceBuy)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiStockTransaction>()
                .Property(a => a.PriceLowSinceBuy)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiStockTransaction>()
                .Property(a => a.TimingScore)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiTradePosition>()
                .Property(p => p.EntryPrice)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiTradePosition>()
                .Property(p => p.ExitPrice)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiTradePosition>()
                .Property(p => p.PnL)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiTradePosition>()
                .Property(p => p.StopLossPrice)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiTradePosition>()
                .Property(p => p.TakeProfitPrice)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiTradePosition>()
                .Property(p => p.TargetProfitAmount)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiTradePosition>()
                .Property(p => p.InvestedAmount)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<AiTradePosition>()
                .Property(p => p.BudgetAmount)
                .HasColumnType("decimal(18,4)");

            // AiPlanPrediction decimal precision
            modelBuilder.Entity<AiPlanPrediction>()
                .Property(p => p.TargetProfit)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<AiPlanPrediction>()
                .Property(p => p.Capital)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<AiPlanPrediction>()
                .Property(p => p.SuccessProbability)
                .HasColumnType("decimal(5,2)");

            // UserAiGoal decimal precision
            modelBuilder.Entity<UserAiGoal>()
                .Property(g => g.InitialCapital)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<UserAiGoal>()
                .Property(g => g.TargetProfit)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<UserAiGoal>()
                .Property(g => g.ProgressPercentage)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<UserAiGoal>()
                .Property(g => g.MinDailyReturnPct)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<UserAiGoal>()
                .Property(g => g.MaxDailyLossPct)
                .HasColumnType("decimal(5,2)");

            // AiLearningKnowledge decimal precision
            modelBuilder.Entity<AiLearningKnowledge>()
                .Property(k => k.AccuracyScore)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<AiLearningKnowledge>()
                .Property(k => k.WeightScore)
                .HasColumnType("decimal(5,2)");

            // InvestmentPlan decimal precision (if needed)
            modelBuilder.Entity<InvestmentPlan>()
                .Property(i => i.Capital)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<InvestmentPlan>()
                .Property(i => i.TargetProfit)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<InvestmentPlan>()
                .Property(i => i.ActualProfit)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<InvestmentPlan>()
                .Property(i => i.RemainingProfitNeeded)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<InvestmentPlan>()
                .Property(i => i.SuccessProbability)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<InvestmentPlan>()
                .Property(i => i.FinalProfit)
                .HasColumnType("decimal(18,4)");
        }
    }
}
