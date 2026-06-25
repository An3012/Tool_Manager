-- =====================================================================
-- KỊCH BẢN KHỞI TẠO CƠ SỞ DỮ LIỆU TOÀN DIỆN (RESET & CREATE NEW)
-- Last Updated: 2026-06-15 15:20:00 (Sửa lỗi cú pháp CREATE OR REPLACE TABLE)
-- Chạy toàn bộ file này trong SSMS để tạo mới hoặc làm sạch dữ liệu cũ
-- =====================================================================

-- Bỏ comment 3 dòng dưới nếu bạn chưa tạo Database
-- CREATE DATABASE AITradingDB_v1;
-- GO
-- USE AITradingDB_v1;
-- GO

-- =====================================================================
-- BƯỚC 1: XÓA CÁC BẢNG CŨ NẾU ĐÃ TỒN TẠI (Để tránh lỗi trùng lặp khi chạy lại)
-- =====================================================================
DROP TABLE IF EXISTS TradingStrategies;
DROP TABLE IF EXISTS UserPreferences;
DROP TABLE IF EXISTS TradeEpisodes;
DROP TABLE IF EXISTS TradePositions;
DROP TABLE IF EXISTS Orders;
DROP TABLE IF EXISTS Assets;
GO

-- =====================================================================
-- BƯỚC 2: TẠO CÁC BẢNG DỮ LIỆU (Lưu ý: SQL Server không hỗ trợ CREATE OR REPLACE TABLE)
-- =====================================================================

-- 1. Bảng lưu trữ danh sách các mã cổ phiếu
CREATE TABLE Assets (
    Symbol NVARCHAR(450) PRIMARY KEY,       -- Mã cổ phiếu (Ví dụ: FPT, SSI)
    Exchange NVARCHAR(MAX) NOT NULL,        -- Sàn giao dịch (HOSE, HNX, UPCOM)
    CompanyName NVARCHAR(MAX) NOT NULL      -- Tên công ty đầy đủ
);
GO

-- 2. Bảng lưu trữ lịch sử đặt lệnh của AI Copilot
CREATE TABLE Orders (
    Id INT IDENTITY(1,1) PRIMARY KEY,       -- ID tự tăng
    Symbol NVARCHAR(MAX) NOT NULL,          -- Mã cổ phiếu
    OrderType NVARCHAR(MAX) NOT NULL,       -- Loại lệnh: BUY, SELL, HOLD
    Quantity INT NOT NULL,                  -- Số lượng cổ phiếu đặt mua/bán
    Price DECIMAL(18,4) NOT NULL,           -- Giá đặt khớp
    OrderDate DATETIME2 NOT NULL,           -- Ngày giờ đặt lệnh
    Status NVARCHAR(MAX) NOT NULL,          -- Trạng thái: PENDING, FILLED, REJECTED
    Rationale NVARCHAR(MAX) NOT NULL        -- Lập luận giải thích lý do giao dịch của AI
);
GO

-- 3. Bảng lưu trữ các vị thế đang nắm giữ và kết quả PnL thực tế
CREATE TABLE TradePositions (
    Id INT IDENTITY(1,1) PRIMARY KEY,       -- ID vị thế tự tăng
    Symbol NVARCHAR(MAX) NOT NULL,          -- Mã cổ phiếu đang giữ
    Quantity INT NOT NULL,                  -- Khối lượng đang giữ
    EntryPrice DECIMAL(18,4) NOT NULL,      -- Giá mua trung bình
    ExitPrice DECIMAL(18,4) NULL,           -- Giá bán (Null nếu đang giữ)
    EntryDate DATETIME2 NOT NULL,           -- Ngày mua
    ExitDate DATETIME2 NULL,                -- Ngày bán
    Status NVARCHAR(MAX) NOT NULL,          -- Trạng thái: OPEN (Đang giữ), CLOSED (Đã bán)
    --update: 17:13 date 16/06/2026
    PnL DECIMAL(18,4) NOT NULL,             -- Lợi nhuận tạm tính hoặc thực tế khi đóng
    --update: 17:13 date 16/06/2026
    StopLossPrice DECIMAL(18,4) NULL,       -- Giá cắt lỗ
    --update: 17:13 date 16/06/2026
    TakeProfitPrice DECIMAL(18,4) NULL,     -- Giá chốt lời
    --update: 17:13 date 16/06/2026(datetime.now)
    IsAiTrade BIT NOT NULL  DEFAULT 0,      -- Cờ phân biệt: 1 = Lệnh tự học AI, 0 = Lệnh thật của người dùng
    --update: 09:55 date 17/06/2026
    TargetProfitAmount DECIMAL(18,4) NULL,   -- Mục tiêu lợi nhuận mong muốn (VND) cho vị thế này
    --update: 10:13 date 17/06/2026
    InvestedAmount DECIMAL(18,4) NULL,       -- Số vốn đã đầu tư thực tế vào vị thế này (VND)
    --update: 11:40 date 17/06/2026
    BudgetAmount DECIMAL(18,4) NULL,         -- Hạn mức vốn riêng của chính mã này (VND)
    ExpectedHoldDays INT NULL                -- Số ngày nắm giữ dự kiến cho vị thế này
);
GO

