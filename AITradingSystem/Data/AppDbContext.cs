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

            // UserPreference decimal precision

            // StockTransaction decimal precision
            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.Price)
                .HasColumnType("decimal(18,4)");

            modelBuilder.Entity<StockTransaction>()
                .Property(t => t.Fee)
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
        }
    }
}
