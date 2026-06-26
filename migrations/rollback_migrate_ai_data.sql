-- Rollback helper for migrate_ai_data.sql
-- This script assumes you still have the backup tables created by the migration script.
-- Modify the backup table names accordingly before running.

-- Example usage:
-- COPY the backup table names from the migration output and replace the variables below.

-- DECLARE @tsBackup NVARCHAR(128) = 'Backup_StockTransactions_20260625'
-- DECLARE @tpBackup NVARCHAR(128) = 'Backup_TradePositions_20260625'
-- DECLARE @teBackup NVARCHAR(128) = 'Backup_TradeEpisodes_20260625'

-- Then run the restore:
-- INSERT INTO StockTransactions (Symbol, TransactionType, Quantity, Price, TransactionDate, Fee, TotalAmount, PnlAmount, Source, PositionId, Notes, PriceHighSinceBuy, PriceLowSinceBuy, TimingScore)
-- SELECT Symbol, TransactionType, Quantity, Price, TransactionDate, Fee, TotalAmount, PnlAmount, Source, PositionId, Notes, PriceHighSinceBuy, PriceLowSinceBuy, TimingScore FROM Backup_StockTransactions_20260625;

-- INSERT INTO TradePositions (Symbol, Quantity, EntryPrice, ExitPrice, EntryDate, ExitDate, Status, PnL, StopLossPrice, TakeProfitPrice, IsAiTrade, TargetProfitAmount, InvestedAmount, BudgetAmount, ExpectedHoldDays)
-- SELECT Symbol, Quantity, EntryPrice, ExitPrice, EntryDate, ExitDate, Status, PnL, StopLossPrice, TakeProfitPrice, IsAiTrade, TargetProfitAmount, InvestedAmount, BudgetAmount, ExpectedHoldDays FROM Backup_TradePositions_20260625;

-- INSERT INTO TradeEpisodes (Id, MarketContext, ActionTaken, Rationale, Result, LessonLearned, Timestamp)
-- SELECT Id, MarketContext, ActionTaken, Rationale, Result, LessonLearned, Timestamp FROM Backup_TradeEpisodes_20260625;

PRINT 'Edit this file: set the backup table names created by migration and run to restore original tables if needed.';