-- 4. Bảng lưu trữ Trí nhớ dài hạn & Bài học kinh nghiệm (Memory Engine)
CREATE TABLE TradeEpisodes (
    Id UNIQUEIDENTIFIER PRIMARY KEY,        -- Khóa chính UUID
    MarketContext NVARCHAR(MAX) NOT NULL,   -- Hoàn cảnh kỹ thuật thị trường lúc đó
    ActionTaken NVARCHAR(MAX) NOT NULL,     -- Hành động thực hiện
    Rationale NVARCHAR(MAX) NOT NULL,       -- Dòng suy nghĩ ban đầu
    Result NVARCHAR(MAX) NOT NULL,          -- Kết quả lãi/lỗ thực tế sau đó
    LessonLearned NVARCHAR(MAX) NOT NULL,   -- BÀI HỌC rút ra do Critic Agent tự phê bình
    Timestamp DATETIME2 NOT NULL            -- Thời gian ghi lại bài học
);
GO

-- 5. Bảng cấu hình mục tiêu người dùng & Thông tin đăng nhập DNSE/DNSE
CREATE TABLE UserPreferences (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    InvestmentHorizon NVARCHAR(MAX) NOT NULL,
    TargetProfitPercentage DECIMAL(18,4) NOT NULL,
    MaxLossPercentage DECIMAL(18,4) NOT NULL,
    AmountPerTrade DECIMAL(18,4) NOT NULL,
    RiskPerTrade DECIMAL(18,4) NOT NULL DEFAULT 0,
    TargetStartDate DATETIME2 NOT NULL DEFAULT GETDATE(),
    TargetAmount DECIMAL(18,4) NOT NULL,
    TakeProfitAmount DECIMAL(18,4) NOT NULL,
    StopLossAmount DECIMAL(18,4) NOT NULL,
    RiskTolerance NVARCHAR(MAX) NOT NULL,
    DnseUsername NVARCHAR(MAX) NOT NULL DEFAULT '',
    DnsePassword NVARCHAR(MAX) NOT NULL DEFAULT '',
    DnseToken NVARCHAR(MAX) NOT NULL DEFAULT ''
);
GO
-- 6. Bảng lưu trữ Thư viện Chiến lược giao dịch (Knowledge Engine)
CREATE TABLE TradingStrategies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(MAX) NOT NULL,          -- Tên mô hình/chiến lược
    StrategyType NVARCHAR(MAX) NOT NULL,  -- Phân loại: Indicator, ChartPattern, PriceAction
    Description NVARCHAR(MAX) NOT NULL,   -- Lý thuyết của chiến lược
    RuleLogic NVARCHAR(MAX) NOT NULL,     -- Quy tắc thực tế để AI áp dụng
    IsAutoGenerated BIT NOT NULL DEFAULT 0, -- 1 = chiến lược tự học từ bài học thực tế
    LearnCount INT NOT NULL DEFAULT 0,      -- Số lần chiến lược được cập nhật từ kinh nghiệm
    WinCount INT NOT NULL DEFAULT 0,        -- Số lần chiến lược dẫn tới kết quả tốt
    LossCount INT NOT NULL DEFAULT 0,       -- Số lần chiến lược dẫn tới kết quả xấu
    LastLearnedAt DATETIME2 NULL            -- Thời điểm học/cập nhật gần nhất
);
GO

