using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AITradingSystem.Data;
using AITradingSystem.Models;
using AITradingSystem.Controllers;
using Microsoft.EntityFrameworkCore;

namespace AITradingSystem.Services
{
    public class TradingSimulationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SimulationLogService _logService;
        private readonly ILogger<TradingSimulationWorker> _logger;
        private readonly Random _random = new();
        private DateTime _lastDnseSyncAndPlanUpdate = DateTime.MinValue;

        public TradingSimulationWorker(
            IServiceProvider serviceProvider,
            SimulationLogService logService,
            ILogger<TradingSimulationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logService = logService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logService.AddLog("[System] Dịch vụ giả lập và tự học ngầm bắt đầu khởi động...");

            // Chờ một chút để hệ thống sẵn sàng hẳn
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var copilotService = scope.ServiceProvider.GetRequiredService<TradingCopilotService>();
                        var reflectionService = scope.ServiceProvider.GetRequiredService<ReflectionService>();
                        var dnseService = scope.ServiceProvider.GetRequiredService<DnseService>();

                        // Đồng bộ DNSE xong lấy dữ liệu lập kế hoạch luôn (định kỳ mỗi 1h)
                        await AutoSyncDnseAndUpdatePlanAsync(context, dnseService, copilotService, stoppingToken);

                        // 1. Tìm danh sách các mã cổ phiếu người dùng đã/đang đầu tư
                        var investedSymbols = await context.TradePositions
                            .Select(p => p.Symbol)
                            .Distinct()
                            .ToListAsync(cancellationToken: stoppingToken);

                        // Nếu chưa có vị thế nào trong DB, sử dụng danh sách mặc định để AI có dữ liệu học
                        if (!investedSymbols.Any())
                        {
                            investedSymbols = new List<string> { "FPT", "SSI", "MWG" };
                        }

                        // 2. Lấy preferences của người dùng
                        var pref = await context.UserPreferences.FirstOrDefaultAsync(cancellationToken: stoppingToken);
                        if (pref == null)
                        {
                            pref = new UserPreference
                            {
                                InvestmentHorizon = "Short-term (T+2.5)",
                                TargetProfitPercentage = 15,
                                MaxLossPercentage = 7,
                                AmountPerTrade = 5000000,
                                TargetAmount = 10000000,
                                RiskTolerance = "Medium"
                            };
                        }

                        _logService.AddLog($"[Học máy] Quét các mã tự học: {string.Join(", ", investedSymbols)}...");

                        // Thử lấy giá thật từ DNSE/VNDirect API trước khi giả lập
                        bool usedRealPrices = false;
                        try
                        {
                            usedRealPrices = await _logService.FetchRealPricesAsync(investedSymbols);
                        }
                        catch (Exception apiEx)
                        {
                            _logger.LogWarning(apiEx, "Không lấy được giá thật, dùng giả lập.");
                        }

                        foreach (var symbol in investedSymbols)
                        {
                            var currentStock = _logService.GetStockState(symbol);
                            if (currentStock == null) continue;

                            decimal newPrice;
                            decimal newChangePct;
                            decimal newRsi;

                            if (usedRealPrices)
                            {
                                // Đã lấy giá thật từ API -> dùng giá hiện tại đã cập nhật
                                newPrice = currentStock.CurrentPrice;
                                newChangePct = currentStock.ChangePercentage;
                                newRsi = currentStock.Rsi;
                                _logService.AddLog($"[DNSE API] {symbol} giá thật: {newPrice:N0} đ ({newChangePct:N2}%, RSI: {newRsi})");
                            }
                            else
                            {
                                // Fallback: Giả lập biến động giá nhẹ (+/- 0.8%) và RSI
                                double pct = (_random.NextDouble() * 1.6 - 0.8) * 0.01;
                                newPrice = currentStock.CurrentPrice * (1 + (decimal)pct);
                                newPrice = Math.Round(newPrice / 100) * 100; // Làm tròn theo bước giá 100đ

                                decimal priceDiffPct = 0;
                                if (currentStock.CurrentPrice > 0)
                                {
                                    priceDiffPct = ((newPrice - currentStock.CurrentPrice) / currentStock.CurrentPrice) * 100;
                                }
                                newChangePct = Math.Round(currentStock.ChangePercentage + priceDiffPct, 2);

                                // Biến động chỉ số RSI tỷ lệ thuận với biến động giá
                                decimal rsiDrift = (decimal)(pct * 100 * (1.2 + _random.NextDouble()));
                                newRsi = Math.Max(10, Math.Min(90, currentStock.Rsi + rsiDrift));
                                newRsi = Math.Round(newRsi, 1);

                                _logService.UpdateStockState(symbol, newPrice, newChangePct, newRsi);
                                _logService.AddLog($"[Học máy] {symbol} giá biến động: {newPrice:N0} đ ({newChangePct:N2}%, RSI: {newRsi})");
                            }

                            // 4. Kiểm tra các vị thế OPEN hiện tại xem có chạm ngưỡng Cắt lỗ / Chốt lời hay không
                            // Lấy TẤT CẢ vị thế OPEN từ bảng TradePositions (vị thế thật của người dùng)
                            var allOpenPositions = await context.TradePositions
                                .Where(p => p.Symbol == symbol && p.Status == "OPEN")
                                .ToListAsync(cancellationToken: stoppingToken);

                            // Lấy chỉ vị thế AI tự học từ bảng AiTradePositions (để AI giao dịch riêng, không đụng vào vị thế thật)
                            var aiOpenPositions = await context.AiTradePositions
                                .Where(p => p.Symbol == symbol && p.Status == "OPEN")
                                .ToListAsync(cancellationToken: stoppingToken);

                            foreach (var pos in allOpenPositions)
                            {
                                decimal currentPnL = (newPrice - pos.EntryPrice) * pos.Quantity;
                                decimal profitPct = (newPrice - pos.EntryPrice) / pos.EntryPrice * 100;

                                bool triggerTakeProfit = false;
                                string tpReason = "";

                                // Ưu tiên 1: Kiểm tra mục tiêu lợi nhuận tổng (TargetProfitAmount)
                                if (pos.TargetProfitAmount.HasValue && pos.TargetProfitAmount.Value > 0 && currentPnL >= pos.TargetProfitAmount.Value)
                                {
                                    triggerTakeProfit = true;
                                    tpReason = $"đạt mục tiêu lợi nhuận {pos.TargetProfitAmount.Value:N0}đ (Lãi thực: {currentPnL:N0}đ)";
                                }
                                // Ưu tiên 2: Kiểm tra giá chốt lời (TakeProfitPrice)
                                else if (pos.TakeProfitPrice.HasValue && pos.TakeProfitPrice.Value > 0)
                                {
                                    triggerTakeProfit = newPrice >= pos.TakeProfitPrice.Value;
                                    tpReason = $"chạm mốc chốt lời {pos.TakeProfitPrice.Value:N0}đ";
                                }
                                // Ưu tiên 3: Fallback mặc định theo %
                                else
                                {
                                    triggerTakeProfit = profitPct >= pref.TargetProfitPercentage;
                                    tpReason = $"đạt {pref.TargetProfitPercentage}% lợi nhuận";
                                }

                                bool triggerStopLoss = false;
                                if (pos.StopLossPrice.HasValue && pos.StopLossPrice.Value > 0)
                                {
                                    triggerStopLoss = newPrice <= pos.StopLossPrice.Value;
                                }
                                else
                                {
                                    triggerStopLoss = profitPct <= -pref.MaxLossPercentage;
                                }

                                if (triggerTakeProfit || triggerStopLoss)
                                {
                                    string reason = triggerTakeProfit ? "Chốt lời" : "Cắt lỗ";
                                    string detailReason = triggerTakeProfit ? tpReason : $"chạm ngưỡng cắt lỗ {(pos.StopLossPrice?.ToString("N0") ?? -pref.MaxLossPercentage + "%")}";

                                    // Không xử lý vị thế thật của người dùng
                                    _logService.AddLog($"[CẢNH BÁO TÀI KHOẢN THẬT] Vị thế thật {symbol} {detailReason}! Giá hiện tại: {newPrice:N0}đ, PnL: {currentPnL:N0}đ.");
                                }
                            }

                            // 5. Yêu cầu AI Trading Copilot phân tích sinh tín hiệu (RAG + Memory)
                            string trend = newChangePct >= 0 ? "Uptrend" : "Sideways";
                            var analysis = await copilotService.AnalyzeAndGenerateSignalAsync(symbol, newPrice, newRsi, trend);

                            if (analysis.Action == "BUY")
                            {
                                // Chỉ kiểm tra vị thế AI để tránh dồn vị thế vô hạn (KHÔNG đụng vào vị thế thật)
                                var hasAiOpen = aiOpenPositions.Any();
                                if (!hasAiOpen)
                                {
                                    var qty = (int)(pref.AmountPerTrade / newPrice);
                                    if (qty == 0) qty = 1;
                                    var investedAmt = qty * newPrice;

                                    // Tạo Order BUY
                                    var order = new AiOrder
                                    {
                                        Symbol = symbol,
                                        OrderType = "BUY",
                                        Quantity = qty,
                                        Price = newPrice,
                                        OrderDate = DateTime.Now,
                                        Status = "FILLED",
                                        Rationale = $"[Giả lập] Mua theo tín hiệu AI: {analysis.Rationale}"
                                    };
                                    context.AiOrders.Add(order);

                                    // Tạo vị thế OPEN mới (chỉ cho AI tự học)
                                    var aiPos = new AiTradePosition
                                    {
                                        Symbol = symbol,
                                        Quantity = qty,
                                        EntryPrice = newPrice,
                                        EntryDate = DateTime.Now,
                                        Status = "OPEN",
                                        PnL = 0,
                                        InvestedAmount = investedAmt,
                                        BudgetAmount = pref.AmountPerTrade
                                    };
                                    context.AiTradePositions.Add(aiPos);
                                    await context.SaveChangesAsync(stoppingToken);

                                    _logService.AddLog($"[Tự học] Khớp lệnh MUA ảo {symbol} số lượng {qty} cổ, vốn {investedAmt:N0}đ. Chiến lược: {analysis.StrategyApplied}");
                                }
                            }
                            else if (analysis.Action == "SELL")
                            {
                                // Bán chỉ vị thế AI tự học (KHÔNG bán vị thế thật của người dùng)
                                var activeAiPos = aiOpenPositions.FirstOrDefault();
                                if (activeAiPos != null)
                                {
                                    activeAiPos.ExitPrice = newPrice;
                                    activeAiPos.ExitDate = DateTime.Now;
                                    activeAiPos.Status = "CLOSED";
                                    activeAiPos.PnL = (newPrice - activeAiPos.EntryPrice) * activeAiPos.Quantity;
                                    context.AiTradePositions.Update(activeAiPos);

                                    var order = new AiOrder
                                    {
                                        Symbol = symbol,
                                        OrderType = "SELL",
                                        Quantity = activeAiPos.Quantity,
                                        Price = newPrice,
                                        OrderDate = DateTime.Now,
                                        Status = "FILLED",
                                        Rationale = $"[Giả lập] Bán theo đề xuất của AI: {analysis.Rationale}"
                                    };
                                    context.AiOrders.Add(order);
                                    await context.SaveChangesAsync(stoppingToken);

                                    _logService.AddLog($"[Tự học] Khớp lệnh BÁN ảo {symbol}. AI tự rút bài học kinh nghiệm...");
                                    await reflectionService.ReflectOnClosedAiPositionAsync(activeAiPos.Id);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong chu kỳ chạy giả lập giao dịch ngầm.");
                    _logService.AddLog($"[Error] Lỗi chu kỳ giả lập: {ex.Message}");
                }

                // Chạy vòng lặp giả lập và tự học mỗi 15 giây
                await Task.Delay(15000, stoppingToken);
            }

            _logService.AddLog("[System] Dịch vụ tự học ngầm đã dừng.");
        }

        private async Task AutoSyncDnseAndUpdatePlanAsync(AppDbContext context, DnseService dnseService, TradingCopilotService copilotService, CancellationToken stoppingToken)
        {
            if (_lastDnseSyncAndPlanUpdate != DateTime.MinValue && (DateTime.Now - _lastDnseSyncAndPlanUpdate).TotalHours < 1)
            {
                return;
            }

            _logService.AddLog("[System] Khởi chạy đồng bộ tài khoản DNSE & cập nhật kế hoạch định kỳ (mỗi 1h)...");
            try
            {
                // 1. Đồng bộ tài khoản DNSE
                try
                {
                    bool success = await dnseService.SyncPortfolioAsync();
                    if (success)
                    {
                        _logService.AddLog("[System] Đồng bộ tài khoản DNSE thành công.");
                    }
                    else
                    {
                        _logService.AddLog("[System] Đồng bộ tài khoản DNSE hoàn tất với trạng thái mô phỏng/không đổi.");
                    }
                }
                catch (Exception dnseEx)
                {
                    _logger.LogError(dnseEx, "Lỗi đồng bộ DNSE định kỳ.");
                    _logService.AddLog($"[Error] Lỗi đồng bộ DNSE: {dnseEx.Message}");
                }

                // 2. Lấy dữ liệu mới nhất từ DB vừa đồng bộ để lập kế hoạch luôn
                var dbPositions = await context.TradePositions.ToListAsync(cancellationToken: stoppingToken);
                var userTransactions = await context.StockTransactions.Where(t => t.Source == "DNSE").ToListAsync(cancellationToken: stoppingToken);
                var allSymbols = dbPositions.Select(p => p.Symbol)
                                          .Concat(userTransactions.Select(t => t.Symbol))
                                          .Distinct()
                                          .ToList();

                // Cập nhật giá thật
                try
                {
                    await _logService.FetchRealPricesAsync(allSymbols);
                }
                catch { }

                var stocks = _logService.GetStockStates();
                var pref = await context.UserPreferences.FirstOrDefaultAsync(cancellationToken: stoppingToken);
                if (pref == null) return;

                var positions = new List<TradePosition>();
                foreach (var symbol in allSymbols)
                {
                    var openPos = dbPositions.FirstOrDefault(p => p.Symbol == symbol && p.Status == "OPEN");
                    if (openPos != null)
                    {
                        positions.Add(openPos);
                    }
                    else
                    {
                        var closedPos = dbPositions.FirstOrDefault(p => p.Symbol == symbol && p.Status == "CLOSED");
                        if (closedPos != null)
                        {
                            var symbolSells = userTransactions.Where(t => t.Symbol == symbol && t.TransactionType == "SELL" && t.PnlAmount.HasValue).ToList();
                            if (symbolSells.Any())
                            {
                                closedPos.PnL = symbolSells.Sum(t => t.PnlAmount.Value);
                            }
                            positions.Add(closedPos);
                        }
                    }
                }

                var closedPositions = positions.Where(p => p.Status == "CLOSED").ToList();
                decimal totalPnL = 0;
                decimal totalRealizedPnL = userTransactions.Any(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue)
                    ? userTransactions.Where(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue).Sum(t => t.PnlAmount.Value)
                    : closedPositions.Sum(p => p.PnL);
                decimal cumulativePnL = totalRealizedPnL;

                var openPositions = positions.Where(p => p.Status == "OPEN").ToList();
                foreach (var pos in openPositions)
                {
                    var currentStock = stocks.FirstOrDefault(s => s.Symbol == pos.Symbol);
                    var currentPrice = currentStock?.CurrentPrice ?? pos.EntryPrice;
                    pos.PnL = (currentPrice - pos.EntryPrice) * pos.Quantity;
                    totalPnL += pos.PnL;
                }
                cumulativePnL += totalPnL;

                decimal totalTargetAmount = 0;
                foreach (var pos in positions)
                {
                    if (pos.TargetProfitAmount.HasValue && pos.TargetProfitAmount.Value > 0)
                    {
                        totalTargetAmount += pos.TargetProfitAmount.Value;
                    }
                }

                // Chuyển đổi StockViewModel
                var stockViewModels = stocks.Select(s => new StockViewModel
                {
                    Symbol = s.Symbol,
                    CompanyName = s.CompanyName,
                    CurrentPrice = s.CurrentPrice,
                    ChangePercentage = s.ChangePercentage,
                    Rsi = s.Rsi,
                    AiSignal = s.AiSignal,
                    Exchange = s.Exchange
                }).ToList();

                var plan = await copilotService.GenerateGlobalPortfolioPlanAsync(openPositions, pref, stockViewModels, cumulativePnL, totalTargetAmount);

                if (plan != null)
                {
                    var targetAmountToUse = pref.TargetAmount;
                    var planStartDate = pref.PlanStartDate ?? DateTime.Today;
                    decimal planRealizedPnL = userTransactions
                        .Where(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue && t.TransactionDate >= planStartDate)
                        .Sum(t => t.PnlAmount.Value);
                    decimal planUnrealizedPnL = positions
                        .Where(p => p.Status == "OPEN" && p.EntryDate >= planStartDate)
                        .Sum(p => p.PnL);
                    decimal planPnL = planRealizedPnL + planUnrealizedPnL;

                    var remainingProfitNeeded = Math.Max(0m, targetAmountToUse - planPnL);
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    var investmentPlan = new InvestmentPlan
                    {
                        RunDate = DateTime.Now,
                        StartDate = plan.StartDate == default ? planStartDate : plan.StartDate,
                        EndDate = plan.EndDate == default ? DateTime.Today : plan.EndDate,
                        Capital = pref.AmountPerTrade,
                        TargetProfit = targetAmountToUse,
                        ActualProfit = planPnL,
                        RemainingProfitNeeded = remainingProfitNeeded,
                        DaysRemainingAtRun = plan.RemainingDays,
                        SuccessProbability = (decimal)plan.SuccessProbability,
                        Status = "Active",
                        DailyCalendarJson = System.Text.Json.JsonSerializer.Serialize(plan, options)
                    };

                    context.InvestmentPlans?.Add(investmentPlan);
                    await context.SaveChangesAsync(stoppingToken);
                    _logService.AddLog($"[System] Cập nhật Kế hoạch đầu tư thành công từ dữ liệu DNSE đồng bộ! Xác suất mới: {plan.SuccessProbability}%");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi cập nhật kế hoạch từ DNSE.");
                _logService.AddLog($"[Error] Lỗi lập kế hoạch sau đồng bộ DNSE: {ex.Message}");
            }
            finally
            {
                _lastDnseSyncAndPlanUpdate = DateTime.Now;
            }
        }
    }
}
