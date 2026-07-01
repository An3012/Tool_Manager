using System.Diagnostics;
using AITradingSystem.Data;
using AITradingSystem.Models;
using AITradingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AITradingSystem.Controllers
{
    public class CopilotController : Controller
    {
        private readonly AppDbContext _context;
        private readonly DnseService _dnseService;
        private readonly TradingCopilotService _copilotService;
        private readonly ReflectionService _reflectionService;
        private readonly SimulationLogService _simulationLogService;

        public CopilotController(
            AppDbContext context,
            DnseService dnseService,
            TradingCopilotService copilotService,
            ReflectionService reflectionService,
            SimulationLogService simulationLogService)
        {
            _context = context;
            _dnseService = dnseService;
            _copilotService = copilotService;
            _reflectionService = reflectionService;
            _simulationLogService = simulationLogService;
        }

        // Endpoint đồng bộ thủ công từ UI
        [HttpPost]
        public async Task<IActionResult> SyncDnse()
        {
            var success = await _dnseService.SyncPortfolioAsync();
            if (success)
            {
                TempData["SuccessMessage"] = "Đồng bộ danh mục thực tế từ DNSE/DNSE thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Đồng bộ thất bại. Vui lòng kiểm tra cấu hình API Key của DNSE.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> AddWatchlistSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                TempData["ErrorMessage"] = "Mã cổ phiếu không hợp lệ.";
                return RedirectToAction(nameof(Index));
            }

            symbol = symbol.Trim().ToUpper();

            // Thử lấy giá thật từ API trước
            try
            {
                await _simulationLogService.FetchRealPricesAsync(new List<string> { symbol });
            }
            catch
            {
                // Bỏ qua lỗi kết nối mạng/API
            }

            // Nếu mã chưa tồn tại trong danh sách thì tự động tạo mới
            var stock = _simulationLogService.GetStockState(symbol);
            if (stock == null)
            {
                _simulationLogService.UpdateOrAddStockState(symbol, 20000m, 0m, 50m, $"CTCP {symbol}");
                TempData["SuccessMessage"] = $"Đã thêm thành công mã {symbol} vào Watchlist!";
            }
            else
            {
                TempData["SuccessMessage"] = $"Mã {symbol} đã sẵn có trong Watchlist.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetPlanDetails(int id)
        {
            var plan = await _context.InvestmentPlans.FindAsync(id);
            if (plan == null) return NotFound();

            return Json(new
            {
                id = plan.Id,
                runDate = plan.RunDate.ToString("dd/MM/yyyy HH:mm"),
                startDate = plan.StartDate.ToString("dd/MM/yyyy"),
                endDate = plan.EndDate.ToString("dd/MM/yyyy"),
                capital = plan.Capital,
                targetProfit = plan.TargetProfit,
                actualProfit = plan.ActualProfit,
                remainingProfitNeeded = plan.RemainingProfitNeeded,
                daysRemainingAtRun = plan.DaysRemainingAtRun,
                successProbability = plan.SuccessProbability,
                status = plan.Status,
                aiVersion = "v1.2",
                dailyCalendarJson = plan.DailyCalendarJson
            });
        }

        public async Task<IActionResult> Index()
        {
            // Cập nhật giá thật cho các mã trong nền (không chặn tải trang)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _simulationLogService.FetchRealPricesAsync(null);
                }
                catch { }
            });

            var preference = await GetUserPreference();
            var strategies = await _context.TradingStrategies.ToListAsync();
            var stocks = GetDNSEStocks();
            var positions = await _context.TradePositions.Where(p => p.Status == "OPEN").ToListAsync();

            // Tính tổng PnL thực tế cho tất cả vị thế đang mở
            decimal totalPnL = 0;
            foreach (var pos in positions)
            {
                var currentPrice = stocks.FirstOrDefault(s => s.Symbol == pos.Symbol)?.CurrentPrice ?? pos.EntryPrice;
                pos.PnL = (currentPrice - pos.EntryPrice) * pos.Quantity;
                totalPnL += pos.PnL;
            }

            // Lấy danh sách bài học kinh nghiệm từ Memory Engine
            var episodes = await _context.TradeEpisodes
                .OrderByDescending(e => e.Timestamp)
                .Take(20)
                .ToListAsync();

            ViewBag.Preference = preference;
            ViewBag.Stocks = stocks;
            ViewBag.Strategies = strategies;
            ViewBag.Positions = positions;
            ViewBag.TotalPnL = totalPnL;
            ViewBag.ThoughtLogs = _simulationLogService.GetLogs();
            ViewBag.Episodes = episodes;

            return View();
        }

        // MÀN HÌNH MỚI: Danh mục cổ phiếu đã mua (Portfolio)
        public async Task<IActionResult> Portfolio()
        {
            // 1. Get all positions from DB
            var dbPositions = await _context.TradePositions.ToListAsync();

            // 2. Get all DNSE transactions
            var userTransactions = await _context.StockTransactions.Where(t => t.Source == "DNSE").ToListAsync();

            // 3. Find all unique symbols that have either a position or transactions
            var allSymbols = dbPositions.Select(p => p.Symbol)
                                      .Concat(userTransactions.Select(t => t.Symbol))
                                      .Distinct()
                                      .ToList();

            // Cập nhật giá thật cho các mã trong nền (không chặn tải trang)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _simulationLogService.FetchRealPricesAsync(allSymbols);
                }
                catch { }
            });

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
                    else
                    {
                        // Create a virtual closed position to show the transaction history
                        var symbolSells = userTransactions.Where(t => t.Symbol == symbol && t.TransactionType == "SELL" && t.PnlAmount.HasValue).ToList();
                        decimal pnl = symbolSells.Sum(t => t.PnlAmount.Value);

                        var entryPrice = 0m;
                        var firstBuy = userTransactions.OrderBy(t => t.TransactionDate).FirstOrDefault(t => t.Symbol == symbol && t.TransactionType == "BUY");
                        if (firstBuy != null)
                        {
                            entryPrice = firstBuy.Price;
                        }

                        var virtualPos = new TradePosition
                        {
                            Id = -Math.Abs(symbol.GetHashCode()), // Unique negative ID
                            Symbol = symbol,
                            Quantity = 0,
                            EntryPrice = entryPrice,
                            EntryDate = firstBuy?.TransactionDate ?? DateTime.Now,
                            Status = "CLOSED",
                            PnL = pnl
                        };
                        positions.Add(virtualPos);
                    }
                }
            }

            // Sort so OPEN positions are first
            positions = positions
                .OrderByDescending(p => p.Status == "OPEN")
                .ThenByDescending(p => p.EntryDate)
                .ToList();

            var closedPositions = positions.Where(p => p.Status == "CLOSED").ToList();
            var stocks = GetDNSEStocks();
            var pref = await GetUserPreference();
            decimal totalPnL = 0;
            decimal totalTargetAmount = 0;
            decimal allocatedBudget = 0;
            decimal totalRealizedPnL = userTransactions.Any(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue)
                ? userTransactions.Where(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue).Sum(t => t.PnlAmount.Value)
                : closedPositions.Sum(p => p.PnL);
            decimal cumulativePnL = totalRealizedPnL;
            var analysisMap = new Dictionary<int, PositionAnalysis>();

            foreach (var pos in positions)
            {
                var currentPrice = stocks.FirstOrDefault(s => s.Symbol == pos.Symbol)?.CurrentPrice ?? pos.EntryPrice;
                var investedAmount = pos.InvestedAmount.HasValue && pos.InvestedAmount.Value > 0
                    ? pos.InvestedAmount.Value
                    : pos.EntryPrice * pos.Quantity;
                var positionBudget = pos.BudgetAmount.HasValue && pos.BudgetAmount.Value > 0
                    ? pos.BudgetAmount.Value
                    : pref.AmountPerTrade;
                if (pos.Status == "OPEN")
                {
                    pos.PnL = (currentPrice - pos.EntryPrice) * pos.Quantity;
                    totalPnL += pos.PnL;
                }

                // Tính toán phân tích mục tiêu cho từng vị thế
                var analysis = new PositionAnalysis
                {
                    FixedBudget = positionBudget,
                    InvestedAmount = investedAmount,
                    RemainingBudget = positionBudget > 0 ? Math.Max(0m, positionBudget - investedAmount) : 0m
                };
                analysis.CurrentPnL = pos.PnL;
                analysis.PnlPercent = pos.EntryPrice > 0 ? (currentPrice - pos.EntryPrice) / pos.EntryPrice * 100 : 0;
                analysis.CanBuyMore = analysis.RemainingBudget > 0;
                analysis.SuggestedAddAmount = analysis.CanBuyMore
                    ? Math.Min(analysis.RemainingBudget, positionBudget > 0 ? positionBudget : analysis.RemainingBudget)
                    : 0m;
                analysis.SuggestedAddQuantity = currentPrice > 0
                    ? (int)Math.Floor(analysis.SuggestedAddAmount / currentPrice)
                    : 0;
                analysis.CanPartialSell = pos.Quantity > 1 && pos.PnL > 0;
                analysis.SuggestedSellQuantity = analysis.CanPartialSell
                    ? Math.Max(1, (int)Math.Floor(pos.Quantity * 0.6m))
                    : 0;
                if (analysis.SuggestedSellQuantity >= pos.Quantity)
                {
                    analysis.SuggestedSellQuantity = Math.Max(1, pos.Quantity - 1);
                }
                analysis.SuggestedSellAmount = analysis.SuggestedSellQuantity > 0
                    ? analysis.SuggestedSellQuantity * currentPrice
                    : 0m;
                analysis.SuggestedSellProfit = analysis.SuggestedSellQuantity > 0
                    ? (currentPrice - pos.EntryPrice) * analysis.SuggestedSellQuantity
                    : 0m;

                // Tính toán số ngày nắm giữ
                var daysHeld = (DateTime.Now.Date - pos.EntryDate.Date).Days;
                if (daysHeld < 0) daysHeld = 0;
                analysis.DaysHeld = daysHeld;

                if (pos.ExpectedHoldDays.HasValue && pos.ExpectedHoldDays.Value > 0)
                {
                    analysis.ExpectedHoldDays = pos.ExpectedHoldDays.Value;
                    analysis.RemainingDays = pos.ExpectedHoldDays.Value - daysHeld;
                }

                if (pos.TargetProfitAmount.HasValue && pos.TargetProfitAmount.Value > 0)
                {
                    analysis.TargetAmount = pos.TargetProfitAmount.Value;
                    analysis.ProgressPercent = Math.Min(100, Math.Max(-100, pos.PnL / pos.TargetProfitAmount.Value * 100));
                    analysis.RemainingAmount = pos.TargetProfitAmount.Value - pos.PnL;
                    analysis.AutoTpPrice = pos.EntryPrice + (pos.TargetProfitAmount.Value / pos.Quantity);
                    totalTargetAmount += pos.TargetProfitAmount.Value;

                    // AI tự động gợi ý hành động dựa trên tiến độ mục tiêu VÀ thời gian đầu tư
                    if (pos.PnL >= pos.TargetProfitAmount.Value)
                    {
                        analysis.AiSignal = "SELL";
                        analysis.AiReason = $"🎯 ĐÃ ĐẠT MỤC TIÊU! Lãi hiện tại {pos.PnL:N0}đ ≥ Mục tiêu {pos.TargetProfitAmount.Value:N0}đ. Nên chốt lời bảo toàn thành quả.";
                    }
                    else if (analysis.RemainingDays.HasValue && analysis.RemainingDays.Value <= 0)
                    {
                        // Đã quá hạn nắm giữ dự kiến
                        if (pos.PnL < 0)
                        {
                            analysis.AiSignal = "SELL";
                            analysis.AiReason = $"⚠️ ĐÃ QUÁ HẠN ĐẦU TƯ ({daysHeld}/{pos.ExpectedHoldDays.Value} ngày) và đang lỗ {Math.Abs(pos.PnL):N0}đ. Nên dứt khoát bán cắt lỗ để giải phóng nguồn vốn.";
                        }
                        else
                        {
                            analysis.AiSignal = "SELL";
                            analysis.AiReason = $"🎯 ĐÃ QUÁ HẠN ĐẦU TƯ ({daysHeld}/{pos.ExpectedHoldDays.Value} ngày) và đang có lãi {pos.PnL:N0}đ. Nên bán để đóng vị thế, thực hiện xoay vòng vốn.";
                        }
                    }
                    else if (analysis.ProgressPercent >= 80)
                    {
                        analysis.AiSignal = "WATCH";
                        analysis.AiReason = $"⚡ Sắp đạt mục tiêu ({analysis.ProgressPercent:N1}%). Còn thiếu {analysis.RemainingAmount:N0}đ. Theo dõi sát để chốt lời đúng lúc.";
                    }
                    else if (analysis.RemainingDays.HasValue && (analysis.RemainingDays.Value == 1 || analysis.RemainingDays.Value == 2))
                    {
                        // Gần đến hạn nắm giữ dự kiến
                        if (pos.PnL < 0)
                        {
                            analysis.AiSignal = "CAUTION";
                            analysis.AiReason = $"⏳ Sắp đến hạn đầu tư (Còn {analysis.RemainingDays.Value} ngày) nhưng đang lỗ {Math.Abs(pos.PnL):N0}đ. Cân nhắc cắt lỗ sớm nếu xu hướng không cải thiện.";
                        }
                        else
                        {
                            analysis.AiSignal = "WATCH";
                            analysis.AiReason = $"⏳ Sắp đến hạn đầu tư (Còn {analysis.RemainingDays.Value} ngày) và đang lãi {pos.PnL:N0}đ. Chuẩn bị bán chốt lời.";
                        }
                    }
                    else if (analysis.CanPartialSell && analysis.RemainingBudget > 0)
                    {
                        analysis.AiSignal = "PARTIAL";
                        analysis.AiReason = $"Đang có lãi {pos.PnL:N0}đ. Có thể bán bớt {analysis.SuggestedSellQuantity:N0} CP để khóa khoảng {analysis.SuggestedSellProfit:N0}đ, giữ lại {pos.Quantity - analysis.SuggestedSellQuantity:N0} CP và chờ mua lại khi giá điều chỉnh.";
                    }
                    else if (analysis.ProgressPercent >= 50)
                    {
                        analysis.AiSignal = "HOLD";
                        analysis.AiReason = $"📈 Đang tiến triển tốt ({analysis.ProgressPercent:N1}%). Giữ vị thế và chờ đạt mục tiêu {pos.TargetProfitAmount.Value:N0}đ.";
                    }
                    else if (pos.PnL < 0)
                    {
                        analysis.AiSignal = "CAUTION";
                        analysis.AiReason = $"⚠️ Đang lỗ {Math.Abs(pos.PnL):N0}đ. Kiểm tra lại mục tiêu hoặc cân nhắc cắt lỗ nếu xu hướng xấu.";
                    }
                    else
                    {
                        analysis.AiSignal = "HOLD";
                        analysis.AiReason = $"📊 Tiến độ {analysis.ProgressPercent:N1}%. Cần thêm {analysis.RemainingAmount:N0}đ để đạt mục tiêu. Kiên nhẫn giữ lệnh.";
                    }

                    // Bổ sung thông tin thời gian nắm giữ vào cuối Rationale (nếu còn hạn và không ở trạng thái báo động thời gian)
                    if (pos.ExpectedHoldDays.HasValue && pos.ExpectedHoldDays.Value > 0 && analysis.RemainingDays.Value > 2)
                    {
                        analysis.AiReason += $" (⏳ Đã giữ {daysHeld}/{pos.ExpectedHoldDays.Value} ngày, còn {analysis.RemainingDays.Value} ngày)";
                    }
                }
                else
                {
                    // Không có mục tiêu riêng -> dùng chỉ báo kỹ thuật mặc định
                    analysis.AiSignal = "NONE";
                    analysis.AiReason = "Chưa đặt mục tiêu lợi nhuận. Hãy nhập số tiền muốn kiếm được để AI phân tích.";
                    if (pos.ExpectedHoldDays.HasValue && pos.ExpectedHoldDays.Value > 0)
                    {
                        analysis.AiReason += $" [Thời gian nắm giữ: Đã giữ {daysHeld}/{pos.ExpectedHoldDays.Value} ngày (còn {analysis.RemainingDays ?? 0} ngày)]";
                    }
                }

                analysis.SymbolRealizedPnL = userTransactions
                    .Where(t => t.Symbol == pos.Symbol && t.TransactionType == "SELL" && t.PnlAmount.HasValue)
                    .Sum(t => t.PnlAmount.Value);
                analysis.SymbolCumulativePnL = analysis.SymbolRealizedPnL;

                analysisMap[pos.Id] = analysis;
                if (pos.Status == "OPEN")
                {
                    allocatedBudget += analysis.InvestedAmount;
                }
            }

            // cumulativePnL += totalPnL;
            decimal remainingOtherBudget = Math.Max(0m, pref.AmountPerTrade - allocatedBudget);

            DateTime planStartDate = pref.PlanStartDate ?? DateTime.Today;
            decimal planRealizedPnL = userTransactions
                .Where(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue && t.TransactionDate >= planStartDate)
                .Sum(t => t.PnlAmount.Value);
            decimal planUnrealizedPnL = positions
                .Where(p => p.Status == "OPEN" && p.EntryDate >= planStartDate)
                .Sum(p => p.PnL);
            decimal planPnL = planRealizedPnL;

            ViewBag.PlanPnL = planPnL;
            ViewBag.Positions = positions;
            ViewBag.ClosedPositions = closedPositions;
            ViewBag.Stocks = stocks;
            ViewBag.Preference = pref;
            ViewBag.TotalPnL = totalPnL;
            ViewBag.TotalTargetAmount = totalTargetAmount;
            ViewBag.AllocatedBudget = allocatedBudget;
            ViewBag.RemainingOtherBudget = remainingOtherBudget;
            ViewBag.TotalRealizedPnL = totalRealizedPnL;
            ViewBag.CumulativePnL = cumulativePnL;
            ViewBag.AnalysisMap = analysisMap;

            var transactions = await _context.StockTransactions.Where(t => t.Source == "DNSE").ToListAsync();
            ViewBag.Transactions = transactions;

            // Load plan history and latest active plan for persistence on page load
            var pastPlans = await _context.InvestmentPlans
                .OrderByDescending(p => p.RunDate)
                .ToListAsync();
            ViewBag.HistoryPlans = pastPlans;

            var latestPlan = pastPlans.FirstOrDefault(p => p.Status == "Active") ?? pastPlans.FirstOrDefault();
            if (latestPlan != null)
            {
                ViewBag.LatestPlanDb = latestPlan;
                if (!string.IsNullOrEmpty(latestPlan.DailyCalendarJson))
                {
                    try
                    {
                        var options = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString,
                            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                        };
                        var planResult = System.Text.Json.JsonSerializer.Deserialize<AITradingSystem.Services.GlobalPortfolioPlanResult>(latestPlan.DailyCalendarJson, options);
                        
                        if (latestPlan.Status == "Active" && planResult != null)
                        {
                            // 1. Sync actual actions in calendar
                            PopulateActualActions(planResult.DailyCalendar, userTransactions);
                            
                            // 2. Sync actual profit & remaining profit
                            latestPlan.ActualProfit = planPnL;
                            latestPlan.RemainingProfitNeeded = Math.Max(0m, latestPlan.TargetProfit - planPnL);
                            
                            // 3. Sync remaining days
                            int daysLeft = (latestPlan.EndDate.Date - DateTime.Today).Days;
                            latestPlan.DaysRemainingAtRun = daysLeft >= 0 ? daysLeft : 0;
                            
                            // 4. Update DailyCalendarJson
                            latestPlan.DailyCalendarJson = System.Text.Json.JsonSerializer.Serialize(planResult, options);
                            
                            _context.InvestmentPlans.Update(latestPlan);
                            await _context.SaveChangesAsync();
                        }
                        
                        ViewBag.GlobalPlan = planResult;
                    }
                    catch (Exception)
                    {
                        // Ignore deserialization exceptions
                    }
                }
            }

            return View("Portfolio");
        }

        [HttpPost]
        public async Task<IActionResult> RunPlanAnalysis(int? prefId)
        {
            // 1. Get all positions from DB
            var dbPositions = await _context.TradePositions.ToListAsync();

            // 2. Get all DNSE transactions
            var userTransactions = await _context.StockTransactions.Where(t => t.Source == "DNSE").ToListAsync();

            // 3. Find all unique symbols that have either a position or transactions
            var allSymbols = dbPositions.Select(p => p.Symbol)
                                      .Concat(userTransactions.Select(t => t.Symbol))
                                      .Distinct()
                                      .ToList();

            // Cập nhật giá thật cho các mã liên quan
            try
            {
                await _simulationLogService.FetchRealPricesAsync(allSymbols);
            }
            catch { }

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
                    else
                    {
                        // Create a virtual closed position to show the transaction history
                        var symbolSells = userTransactions.Where(t => t.Symbol == symbol && t.TransactionType == "SELL" && t.PnlAmount.HasValue).ToList();
                        decimal pnl = symbolSells.Sum(t => t.PnlAmount.Value);

                        var entryPrice = 0m;
                        var firstBuy = userTransactions.OrderBy(t => t.TransactionDate).FirstOrDefault(t => t.Symbol == symbol && t.TransactionType == "BUY");
                        if (firstBuy != null)
                        {
                            entryPrice = firstBuy.Price;
                        }

                        var virtualPos = new TradePosition
                        {
                            Id = -Math.Abs(symbol.GetHashCode()), // Unique negative ID
                            Symbol = symbol,
                            Quantity = 0,
                            EntryPrice = entryPrice,
                            EntryDate = firstBuy?.TransactionDate ?? DateTime.Now,
                            Status = "CLOSED",
                            PnL = pnl
                        };
                        positions.Add(virtualPos);
                    }
                }
            }

            // Sort so OPEN positions are first
            positions = positions
                .OrderByDescending(p => p.Status == "OPEN")
                .ThenByDescending(p => p.EntryDate)
                .ToList();

            var closedPositions = positions.Where(p => p.Status == "CLOSED").ToList();
            var stocks = GetDNSEStocks();
            UserPreference pref;
            if (prefId.HasValue)
            {
                pref = await _context.UserPreferences.FindAsync(prefId.Value) ?? await GetUserPreference();
            }
            else
            {
                pref = await GetUserPreference();
            }

            decimal totalPnL = 0;
            decimal totalRealizedPnL = userTransactions.Any(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue)
                ? userTransactions.Where(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue).Sum(t => t.PnlAmount.Value)
                : closedPositions.Sum(p => p.PnL);
            decimal cumulativePnL = totalRealizedPnL;

            var openPositions = positions.Where(p => p.Status == "OPEN").ToList();
            foreach (var pos in openPositions)
            {
                var currentPrice = stocks.FirstOrDefault(s => s.Symbol == pos.Symbol)?.CurrentPrice ?? pos.EntryPrice;
                pos.PnL = (currentPrice - pos.EntryPrice) * pos.Quantity;
                totalPnL += pos.PnL;
            }
            // cumulativePnL += totalPnL;

            decimal totalTargetAmount = 0;
            foreach (var pos in positions)
            {
                if (pos.TargetProfitAmount.HasValue && pos.TargetProfitAmount.Value > 0)
                {
                    totalTargetAmount += pos.TargetProfitAmount.Value;
                }
            }

            var planStartDate = pref.PlanStartDate ?? DateTime.Today;
            decimal planRealizedPnL = userTransactions
                .Where(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue && t.TransactionDate >= planStartDate)
                .Sum(t => t.PnlAmount.Value);
            decimal planPnL = planRealizedPnL;

            var plan = await _copilotService.GenerateGlobalPortfolioPlanAsync(openPositions, pref, stocks, planPnL, totalTargetAmount);

            if (plan != null)
            {
                if (plan.DailyCalendar != null)
                {
                    PopulateActualActions(plan.DailyCalendar, await _context.StockTransactions.Where(t => t.Source == "DNSE").ToListAsync());
                }

                var targetAmountToUse = pref.TargetAmount;
                decimal planUnrealizedPnL = positions
                    .Where(p => p.Status == "OPEN" && p.EntryDate >= planStartDate)
                    .Sum(p => p.PnL);

                var remainingProfitNeeded = Math.Max(0m, targetAmountToUse - planPnL);
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var activePlan = await _context.InvestmentPlans
                    .FirstOrDefaultAsync(p => p.Status == "Active");

                if (activePlan != null)
                {
                    activePlan.RunDate = DateTime.Now;
                    activePlan.StartDate = plan.StartDate == default ? (pref.PlanStartDate ?? DateTime.Today) : plan.StartDate;
                    activePlan.EndDate = plan.EndDate == default ? DateTime.Today : plan.EndDate;
                    activePlan.Capital = pref.AmountPerTrade;
                    activePlan.TargetProfit = targetAmountToUse;
                    activePlan.ActualProfit = planPnL;
                    activePlan.RemainingProfitNeeded = remainingProfitNeeded;
                    activePlan.DaysRemainingAtRun = plan.RemainingDays;
                    activePlan.SuccessProbability = (decimal)plan.SuccessProbability;
                    activePlan.DailyCalendarJson = System.Text.Json.JsonSerializer.Serialize(plan, options);

                    _context.InvestmentPlans.Update(activePlan);
                    await _context.SaveChangesAsync();
                    TempData["LatestPlanId"] = activePlan.Id;
                }
                else
                {
                    var investmentPlan = new InvestmentPlan
                    {
                        RunDate = DateTime.Now,
                        StartDate = plan.StartDate == default ? (pref.PlanStartDate ?? DateTime.Today) : plan.StartDate,
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

                    _context.InvestmentPlans.Add(investmentPlan);
                    await _context.SaveChangesAsync();
                    TempData["LatestPlanId"] = investmentPlan.Id;
                }
            }

            // Populate history for the partial view
            ViewBag.HistoryPlans = await _context.InvestmentPlans
                .OrderByDescending(p => p.RunDate)
                .ToListAsync();

            ViewBag.GlobalPlan = plan;
            ViewBag.Preference = pref;

            return PartialView("_AiPlanPartial");
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeletePlan(int id)
        {
            if (_context.InvestmentPlans == null)
            {
                return NotFound(new { success = false, message = "Không có kết nối CSDL." });
            }
            var plan = await _context.InvestmentPlans.FindAsync(id);
            if (plan == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy kế hoạch." });
            }

            plan.Status = "Cancelled";
            _context.InvestmentPlans.Update(plan);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã chuyển kế hoạch sang trạng thái hủy thành công." });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ActivatePlan(int id)
        {
            if (_context.InvestmentPlans == null)
            {
                return NotFound(new { success = false, message = "Không có kết nối CSDL." });
            }
            var plan = await _context.InvestmentPlans.FindAsync(id);
            if (plan == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy kế hoạch." });
            }

            // Set other Active plans to Expired
            var activePlans = await _context.InvestmentPlans
                .Where(p => p.Status == "Active")
                .ToListAsync();
            foreach (var p in activePlans)
            {
                p.Status = "Expired";
            }

            plan.Status = "Active";
            _context.InvestmentPlans.Update(plan);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Kích hoạt kế hoạch thành công." });
        }

        private void PopulateActualActions(List<AITradingSystem.Models.DailyCalendarItem> calendar, List<StockTransaction> transactions)
        {
            foreach (var item in calendar)
            {
                if (DateTime.TryParseExact(item.Date, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    var todaysTx = transactions.Where(t => t.TransactionDate.Date == parsedDate.Date).ToList();
                    if (todaysTx.Any())
                    {
                        var actions = todaysTx.Select(t => $"{(t.TransactionType == "BUY" ? "MUA" : "BÁN")} {t.Symbol} ({t.Quantity} CP @ {t.Price:N0})").ToList();
                        item.ActualAction = string.Join("<br/>", actions);
                    }
                    else
                    {
                        if (parsedDate.Date <= DateTime.Today.Date)
                        {
                            item.ActualAction = "Không có giao dịch từ người dùng";
                        }
                        else
                        {
                            item.ActualAction = "-";
                        }
                    }
                }
            }
        }

        // MÀN HÌNH MỚI: Liên kết tài khoản & Cấu hình rủi ro (Settings)

        public async Task<IActionResult> Account()
        {
            var preferences = await _context.UserPreferences.ToListAsync();
            if (!preferences.Any())
            {
                var defaultPref = new UserPreference
                {
                    InvestmentHorizon = "Short-term (T+2.5)",
                    TargetProfitPercentage = 15,
                    MaxLossPercentage = 7,
                    AmountPerTrade = 5000000,
                    TargetAmount = 10000000,
                    RiskTolerance = "Medium"
                };
                _context.UserPreferences.Add(defaultPref);
                await _context.SaveChangesAsync();
                preferences.Add(defaultPref);
            }
            ViewBag.PreferencesList = preferences;
            ViewBag.Preference = preferences.FirstOrDefault(p => !string.IsNullOrEmpty(p.DnseUsername)) ?? preferences.First();
            return View();
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CompletePlan(int id)
        {
            if (_context.InvestmentPlans == null)
            {
                return NotFound(new { success = false, message = "Không có kết nối CSDL." });
            }
            var plan = await _context.InvestmentPlans.FindAsync(id);
            if (plan == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy kế hoạch." });
            }

            plan.Status = "Success";
            _context.InvestmentPlans.Update(plan);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã đánh dấu hoàn thành kế hoạch thành công." });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeletePreference(int id)
        {
            if (_context.UserPreferences == null)
            {
                return NotFound(new { success = false, message = "Không có kết nối CSDL." });
            }
            var pref = await _context.UserPreferences.FindAsync(id);
            if (pref == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy cấu hình." });
            }

            // Don't delete the last one to prevent blank state
            var count = await _context.UserPreferences.CountAsync();
            if (count <= 1)
            {
                return BadRequest(new { success = false, message = "Không thể xóa cấu hình cuối cùng." });
            }

            _context.UserPreferences.Remove(pref);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Xóa cấu hình mục tiêu thành công." });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRisk(int id, decimal? takeProfit, decimal? stopLoss, decimal? targetProfitAmount, decimal? budgetAmount, int? expectedHoldDays)
        {
            var position = await _context.TradePositions.FindAsync(id);
            if (position != null)
            {
                position.TakeProfitPrice = takeProfit;
                position.StopLossPrice = stopLoss;
                position.TargetProfitAmount = targetProfitAmount;
                position.BudgetAmount = budgetAmount;
                position.ExpectedHoldDays = expectedHoldDays;

                // Tự động tính giá chốt lời từ mục tiêu lợi nhuận nếu người dùng đặt mục tiêu nhưng chưa đặt TP
                if (targetProfitAmount.HasValue && targetProfitAmount.Value > 0 && (!takeProfit.HasValue || takeProfit.Value == 0))
                {
                    position.TakeProfitPrice = position.EntryPrice + (targetProfitAmount.Value / position.Quantity);
                }

                _context.TradePositions.Update(position);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật mục tiêu và cài đặt rủi ro cho mã {position.Symbol}!";
            }
            return RedirectToAction(nameof(Portfolio));
        }

        [HttpPost]
        public async Task<IActionResult> AddMorePosition(int id)
        {
            var position = await _context.TradePositions.FindAsync(id);
            if (position == null) return NotFound();

            var pref = await GetUserPreference();
            var stocks = GetDNSEStocks();
            var currentPrice = stocks.FirstOrDefault(s => s.Symbol == position.Symbol)?.CurrentPrice ?? position.EntryPrice;
            var fixedBudget = position.BudgetAmount.HasValue && position.BudgetAmount.Value > 0
                ? position.BudgetAmount.Value
                : pref.AmountPerTrade;
            var existingInvested = position.InvestedAmount.HasValue && position.InvestedAmount.Value > 0
                ? position.InvestedAmount.Value
                : position.EntryPrice * position.Quantity;
            var remainingBudget = fixedBudget > 0 ? Math.Max(0m, fixedBudget - existingInvested) : 0m;

            if (remainingBudget < currentPrice)
            {
                TempData["ErrorMessage"] = $"Không đủ hạn mức còn lại để mua thêm 1 CP {position.Symbol}. Còn {remainingBudget:N0}đ, giá hiện tại là {currentPrice:N0}đ.";
                return RedirectToAction(nameof(Portfolio));
            }

            var fixedInvestAmount = Math.Min(remainingBudget, fixedBudget > 0 ? fixedBudget : remainingBudget);
            var addQuantity = (int)Math.Floor(fixedInvestAmount / currentPrice);
            if (addQuantity <= 0)
            {
                TempData["ErrorMessage"] = $"Phần vốn còn lại {remainingBudget:N0}đ chưa đủ để mua thêm 1 CP {position.Symbol}.";
                return RedirectToAction(nameof(Portfolio));
            }

            var addedAmount = addQuantity * currentPrice;

            position.Quantity += addQuantity;
            position.InvestedAmount = existingInvested + addedAmount;
            position.EntryPrice = position.InvestedAmount.Value / position.Quantity;

            if (position.TargetProfitAmount.HasValue && position.TargetProfitAmount.Value > 0)
            {
                position.TakeProfitPrice = position.EntryPrice + (position.TargetProfitAmount.Value / position.Quantity);
            }

            _context.TradePositions.Update(position);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã mua thêm {addQuantity:N0} CP {position.Symbol} từ nguồn cố định {fixedInvestAmount:N0}đ.";
            return RedirectToAction(nameof(Portfolio));
        }

        private async Task<UserPreference> GetUserPreference()
        {
            var preference = await _context.UserPreferences.FirstOrDefaultAsync();
            if (preference == null)
            {
                preference = new UserPreference
                {
                    InvestmentHorizon = "3",
                    TargetProfitPercentage = 15,
                    MaxLossPercentage = 7,
                    AmountPerTrade = 5000000,
                    TargetAmount = 10000000,
                    TakeProfitAmount = 2000000,
                    StopLossAmount = 1000000,
                    RiskTolerance = "Medium"
                };
            }
            return preference;
        }

        // Cập nhật cấu hình của người dùng
        [HttpPost]
        public async Task<IActionResult> SavePreferences(UserPreference model)
        {
            if (ModelState.IsValid)
            {
                var existingDnse = await _context.UserPreferences
                    .FirstOrDefaultAsync(p => !string.IsNullOrEmpty(p.DnseUsername));

                model.Id = 0; // force insert new row
                if (existingDnse != null)
                {
                    model.DnseUsername = existingDnse.DnseUsername;
                    model.DnsePassword = existingDnse.DnsePassword;
                    model.DnseToken = existingDnse.DnseToken;
                }
                else
                {
                    model.DnseUsername = string.Empty;
                    model.DnsePassword = string.Empty;
                    model.DnseToken = string.Empty;
                }

                _context.UserPreferences.Add(model);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã lưu thêm cấu hình mục tiêu mới!";
            }
            return RedirectToAction(nameof(Account));
        }

        // Liên kết tài khoản DNSE / DNSE
        [HttpPost]
        public async Task<IActionResult> SaveDnseAccount(string dnseUsername, string dnsePassword)
        {
            var existing = await _context.UserPreferences.FirstOrDefaultAsync();
            if (existing == null)
            {
                existing = new UserPreference
                {
                    InvestmentHorizon = "Short-term (T+2.5)",
                    TargetProfitPercentage = 15,
                    MaxLossPercentage = 7,
                    AmountPerTrade = 5000000,
                    TargetAmount = 10000000,
                    TakeProfitAmount = 2000000,
                    StopLossAmount = 1000000,
                    RiskTolerance = "Medium",
                    DnseUsername = dnseUsername ?? string.Empty,
                    DnsePassword = dnsePassword ?? string.Empty,
                    DnseToken = string.Empty
                };
                _context.UserPreferences.Add(existing);
            }
            else
            {
                if (existing.DnseUsername != dnseUsername || existing.DnsePassword != dnsePassword)
                {
                    existing.DnseUsername = dnseUsername ?? string.Empty;
                    existing.DnsePassword = dnsePassword ?? string.Empty;
                    existing.DnseToken = string.Empty; // Reset token cũ
                }
                _context.UserPreferences.Update(existing);
            }
            await _context.SaveChangesAsync();

            // Tự động kích hoạt đồng bộ Deal thực tế từ DNSE ngay khi liên kết thành công
            var syncSuccess = await _dnseService.SyncPortfolioAsync();
            if (syncSuccess)
            {
                TempData["SuccessMessage"] = "Liên kết tài khoản DNSE (DNSE) và đồng bộ danh sách Deal thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Liên kết tài khoản thành công nhưng đồng bộ danh sách Deal thất bại. Vui lòng kiểm tra lại thông tin đăng nhập.";
            }
            return RedirectToAction(nameof(Index));
        }

        // Hủy liên kết tài khoản DNSE
        [HttpPost]
        public async Task<IActionResult> UnlinkDnse()
        {
            var existing = await _context.UserPreferences.FirstOrDefaultAsync();
            if (existing != null)
            {
                existing.DnseUsername = string.Empty;
                existing.DnsePassword = string.Empty;
                existing.DnseToken = string.Empty;
                _context.UserPreferences.Update(existing);

                // Xóa các vị thế OPEN đã đồng bộ từ trước
                var openPositions = await _context.TradePositions.Where(p => p.Status == "OPEN").ToListAsync();
                _context.TradePositions.RemoveRange(openPositions);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã hủy liên kết tài khoản DNSE (DNSE) thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // Tạo tín hiệu thông qua AI Copilot
        [HttpPost]
        public async Task<IActionResult> GenerateSignal(string symbol)
        {
            var stocks = GetDNSEStocks();
            var stock = stocks.FirstOrDefault(s => s.Symbol == symbol);
            if (stock == null) return NotFound();

            // Xác định xu hướng thị trường dựa trên biến động giá của mã
            string trend = stock.ChangePercentage >= 0 ? "Uptrend" : "Sideways";

            // Gọi AI Copilot phân tích dựa trên mục tiêu, bối cảnh, và bài học quá khứ
            var analysis = await _copilotService.AnalyzeAndGenerateSignalAsync(symbol, stock.CurrentPrice, stock.Rsi, trend);

            var pref = await _context.UserPreferences.FirstOrDefaultAsync() ?? new UserPreference { AmountPerTrade = 5000000 };
            var quantity = (int)(pref.AmountPerTrade / stock.CurrentPrice);
            var takeProfitPrice = analysis.TargetPrice > 0
                ? analysis.TargetPrice
                : stock.CurrentPrice * (1 + ((pref.TargetProfitPercentage > 0 ? pref.TargetProfitPercentage : 15m) / 100m));
            var stopLossPrice = analysis.StopLossPrice > 0
                ? analysis.StopLossPrice
                : stock.CurrentPrice * (1 - ((pref.MaxLossPercentage > 0 ? pref.MaxLossPercentage : 7m) / 100m));

            if (quantity == 0) quantity = 1; // Đảm bảo giao dịch tối thiểu 1 cổ phiếu cho DNSE

            // 1. Lưu lại Order phát sinh (lệnh AI giả lập)
            var order = new AiOrder
            {
                Symbol = symbol,
                OrderType = analysis.Action,
                Quantity = quantity,
                Price = stock.CurrentPrice,
                OrderDate = DateTime.Now,
                Status = analysis.Action != "HOLD" ? "FILLED" : "REJECTED",
                Rationale = analysis.Rationale
            };
            _context.AiOrders!.Add(order);

            // 2. Nếu là lệnh MUA (BUY), tự động mở một vị thế giả định để người dùng theo dõi và chốt lời/cắt lỗ
            if (analysis.Action == "BUY")
            {
                // CHỈ xóa vị thế AI cũ cùng mã từ bảng AiTradePositions (KHÔNG xóa vị thế thật đồng bộ từ DNSE!)
                var oldAiPos = await _context.AiTradePositions.FirstOrDefaultAsync(p => p.Symbol == symbol && p.Status == "OPEN");
                if (oldAiPos != null) _context.AiTradePositions.Remove(oldAiPos);

                var investedAmount = quantity * stock.CurrentPrice;
                var position = new AiTradePosition
                {
                    Symbol = symbol,
                    Quantity = quantity,
                    EntryPrice = stock.CurrentPrice,
                    EntryDate = DateTime.Now,
                    Status = "OPEN",
                    PnL = 0,
                    TakeProfitPrice = takeProfitPrice,
                    StopLossPrice = stopLossPrice,
                    InvestedAmount = investedAmount,
                    BudgetAmount = pref.AmountPerTrade
                };
                _context.AiTradePositions.Add(position);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Tín hiệu Copilot: {analysis.Action} {symbol}. Chiến lược áp dụng: {analysis.StrategyApplied}.";
            return RedirectToAction(nameof(Index));
        }

        // Bán cổ phiếu (Chốt vị thế) -> Kích hoạt Critic Agent tự học
        [HttpPost]
        public async Task<IActionResult> ClosePosition(int id, decimal currentPrice)
        {
            var position = await _context.TradePositions.FindAsync(id);
            if (position == null) return NotFound();

            position.ExitPrice = currentPrice;
            position.ExitDate = DateTime.Now;
            position.Status = "CLOSED";
            position.PnL = (currentPrice - position.EntryPrice) * position.Quantity;

            _context.TradePositions.Update(position);
            await _context.SaveChangesAsync();

            // KÍCH HOẠT VÒNG TỰ HỌC (REFLECTION LOOP):
            // Critic Agent sẽ nhảy vào đánh giá, so sánh bối cảnh mua với PnL bán thực tế, 
            // viết ra một Lesson Learned rồi cập nhật ngược lại vào Vector Database/Memory DB
            await _reflectionService.ReflectOnClosedPositionAsync(position.Id);

            TempData["SuccessMessage"] = $"Đã bán vị thế {position.Symbol}. AI đã tự phân tích lại và đúc rút bài học mới vào Trí nhớ dài hạn!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SellPartialPosition(int id, int sellQuantity, decimal currentPrice)
        {
            var position = await _context.TradePositions.FindAsync(id);
            if (position == null) return NotFound();

            if (sellQuantity <= 0 || sellQuantity >= position.Quantity)
            {
                TempData["ErrorMessage"] = $"Số lượng bán bớt không hợp lệ cho mã {position.Symbol}.";
                return RedirectToAction(nameof(Portfolio));
            }

            var currentInvested = position.InvestedAmount.HasValue && position.InvestedAmount.Value > 0
                ? position.InvestedAmount.Value
                : position.EntryPrice * position.Quantity;

            var realizedPnL = (currentPrice - position.EntryPrice) * sellQuantity;
            var soldCostBasis = position.EntryPrice * sellQuantity;
            var remainingQuantity = position.Quantity - sellQuantity;
            var remainingInvested = Math.Max(0m, currentInvested - soldCostBasis);

            position.Quantity = remainingQuantity;
            position.InvestedAmount = remainingInvested;
            position.PnL = (currentPrice - position.EntryPrice) * remainingQuantity;
            position.EntryPrice = remainingQuantity > 0
                ? remainingInvested / remainingQuantity
                : position.EntryPrice;

            if (position.TargetProfitAmount.HasValue && position.TargetProfitAmount.Value > 0 && remainingQuantity > 0)
            {
                position.TakeProfitPrice = position.EntryPrice + (position.TargetProfitAmount.Value / remainingQuantity);
            }

            if (remainingQuantity <= 0)
            {
                position.ExitPrice = currentPrice;
                position.ExitDate = DateTime.Now;
                position.Status = "CLOSED";
            }

            _context.Orders.Add(new Order
            {
                Symbol = position.Symbol,
                OrderType = "SELL",
                Quantity = sellQuantity,
                Price = currentPrice,
                OrderDate = DateTime.Now,
                Status = "FILLED",
                Rationale = $"Bán bớt {sellQuantity:N0} CP để khóa lãi {realizedPnL:N0}đ và giữ phần còn lại quay vòng vốn."
            });

            _context.TradePositions.Update(position);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã bán bớt {sellQuantity:N0} CP {position.Symbol}, khóa khoảng {realizedPnL:N0}đ lãi, còn lại {remainingQuantity:N0} CP.";
            return RedirectToAction(nameof(Portfolio));
        }

        [HttpGet]
        public IActionResult GetWatchlist()
        {
            var stocks = GetDNSEStocks();
            return Json(stocks);
        }

        [HttpGet]
        public IActionResult GetStockDetail(string symbol)
        {
            var stock = GetDNSEStocks().FirstOrDefault(s => s.Symbol == symbol);
            if (stock == null) return NotFound();

            var candles = new List<object>();
            var random = new Random();
            decimal current = stock.CurrentPrice * 0.85m;
            for (int i = 30; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;

                decimal change = (decimal)((random.NextDouble() - 0.4) * 0.04);
                current = current * (1 + change);

                decimal high = current * (1 + (decimal)(random.NextDouble() * 0.02));
                decimal low = current * (1 - (decimal)(random.NextDouble() * 0.02));
                decimal open = low + (high - low) * (decimal)random.NextDouble();
                decimal close = low + (high - low) * (decimal)random.NextDouble();

                if (i == 0) close = stock.CurrentPrice;

                candles.Add(new
                {
                    time = date.ToString("yyyy-MM-dd"),
                    open = open,
                    high = high,
                    low = low,
                    close = close
                });
            }

            var bidOrders = new List<object>();
            var askOrders = new List<object>();
            for (int i = 1; i <= 3; i++)
            {
                bidOrders.Add(new { price = stock.CurrentPrice - (i * 100), volume = random.Next(100, 5000) });
                askOrders.Add(new { price = stock.CurrentPrice + (i * 100), volume = random.Next(100, 5000) });
            }

            return Json(new
            {
                symbol = stock.Symbol,
                exchange = stock.Exchange,
                companyName = stock.CompanyName,
                referencePrice = stock.CurrentPrice * 0.98m,
                ceilingPrice = stock.CurrentPrice * 1.07m,
                floorPrice = stock.CurrentPrice * 0.93m,
                currentPrice = stock.CurrentPrice,
                changePercentage = stock.ChangePercentage,
                highPrice = stock.CurrentPrice * 1.02m,
                lowPrice = stock.CurrentPrice * 0.97m,
                averagePrice = stock.CurrentPrice,
                matchedVolume = random.Next(100000, 5000000),
                matchedValue = random.Next(10000000, 500000000),
                strategyApplied = "RSI & MACD Analysis",
                aiRationale = stock.AiSignal == "BUY" ? "Chỉ số kỹ thuật cho thấy xu hướng tăng." : "Khuyến nghị theo dõi diễn biến.",
                bids = bidOrders,
                asks = askOrders,
                historicalCandles = candles
            });
        }

        [HttpGet]
        public async Task<IActionResult> AdvancedAnalysis(int id)
        {
            TradePosition? position = null;

            if (id < 0)
            {
                var userTransactions = await _context.StockTransactions.Where(t => t.Source == "DNSE").ToListAsync();
                var symbolGroups = userTransactions.GroupBy(t => t.Symbol).ToList();
                var matchingGroup = symbolGroups.FirstOrDefault(g => -Math.Abs(g.Key.GetHashCode()) == id);
                if (matchingGroup != null)
                {
                    var symbol = matchingGroup.Key;
                    var symbolSells = matchingGroup.Where(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue).ToList();
                    decimal virtPnl = symbolSells.Sum(t => t.PnlAmount.Value);

                    var entryPrice = 0m;
                    var firstBuy = matchingGroup.OrderBy(t => t.TransactionDate).FirstOrDefault(t => t.TransactionType == "BUY");
                    if (firstBuy != null)
                    {
                        entryPrice = firstBuy.Price;
                    }

                    position = new TradePosition
                    {
                        Id = id,
                        Symbol = symbol,
                        Quantity = 0,
                        EntryPrice = entryPrice,
                        EntryDate = firstBuy?.TransactionDate ?? DateTime.Now,
                        Status = "CLOSED",
                        PnL = virtPnl
                    };
                }
            }
            else
            {
                position = await _context.TradePositions.FindAsync(id);
            }

            if (position == null) return NotFound();

            var stocks = GetDNSEStocks();
            var stock = stocks.FirstOrDefault(s => s.Symbol == position.Symbol);
            var currentPrice = stock?.CurrentPrice ?? position.EntryPrice;
            var rsi = stock?.Rsi ?? 50m;
            var changePercentage = stock?.ChangePercentage ?? 0m;

            var pnl = position.Status == "OPEN"
                ? (currentPrice - position.EntryPrice) * position.Quantity
                : position.PnL;

            var pnlPercent = position.EntryPrice > 0
                ? (currentPrice - position.EntryPrice) / position.EntryPrice * 100
                : 0;

            var symbolTransactions = await _context.StockTransactions
                .Where(t => t.Symbol == position.Symbol && t.Source == "DNSE" && t.TransactionType == "SELL" && t.TimingScore.HasValue)
                .ToListAsync();

            decimal? avgTimingScore = symbolTransactions.Any()
                ? symbolTransactions.Average(t => t.TimingScore.Value)
                : null;

            var trend = changePercentage >= 0 ? "Uptrend" : "Sideways";
            var signal = await _copilotService.AnalyzeAndGenerateSignalAsync(position.Symbol, currentPrice, rsi, trend);

            var pref = await GetUserPreference();
            var fixedBudget = position.BudgetAmount.HasValue && position.BudgetAmount.Value > 0
                ? position.BudgetAmount.Value
                : pref.AmountPerTrade;
            var investedAmount = position.InvestedAmount.HasValue && position.InvestedAmount.Value > 0
                ? position.InvestedAmount.Value
                : position.EntryPrice * position.Quantity;
            var remainingBudget = fixedBudget > 0 ? Math.Max(0m, fixedBudget - investedAmount) : 0m;

            string sellTiming = "Không khuyến nghị";
            string sellDesc = "Xu hướng giá vẫn tốt, khuyên giữ tiếp vị thế.";
            if (pnlPercent > 10 || rsi > 70)
            {
                sellTiming = "Đề xuất BÁN (Cao)";
                sellDesc = $"Chỉ báo RSI đạt {rsi:F1} (vùng quá mua), giá đang ở mức đỉnh ngắn hạn. Nên chốt lời để bảo toàn lợi nhuận.";
            }
            else if (pnlPercent < -7 || rsi < 30)
            {
                sellTiming = "Đề xuất CẮT LỖ (Cao)";
                sellDesc = $"Vị thế đã chạm ngưỡng cắt lỗ tối đa hoặc RSI quá thấp. Bán để bảo toàn nguồn vốn còn lại.";
            }

            var targetPrice = position.TakeProfitPrice ?? (position.EntryPrice * 1.15m);
            var stopLossPrice = position.StopLossPrice ?? (position.EntryPrice * 0.93m);

            int suggestSellQty = position.Quantity > 1 ? (int)Math.Floor(position.Quantity * 0.5m) : 0;
            decimal estValue = suggestSellQty * currentPrice;
            decimal estProfit = (currentPrice - position.EntryPrice) * suggestSellQty;

            int suggestBuyQty = remainingBudget >= currentPrice ? (int)Math.Floor(remainingBudget / currentPrice) : 0;
            decimal suggestBuyVal = suggestBuyQty * currentPrice;

            return Json(new
            {
                symbol = position.Symbol,
                quantity = position.Quantity,
                entryPrice = position.EntryPrice,
                currentPrice = currentPrice,
                pnL = pnl,
                pnlPercent = pnlPercent,
                aiSignal = signal.Action,
                strategyApplied = signal.StrategyApplied,
                aiRationale = signal.Rationale,
                sellSuggestion = new
                {
                    timing = sellTiming,
                    targetPrice = targetPrice,
                    stopLossPrice = stopLossPrice,
                    description = sellDesc
                },
                partialSellSuggestion = new
                {
                    timing = suggestSellQty > 0 ? "Khuyến nghị BÁN BỚT" : "Chưa tối ưu",
                    quantity = suggestSellQty,
                    estimatedValue = estValue,
                    estimatedProfit = estProfit,
                    description = suggestSellQty > 0
                        ? $"Bán bớt {suggestSellQty:N0} CP để hiện thực hóa {estProfit:N0}đ lợi nhuận tạm tính, giảm rủi ro điều chỉnh."
                        : "Không đủ số lượng cổ phiếu tối thiểu để thực hiện bán bớt."
                },
                partialBuySuggestion = new
                {
                    timing = suggestBuyQty > 0 ? "Có thể MUA THÊM" : "Không khuyến nghị",
                    remainingBudget = remainingBudget,
                    suggestedQuantity = suggestBuyQty,
                    suggestedValue = suggestBuyVal,
                    description = suggestBuyQty > 0
                        ? $"Có thể giải ngân thêm {suggestBuyVal:N0}đ (mua {suggestBuyQty:N0} CP) ở vùng giá hiện tại để tối ưu hóa vốn mà không vượt hạn mức."
                        : "Đã phân bổ hết hạn mức vốn cho mã này hoặc giá trị còn lại không đủ mua thêm 1 cổ phiếu."
                },
                avgTimingScore = avgTimingScore,
                isAiTrade = false
            });
        }


        private List<StockViewModel> GetDNSEStocks()
        {
            return _simulationLogService.GetStockStates();
        }
    }


public class StockViewModel
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal ChangePercentage { get; set; }
    public decimal Rsi { get; set; }
    public string AiSignal { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
}

public class PositionAnalysis
{
    public decimal CurrentPnL { get; set; }
    public decimal PnlPercent { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal ProgressPercent { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal AutoTpPrice { get; set; }
    public decimal FixedBudget { get; set; }
    public decimal InvestedAmount { get; set; }
    public decimal RemainingBudget { get; set; }
    public bool CanBuyMore { get; set; }
    public decimal SuggestedAddAmount { get; set; }
    public int SuggestedAddQuantity { get; set; }
    public bool CanPartialSell { get; set; }
    public int SuggestedSellQuantity { get; set; }
    public decimal SuggestedSellAmount { get; set; }
    public decimal SuggestedSellProfit { get; set; }
    public string AiSignal { get; set; } = "NONE"; // SELL, WATCH, HOLD, CAUTION, NONE
    public string AiReason { get; set; } = string.Empty;
    public int DaysHeld { get; set; }
    public int? ExpectedHoldDays { get; set; }
    public int? RemainingDays { get; set; }
    public List<ActiveLot> ActiveLots { get; set; } = new List<ActiveLot>();
    public decimal SymbolRealizedPnL { get; set; }
    public decimal SymbolCumulativePnL { get; set; }
}

public class ActiveLot
{
    public DateTime Date { get; set; }
    public int RemainingQuantity { get; set; }
    public int OriginalQuantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal PnL { get; set; }
    public decimal PnLPercent { get; set; }
}
}