-- =====================================================================
-- BƯỚC 3: NẠP DỮ LIỆU NỀN TẢNG (KNOWLEDGE BASE SEED DATA)
-- =====================================================================
INSERT INTO TradingStrategies (Name, StrategyType, Description, RuleLogic)
VALUES 
(
    N'RSI Quá mua / Quá bán (RSI Overbought/Oversold)',
    N'Indicator',
    N'Chỉ số sức mạnh tương đối (RSI) đo lường động lượng giá. RSI dưới 30 báo hiệu quá bán (rẻ), trên 70 báo hiệu quá mua (đắt). Rất hiệu quả khi thị trường đi ngang (Sideways).',
    N'MUA khi RSI vượt từ dưới lên trên mốc 30. BÁN khi RSI vượt từ trên xuống dưới mốc 70 hoặc khi đạt mục tiêu lợi nhuận.'
),
(
    N'MACD Cắt đường tín hiệu (MACD Signal Line Crossover)',
    N'Indicator',
    N'Đường trung bình động hội tụ phân kỳ (MACD) phản ánh xu hướng. Khi MACD cắt lên đường Tín hiệu, nó báo hiệu động lượng tăng giá bắt đầu.',
    N'MUA khi đường MACD cắt lên trên đường Signal Line trong xu hướng tăng (Uptrend). BÁN khi đường MACD cắt xuống dưới đường Signal Line.'
),
(
    N'Mô hình Hai đáy (Double Bottom Pattern)',
    N'ChartPattern',
    N'Mô hình đảo chiều xu hướng giảm sang tăng tạo bởi 2 đáy giá sàn bằng nhau. Sự bứt phá qua đường viền cổ (neckline) là tín hiệu mua mạnh mẽ.',
    N'MUA khi giá đóng cửa vượt lên trên đường viền cổ (Neckline) với khối lượng tăng mạnh.'
),
(
    N'Dải Bollinger co thắt và bứt phá (Bollinger Bands Squeeze & Breakout)',
    N'Indicator',
    N'Bollinger Bands thu hẹp báo hiệu thị trường sắp có biến động bùng nổ xu hướng mới.',
    N'MUA khi giá đóng cửa vượt lên trên Dải trên (Upper Band) sau một chu kỳ Bands co thắt cực đại.'
);
GO

-- =====================================================================
-- LỊCH SỬ CẬP NHẬT CẤU TRÚC (INCREMENTAL DATABASE MIGRATION HISTORY)
-- Nếu bạn ĐÃ TẠO các bảng trước đây và không muốn xóa dữ liệu cũ (không chạy DROP), 
-- bạn chỉ cần chọn và chạy các đoạn lệnh tương ứng dưới đây theo mốc thời gian:
-- =====================================================================

/*
-- [CẬP NHẬT: 2026-06-15 14:30]
-- Bổ sung cột TargetAmount vào bảng cấu hình UserPreferences
ALTER TABLE UserPreferences ADD TargetAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
*/

/*
-- [CẬP NHẬT: 2026-06-15 14:52]
-- Tạo bảng thư viện chiến lược (TradingStrategies) và nạp dữ liệu nền tảng
CREATE TABLE TradingStrategies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(MAX) NOT NULL,          
    StrategyType NVARCHAR(MAX) NOT NULL,  
    Description NVARCHAR(MAX) NOT NULL,   
    RuleLogic NVARCHAR(MAX) NOT NULL,
    IsAutoGenerated BIT NOT NULL DEFAULT 0,
    LearnCount INT NOT NULL DEFAULT 0,
    WinCount INT NOT NULL DEFAULT 0,
    LossCount INT NOT NULL DEFAULT 0,
    LastLearnedAt DATETIME2 NULL
);

INSERT INTO TradingStrategies (Name, StrategyType, Description, RuleLogic)
VALUES 
(N'RSI Quá mua / Quá bán (RSI Overbought/Oversold)', N'Indicator', N'Chỉ số sức mạnh tương đối (RSI) dưới 30 quá bán, trên 70 quá mua.', N'MUA khi RSI vượt mốc 30. BÁN khi RSI thủng mốc 70.'),
(N'MACD Cắt đường tín hiệu (MACD Signal Line Crossover)', N'Indicator', N'MACD hội tụ/phân kỳ chỉ báo động lượng.', N'MUA khi MACD cắt lên Signal Line. BÁN khi cắt xuống.'),
(N'Mô hình Hai đáy (Double Bottom Pattern)', N'ChartPattern', N'Mô hình đảo chiều xu hướng tạo bởi 2 đáy giá sàn.', N'MUA khi giá đóng cửa vượt lên trên đường viền cổ.'),
(N'Dải Bollinger co thắt và bứt phá (Bollinger Bands Squeeze & Breakout)', N'Indicator', N'Co thắt báo hiệu tích lũy và sắp bùng nổ xu hướng.', N'MUA khi giá đóng cửa vượt Dải trên (Upper Band).');
*/

