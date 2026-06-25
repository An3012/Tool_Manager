using Microsoft.AspNetCore.Mvc;
using AITradingSystem.Data;
using AITradingSystem.Models;
using AITradingSystem.Services;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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

        // Dashboard chính - Chỉ hiện thị tín hiệu và biểu đồ chung
        public async Task<IActionResult> Index()
        {
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
            var positions = await _context.TradePositions.OrderByDescending(p => p.Status == "OPEN").ThenByDescending(p => p.EntryDate).ToListAsync();
            var closedPositions = positions.Where(p => p.Status == "CLOSED").ToList();
            var stocks = GetDNSEStocks();
            var pref = await GetUserPreference();
            decimal totalPnL = 0;
            decimal totalTargetAmount = 0;
            decimal allocatedBudget = 0;
            decimal totalRealizedPnL = closedPositions.Sum(p => p.PnL);
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
                pos.PnL = (currentPrice - pos.EntryPrice) * pos.Quantity;
                totalPnL += pos.PnL;

                // Tính toán phân tích mục tiêu cho từng vị thế
                var analysis = new PositionAnalysis
                {
                    FixedBudget = positionBudget,
                    InvestedAmount = investedAmount,
                    RemainingBudget = positionBudget > 0 ? Math.Max(0m, positionBudget - investedAmount) : 0m
                };
                analysis.CurrentPnL = pos.PnL;
                analysis.PnlPercent = pos.EntryPrice > 0 ? (currentPrice - pos.EntryPrice) / pos.EntryPrice * 100 : 0;
                analysis.CanBuyMore = !pos.IsAiTrade && analysis.RemainingBudget > 0;
                analysis.SuggestedAddAmount = analysis.CanBuyMore
                    ? Math.Min(analysis.RemainingBudget, positionBudget > 0 ? positionBudget : analysis.RemainingBudget)
                    : 0m;
                analysis.SuggestedAddQuantity = currentPrice > 0
                    ? (int)Math.Floor(analysis.SuggestedAddAmount / currentPrice)
                    : 0;
                analysis.CanPartialSell = !pos.IsAiTrade && pos.Quantity > 1 && pos.PnL > 0;
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

                analysisMap[pos.Id] = analysis;
                if (pos.Status == "OPEN" && !pos.IsAiTrade)
                {
                    allocatedBudget += analysis.InvestedAmount;
                }
            }

            cumulativePnL += totalPnL;
            decimal remainingOtherBudget = Math.Max(0m, pref.TargetAmount - allocatedBudget);

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
            
            var transactions = await _context.StockTransactions.ToListAsync();
            ViewBag.Transactions = transactions;

            return View("Portfolio");
        }

        [HttpPost]
        public async Task<IActionResult> RunPlanAnalysis()
        {
            var positions = await _context.TradePositions.Where(p => p.Status == "OPEN").ToListAsync();
            var pref = await GetUserPreference();
            var stocks = GetDNSEStocks();

            var plan = new AITradingSystem.Services.GlobalPortfolioPlanResult
            {
                SuccessProbability = 75,
                PlanSummary = $"Dựa trên danh mục hiện tại và mục tiêu đầu tư, hệ thống đã lập kế hoạch tối ưu cơ cấu tài sản. Ngày bắt đầu: {DateTime.Today:dd/MM/yyyy}.",
                Actions = new List<AITradingSystem.Services.PlanAction>(),
                ExpectedContributions = new List<AITradingSystem.Services.ExpectedContribution>(),
                DailyCalendar = new List<AITradingSystem.Models.DailyCalendarItem>(),
                Rationale = "Đảm bảo phân bổ vốn theo đúng tỷ trọng rủi ro, ưu tiên chốt lời các mã đạt mục tiêu và cắt lỗ sớm các mã vi phạm.",
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                RemainingDays = 7
            };

            decimal totalAllocated = 0;
            int dayOffset = 0;

            foreach (var pos in positions)
            {
                if (pos.Status == "CLOSED") continue;

                var currentPrice = stocks.FirstOrDefault(s => s.Symbol == pos.Symbol)?.CurrentPrice ?? pos.EntryPrice;
                var currentPnL = (currentPrice - pos.EntryPrice) * pos.Quantity;
                
                decimal targetPrice = pos.TakeProfitPrice ?? (pos.EntryPrice * (1 + (pref.TargetProfitPercentage > 0 ? pref.TargetProfitPercentage / 100m : 0.1m)));
                decimal stopLossPrice = pos.StopLossPrice ?? (pos.EntryPrice * (1 - (pref.MaxLossPercentage > 0 ? pref.MaxLossPercentage / 100m : 0.05m)));
                
                decimal expectedProfit = pos.Quantity * Math.Max(0, targetPrice - pos.EntryPrice);
                decimal progress = (targetPrice - pos.EntryPrice) > 0 ? (currentPrice - pos.EntryPrice) / (targetPrice - pos.EntryPrice) * 100m : 0;
                
                int daysHeld = (DateTime.Today - pos.EntryDate.Date).Days;
                int remainDays = (pos.ExpectedHoldDays ?? 30) - daysHeld;
                
                string action, desc, calDesc;
                
                if (currentPrice >= targetPrice)
                {
                    action = "BÁN (CHỐT LỜI)";
                    desc = $"Đã đạt mục tiêu lợi nhuận ({targetPrice:N0} đ). Tiến độ 100%. Khuyến nghị chốt lời toàn bộ.";
                    calDesc = $"**{pos.Symbol}**: Giá đã đạt kỳ vọng. Nên thực hiện lệnh chốt ngay hôm nay để thu hồi vốn và lãi.";
                }
                else if (currentPrice <= stopLossPrice)
                {
                    action = "BÁN (CẮT LỖ)";
                    desc = $"Đã vi phạm ngưỡng cắt lỗ ({stopLossPrice:N0} đ). Khuyến nghị cắt lỗ khẩn cấp để bảo toàn vốn.";
                    calDesc = $"**{pos.Symbol}**: Rủi ro cao! Đề xuất đóng vị thế ngay lập tức để tránh lỗ sâu hơn.";
                }
                else if (currentPnL > 0)
                {
                    action = "NẮM GIỮ (ĐANG LÃI)";
                    desc = $"Tiến độ {progress:N1}%. Đang có lãi {currentPnL:N0} đ. Đã giữ {daysHeld} ngày. Xu hướng tốt, tiếp tục gồng lãi tới {targetPrice:N0} đ.";
                    calDesc = $"**{pos.Symbol}**: Tiếp tục nắm giữ. Cân nhắc dời điểm chặn lãi (trailing stop) lên hòa vốn để bảo vệ thành quả.";
                }
                else
                {
                    action = "NẮM GIỮ (CHỜ PHỤC HỒI)";
                    desc = $"Đang lỗ nhẹ {Math.Abs(currentPnL):N0} đ. Còn cách xa cắt lỗ ({stopLossPrice:N0} đ). Thời gian cầm còn {remainDays} ngày.";
                    calDesc = $"**{pos.Symbol}**: Giai đoạn tích lũy. Nắm giữ và theo dõi chặt chẽ ngưỡng hỗ trợ gần {stopLossPrice:N0} đ.";
                }
                
                plan.Actions.Add(new AITradingSystem.Services.PlanAction
                {
                    Ticker = pos.Symbol,
                    Action = action,
                    Description = desc
                });

                plan.ExpectedContributions.Add(new AITradingSystem.Services.ExpectedContribution
                {
                    Ticker = pos.Symbol,
                    ExpectedProfit = expectedProfit,
                    Description = $"Mục tiêu chốt tại {targetPrice:N0} đ (Khối lượng: {pos.Quantity} CP)"
                });

                var planDate = DateTime.Today.AddDays(dayOffset);
                plan.DailyCalendar.Add(new AITradingSystem.Models.DailyCalendarItem
                {
                    Date = planDate.ToString("dd/MM/yyyy"),
                    DayOfWeek = planDate.ToString("dddd"),
                    ActionType = action.Contains("BÁN") ? "BÁN" : "GIỮ",
                    Description = calDesc,
                    Target = $"{targetPrice:N0} đ",
                    CumulativePnL = $"{currentPnL:N0} đ",
                    ActualAction = "-"
                });
                
                dayOffset++;
                if (dayOffset > 7) dayOffset = 0;
            }

            PopulateActualActions(plan.DailyCalendar, await _context.StockTransactions.ToListAsync());

            ViewBag.GlobalPlan = plan;

            return await Portfolio();
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
                        item.ActualAction = "-";
                    }
                }
            }
        }

        // MÀN HÌNH MỚI: Liên kết tài khoản & Cấu hình rủi ro (Settings)

        public async Task<IActionResult> Account()
        {
            var preference = await GetUserPreference();
            ViewBag.Preference = preference;
            return View();
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
                var existing = await _context.UserPreferences.FirstOrDefaultAsync();
                if (existing == null)
                {
                    model.DnseUsername = string.Empty;
                    model.DnsePassword = string.Empty;
                    model.DnseToken = string.Empty;
                    _context.UserPreferences.Add(model);
                }
                else
                {
                    existing.InvestmentHorizon = model.InvestmentHorizon;
                    existing.TargetProfitPercentage = model.TargetProfitPercentage;
                    existing.MaxLossPercentage = model.MaxLossPercentage;
                    existing.AmountPerTrade = model.AmountPerTrade;
                    existing.TargetAmount = model.TargetAmount;
                    existing.TakeProfitAmount = model.TakeProfitAmount;
                    existing.StopLossAmount = model.StopLossAmount;
                    existing.RiskTolerance = model.RiskTolerance;
                    existing.PlanStartDate = model.PlanStartDate;
                    
                    _context.UserPreferences.Update(existing);
                }
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật cấu hình mục tiêu đầu tư thành công!";
            }
            return RedirectToAction(nameof(Index));
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

            // 1. Lưu lại Order phát sinh
            var order = new Order
            {
                Symbol = symbol,
                OrderType = analysis.Action,
                Quantity = quantity,
                Price = stock.CurrentPrice,
                OrderDate = DateTime.Now,
                Status = analysis.Action != "HOLD" ? "FILLED" : "REJECTED",
                Rationale = analysis.Rationale
            };
            _context.Orders.Add(order);

            // 2. Nếu là lệnh MUA (BUY), tự động mở một vị thế giả định để người dùng theo dõi và chốt lời/cắt lỗ
            if (analysis.Action == "BUY")
            {
                // CHỈ xóa vị thế AI cũ cùng mã (KHÔNG xóa vị thế thật đồng bộ từ DNSE!)
                var oldAiPos = await _context.TradePositions.FirstOrDefaultAsync(p => p.Symbol == symbol && p.Status == "OPEN" && p.IsAiTrade);
                if (oldAiPos != null) _context.TradePositions.Remove(oldAiPos);

                var investedAmount = quantity * stock.CurrentPrice;
                var position = new TradePosition
                {
                    Symbol = symbol,
                    Quantity = quantity,
                    EntryPrice = stock.CurrentPrice,
                    EntryDate = DateTime.Now,
                    Status = "OPEN",
                    PnL = 0,
                    TakeProfitPrice = takeProfitPrice,
                    StopLossPrice = stopLossPrice,
                    IsAiTrade = true, // Đánh dấu rõ: Đây là lệnh từ AI Copilot, KHÔNG phải vị thế thật
                    InvestedAmount = investedAmount,
                    BudgetAmount = pref.AmountPerTrade
                };
                _context.TradePositions.Add(position);
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

                candles.Add(new {
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

            return Json(new {
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
