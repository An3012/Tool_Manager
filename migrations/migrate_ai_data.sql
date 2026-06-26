-- Migration script: Move AI simulation data to dedicated Ai_* tables
-- IMPORTANT: Run on a copy or ensure you have backups before running on production.
-- This script moves rows identified as AI simulation (Source='AI_SIMULATION' or IsAiTrade=1)
-- and removes them from the user-facing tables.

SET NOCOUNT ON;
GO

BEGIN TRANSACTION;

-- 1) Backup user-facing tables (quick copy)
DECLARE @tsBackupName NVARCHAR(128) = 'Backup_StockTransactions_' + CONVERT(VARCHAR(20), GETDATE(), 112);
DECLARE @tpBackupName NVARCHAR(128) = 'Backup_TradePositions_' + CONVERT(VARCHAR(20), GETDATE(), 112);
DECLARE @teBackupName NVARCHAR(128) = 'Backup_TradeEpisodes_' + CONVERT(VARCHAR(20), GETDATE(), 112);

PRINT 'Creating backups...';
IF OBJECT_ID('tempdb..#tmp') IS NOT NULL DROP TABLE #tmp;

EXEC('SELECT * INTO ' + @tsBackupName + ' FROM StockTransactions');
EXEC('SELECT * INTO ' + @tpBackupName + ' FROM TradePositions');
EXEC('SELECT * INTO ' + @teBackupName + ' FROM TradeEpisodes');

PRINT 'Backups created: ' + @tsBackupName + ', ' + @tpBackupName + ', ' + @teBackupName;

-- 2) Move StockTransactions where Source='AI_SIMULATION'
PRINT 'Moving AI StockTransactions...';
INSERT INTO Ai_StockTransactions (Symbol, TransactionType, Quantity, Price, TransactionDate, Fee, TotalAmount, PnlAmount, Source, PositionId, Notes, PriceHighSinceBuy, PriceLowSinceBuy, TimingScore)
SELECT Symbol, TransactionType, Quantity, Price, TransactionDate, Fee, TotalAmount, PnlAmount, Source, PositionId, Notes, PriceHighSinceBuy, PriceLowSinceBuy, TimingScore
FROM StockTransactions WHERE Source = 'AI_SIMULATION';

DELETE FROM StockTransactions WHERE Source = 'AI_SIMULATION';

-- 3) Move TradePositions where IsAiTrade = 1
PRINT 'Moving AI TradePositions...';
INSERT INTO Ai_TradePositions (Symbol, Quantity, EntryPrice, ExitPrice, EntryDate, ExitDate, Status, PnL, StopLossPrice, TakeProfitPrice, IsAiTrade, TargetProfitAmount, InvestedAmount, BudgetAmount, ExpectedHoldDays)
SELECT Symbol, Quantity, EntryPrice, ExitPrice, EntryDate, ExitDate, Status, PnL, StopLossPrice, TakeProfitPrice, IsAiTrade, TargetProfitAmount, InvestedAmount, BudgetAmount, ExpectedHoldDays
FROM TradePositions WHERE IsAiTrade = 1;

DELETE FROM TradePositions WHERE IsAiTrade = 1;

-- 4) Move TradeEpisodes - NOTE: No explicit marker for AI episodes. This will move all episodes.
-- If you only want to move a subset, modify the WHERE clause accordingly.
PRINT 'Moving TradeEpisodes (all rows) to Ai_TradeEpisodes...';
INSERT INTO Ai_TradeEpisodes (Id, MarketContext, ActionTaken, Rationale, Result, LessonLearned, Timestamp)
SELECT Id, MarketContext, ActionTaken, Rationale, Result, LessonLearned, Timestamp FROM TradeEpisodes;

DELETE FROM TradeEpisodes;

COMMIT TRANSACTION;

PRINT 'Migration complete. Please validate data and update application services to use Ai_* tables for simulation data.';
GO

-- Rollback note:
-- If you need to restore backups created above, run:
-- SELECT * INTO StockTransactions_restore FROM Backup_StockTransactions_YYYYMMDD;
-- SELECT * INTO TradePositions_restore FROM Backup_TradePositions_YYYYMMDD;
-- SELECT * INTO TradeEpisodes_restore FROM Backup_TradeEpisodes_YYYYMMDD;

-- Replace YYYYMMDD with the actual date used in backup names printed during script execution.