/*
-- [CẬP NHẬT: 2026-06-15 15:12]
-- Bổ sung các cột đăng nhập tài khoản DNSE (DNSE) vào UserPreferences
ALTER TABLE UserPreferences ADD DnseUsername NVARCHAR(MAX) NOT NULL DEFAULT '';
ALTER TABLE UserPreferences ADD DnsePassword NVARCHAR(MAX) NOT NULL DEFAULT '';
ALTER TABLE UserPreferences ADD DnseToken NVARCHAR(MAX) NOT NULL DEFAULT '';
*/

/*
-- [CẬP NHẬT: 2026-06-17 09:55]
-- Bổ sung cột mục tiêu lợi nhuận mong muốn (VND) cho từng vị thế
ALTER TABLE TradePositions ADD TargetProfitAmount DECIMAL(18,4) NULL;
*/

/*
-- [CẬP NHẬT: 2026-06-17 10:13]
-- Bổ sung cột số vốn đã đầu tư vào từng vị thế
ALTER TABLE TradePositions ADD InvestedAmount DECIMAL(18,4) NULL;
*/

/*
-- [CẬP NHẬT: 2026-06-17 11:40]
-- Bổ sung cột hạn mức vốn riêng cho từng vị thế
ALTER TABLE TradePositions ADD BudgetAmount DECIMAL(18,4) NULL;
*/

/*
-- [CẬP NHẬT: 2026-06-17 10:40]
-- Bổ sung cột chốt lời/cắt lỗ cấu hình mặc định cho UserPreferences
ALTER TABLE UserPreferences ADD TakeProfitAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
ALTER TABLE UserPreferences ADD StopLossAmount DECIMAL(18,4) NOT NULL DEFAULT 0;
*/

/*
-- [CẬP NHẬT: 2026-06-17 11:20]
-- Bổ sung metadata tự học cho Knowledge Engine TradingStrategies
ALTER TABLE TradingStrategies ADD IsAutoGenerated BIT NOT NULL DEFAULT 0;
ALTER TABLE TradingStrategies ADD LearnCount INT NOT NULL DEFAULT 0;
ALTER TABLE TradingStrategies ADD WinCount INT NOT NULL DEFAULT 0;
ALTER TABLE TradingStrategies ADD LossCount INT NOT NULL DEFAULT 0;
ALTER TABLE TradingStrategies ADD LastLearnedAt DATETIME2 NULL;
*/

/*
-- [CẬP NHẬT: 2026-06-18 11:15]
-- Bổ sung cột số ngày nắm giữ dự kiến cho từng vị thế
ALTER TABLE TradePositions ADD ExpectedHoldDays INT NULL;
*/

/*
-- [CẬP NHẬT: 2026-06-18 15:25]
-- Bảng chi tiết giao dịch từng lệnh MUA/BÁN riêng lẻ
-- Dùng để AI phân tích timing: bán sớm/muộn → lời/lỗ bao nhiêu
*/
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='StockTransactions' AND xtype='U')
BEGIN
    CREATE TABLE StockTransactions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Symbol NVARCHAR(MAX) NOT NULL,                   -- Mã CP (VD: POW)
        TransactionType NVARCHAR(10) NOT NULL,            -- BUY hoặc SELL
        Quantity INT NOT NULL,                             -- Số lượng giao dịch
        Price DECIMAL(18,4) NOT NULL,                     -- Giá khớp lệnh
        TransactionDate DATETIME2 NOT NULL,               -- Ngày giờ khớp
        Fee DECIMAL(18,4) NULL,                           -- Phí giao dịch
        TotalAmount DECIMAL(18,4) NOT NULL DEFAULT 0,     -- Tổng tiền = Qty × Price
        PnlAmount DECIMAL(18,4) NULL,                     -- Lãi/Lỗ (chỉ cho SELL)
        Source NVARCHAR(50) NOT NULL DEFAULT 'DNSE',      -- DNSE, AI_SIMULATION
        PositionId INT NULL,                              -- FK TradePositions.Id
        Notes NVARCHAR(MAX) NULL,                         -- Ghi chú
        PriceHighSinceBuy DECIMAL(18,4) NULL,             -- Giá cao nhất kỳ nắm giữ
        PriceLowSinceBuy DECIMAL(18,4) NULL,              -- Giá thấp nhất kỳ nắm giữ
        TimingScore DECIMAL(18,4) NULL                    -- Timing Score 0-100% (chỉ SELL)
    );
    PRINT 'Created table StockTransactions';
END;

/*
-- [CẬP NHẬT: 2026-06-25]
-- Bổ sung cột Ngày bắt đầu kế hoạch cho bảng cấu hình UserPreferences
ALTER TABLE UserPreferences ADD PlanStartDate DATETIME2 NULL;
*/
