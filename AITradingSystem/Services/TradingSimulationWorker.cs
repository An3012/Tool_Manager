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
using Microsoft.EntityFrameworkCore;

namespace AITradingSystem.Services
{
    public class TradingSimulationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SimulationLogService _logService;
        private readonly ILogger<TradingSimulationWorker> _logger;
        private readonly Random _random = new();

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
                            // Lấy TẤT CẢ vị thế OPEN (để giám sát cả vị thế thật và AI)
                            var allOpenPositions = await context.TradePositions
                                .Where(p => p.Symbol == symbol && p.Status == "OPEN")
                                .ToListAsync(cancellationToken: stoppingToken);

                            // Lấy chỉ vị thế AI tự học (để AI giao dịch riêng, không đụng vào vị thế thật)
                            var aiOpenPositions = allOpenPositions.Where(p => p.IsAiTrade).ToList();

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

                                    if (pos.IsAiTrade)
                                    {
                                        _logService.AddLog($"[Cảnh báo] Vị thế AI {symbol} {detailReason}. Giá: {newPrice:N0}đ. Tự động bán đóng vị thế.");

                                        // Đóng vị thế
                                        pos.ExitPrice = newPrice;
                                        pos.ExitDate = DateTime.Now;
                                        pos.Status = "CLOSED";
                                        pos.PnL = currentPnL;
                                        context.TradePositions.Update(pos);

                                        // Ghi nhận Order
                                        var order = new Order
                                        {
                                            Symbol = symbol,
                                            OrderType = "SELL",
                                            Quantity = pos.Quantity,
                                            Price = newPrice,
                                            OrderDate = DateTime.Now,
                                            Status = "FILLED",
                                            Rationale = $"Tự động {reason}: {detailReason} trong giả lập."
                                        };
                                        context.Orders.Add(order);
                                        await context.SaveChangesAsync(stoppingToken);

                                        // Rút bài học kinh nghiệm
                                        _logService.AddLog($"[Tự học] Critic Agent tiến hành phân tích và ghi nhớ bài học từ lệnh đóng {symbol}...");
                                        await reflectionService.ReflectOnClosedPositionAsync(pos.Id);
                                    }
                                    else
                                    {
                                        _logService.AddLog($"[CẢNH BÁO TÀI KHOẢN THẬT] Vị thế thật {symbol} {detailReason}! Giá hiện tại: {newPrice:N0}đ, PnL: {currentPnL:N0}đ.");
                                    }
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
                                    var order = new Order
                                    {
                                        Symbol = symbol,
                                        OrderType = "BUY",
                                        Quantity = qty,
                                        Price = newPrice,
                                        OrderDate = DateTime.Now,
                                        Status = "FILLED",
                                        Rationale = $"[Giả lập] Mua theo tín hiệu AI: {analysis.Rationale}"
                                    };
                                    context.Orders.Add(order);

                                    // Tạo vị thế OPEN mới (chỉ cho AI tự học)
                                    var pos = new TradePosition
                                    {
                                        Symbol = symbol,
                                        Quantity = qty,
                                        EntryPrice = newPrice,
                                        EntryDate = DateTime.Now,
                                        Status = "OPEN",
                                        PnL = 0,
                                        IsAiTrade = true,
                                        InvestedAmount = investedAmt,
                                        BudgetAmount = pref.AmountPerTrade
                                    };
                                    context.TradePositions.Add(pos);
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
                                    context.TradePositions.Update(activeAiPos);

                                    var order = new Order
                                    {
                                        Symbol = symbol,
                                        OrderType = "SELL",
                                        Quantity = activeAiPos.Quantity,
                                        Price = newPrice,
                                        OrderDate = DateTime.Now,
                                        Status = "FILLED",
                                        Rationale = $"[Giả lập] Bán theo đề xuất của AI: {analysis.Rationale}"
                                    };
                                    context.Orders.Add(order);
                                    await context.SaveChangesAsync(stoppingToken);

                                    _logService.AddLog($"[Tự học] Khớp lệnh BÁN ảo {symbol}. AI tự rút bài học kinh nghiệm...");
                                    await reflectionService.ReflectOnClosedPositionAsync(activeAiPos.Id);
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
    }
}
