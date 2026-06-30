using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AITradingSystem.Controllers;
using AITradingSystem.Data;
using AITradingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AITradingSystem.Services
{
    public class TradingCopilotService
    {
        private readonly AppDbContext _context;
        private readonly Kernel _kernel;
        private readonly IConfiguration _configuration;

        public TradingCopilotService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;

            // Khởi tạo Semantic Kernel dùng Google Gemini hoặc OpenAI
            var apiKey = _configuration["AiConfig:ApiKey"] ?? "MOCK_KEY";
            var builder = Kernel.CreateBuilder();

            // Mặc định đăng ký một Chat Completion Service (ví dụ OpenAI hoặc Gemini)
            // Trong thực tế sẽ gọi builder.AddOpenAIChatCompletion(...) hoặc AddGoogleGenAIChatCompletion(...)
            // Ở đây ta sẽ sử dụng mock hoặc fallback nếu ApiKey chưa được điền
            _kernel = builder.Build();
        }

        // Tạo tín hiệu Copilot phân tích chuyên sâu cho một mã cổ phiếu
        public async Task<OrderSignalResult> AnalyzeAndGenerateSignalAsync(string symbol, decimal currentPrice, decimal rsi, string marketTrend)
        {
            // 1. Đọc cấu hình đầu tư của người dùng
            var pref = await _context.UserPreferences.FirstOrDefaultAsync() ?? new UserPreference
            {
                InvestmentHorizon = "Short-term (T+2.5)",
                TargetProfitPercentage = 15,
                MaxLossPercentage = 7,
                AmountPerTrade = 5000000,
                TargetAmount = 10000000,
                TakeProfitAmount = 2000000,
                StopLossAmount = 1000000,
                RiskTolerance = "Medium"
            };

            // Tìm xem người dùng có vị thế mở nào của mã này có cài đặt mục tiêu chốt lời/cắt lỗ riêng hay không
            var positionGoal = await _context.TradePositions
                .Where(p => p.Symbol == symbol && p.Status == "OPEN")
                .FirstOrDefaultAsync();

            decimal? customTakeProfit = positionGoal?.TakeProfitPrice;
            decimal? customStopLoss = positionGoal?.StopLossPrice;
            decimal? customTargetAmount = positionGoal?.TargetProfitAmount;

            var goalPrompt = "";
            if (customTargetAmount.HasValue && customTargetAmount.Value > 0 && positionGoal != null)
            {
                decimal currentPnL = (currentPrice - positionGoal.EntryPrice) * positionGoal.Quantity;
                decimal progressPct = currentPnL / customTargetAmount.Value * 100;
                goalPrompt += $"\n- MỤC TIÊU LỢI NHUẬN TỔNG cho mã {symbol}: {customTargetAmount.Value:N0} đ. Lãi/Lỗ hiện tại: {currentPnL:N0} đ (Tiến độ: {progressPct:N1}%)";
            }
            if (customTakeProfit.HasValue && customTakeProfit.Value > 0)
            {
                goalPrompt += $"\n- MỤC TIÊU CHỐT LỜI RIÊNG cho mã {symbol}: {customTakeProfit.Value:N0} đ (Tăng {((customTakeProfit.Value - currentPrice) / currentPrice * 100):N1}% so với giá hiện tại)";
            }
            if (customStopLoss.HasValue && customStopLoss.Value > 0)
            {
                goalPrompt += $"\n- NGƯỠNG CẮT LỖ RIÊNG cho mã {symbol}: {customStopLoss.Value:N0} đ (Giảm {((currentPrice - customStopLoss.Value) / currentPrice * 100):N1}% so với giá hiện tại)";
            }
            if (positionGoal != null && positionGoal.ExpectedHoldDays.HasValue && positionGoal.ExpectedHoldDays.Value > 0)
            {
                var daysHeld = (DateTime.Now.Date - positionGoal.EntryDate.Date).Days;
                if (daysHeld < 0) daysHeld = 0;
                var remainingDays = positionGoal.ExpectedHoldDays.Value - daysHeld;
                goalPrompt += $"\n- THỜI GIAN GIỮ DỰ KIẾN: {positionGoal.ExpectedHoldDays.Value} ngày (Đã giữ {daysHeld} ngày, còn {remainingDays} ngày)";
            }

            // 2. Lấy kiến thức (Knowledge Engine) về các chiến lược và điều kiện áp dụng tương ứng
            var strategies = await _context.TradingStrategies.ToListAsync();
            var strategiesContext = string.Join("\n", strategies.Select(s => $"- {s.Name}: Áp dụng trong điều kiện '{s.StrategyType}' (Ví dụ: {s.Description}). Quy tắc logic: {s.RuleLogic}"));

            // 3. Lấy ký ức kinh nghiệm (Memory Engine) - Các bài học quá khứ
            // Lấy 5 bài học thất bại/thành công gần đây nhất trong cơ sở dữ liệu
            var pastLessons = await _context.TradeEpisodes
                .Where(e => !string.IsNullOrEmpty(e.LessonLearned))
                .OrderByDescending(e => e.Timestamp)
                .Take(5)
                .ToListAsync();

            var memoryContext = pastLessons.Any()
                ? string.Join("\n", pastLessons.Select(l => $"- Lệnh {l.ActionTaken} trước đây: Kết quả {l.Result}. Bài học rút ra: {l.LessonLearned}"))
                : "Chưa có bài học kinh nghiệm nào được lưu trữ.";

            // 4. Tạo bối cảnh thị trường hiện tại (Market Context)
            var currentContext = $"Mã: {symbol}, Giá hiện tại: {currentPrice:N0} đ, RSI: {rsi}, Xu hướng thị trường: {marketTrend}.";

            // 5. Xây dựng Prompt gửi cho AI
            var prompt = $@"
Bạn là AI Trading Copilot chuyên nghiệp tối ưu riêng cho thị trường chứng khoán Việt Nam.Nhiệm vụ của bạn là đưa ra tín hiệu cố vấn giao dịch (MUA, BÁN, hoặc THEO DÕI) dựa trên bối cảnh thị trường, mục tiêu của người dùng, thư viện chiến lược (Knowledge) và các bài học kinh nghiệm trong quá khứ (Memory).[MỤC TIÊU CỦA NGƯỜI DÙNG]
- Thời gian đầu tư: {pref.InvestmentHorizon}
- Số tiền đầu tư tối đa mỗi mã: {pref.AmountPerTrade:N0} VND
- Mục tiêu lợi nhuận (đ): {pref.TargetAmount:N0}
- Chốt lời theo tiền (đ): {pref.TakeProfitAmount:N0}
- Cắt lỗ theo tiền (đ): {pref.StopLossAmount:N0}
- Khẩu vị rủi ro: {pref.RiskTolerance}
{(string.IsNullOrEmpty(goalPrompt) ? $"- Mục tiêu lợi nhuận mặc định: {pref.TargetProfitPercentage}%\n- Ngưỡng cắt lỗ tối đa mặc định: {pref.MaxLossPercentage}%" : $"- Cấu hình mục tiêu riêng biệt đã thiết lập:{goalPrompt}")}

[THƯ VIỆN CHIẾN LƯỢC KHẢ DỤNG - KNOWLEDGE ENGINE]
{strategiesContext}

[BÀI HỌC KINH NGHIỆM TRONG QUÁ KHỨ - MEMORY ENGINE]
{memoryContext}

[BỐI CẢNH THỊ TRƯỜNG HIỆN TẠI]
{currentContext}

YÊU CẦU ĐẶC BIỆT:
Nếu người dùng đã đặt mục tiêu chốt lời hoặc cắt lỗ riêng biệt cho mã {symbol} ở trên, bạn phải ưu tiên phân tích so với các mốc giá này:
1. Nếu giá hiện tại đạt hoặc vượt mốc chốt lời, đề xuất hành động phải là BÁN (SELL) với lý do chốt lời bảo vệ thành quả.2. Nếu giá hiện tại chạm hoặc giảm dưới mốc cắt lỗ, đề xuất hành động phải là BÁN (SELL) với lý do cắt lỗ quản trị rủi ro.3. Nếu chưa chạm các mốc mục tiêu riêng biệt đặt ra, hãy kết hợp chỉ báo kỹ thuật để đưa ra tín hiệu MUA (BUY) hoặc THEO DÕI (HOLD).Vui lòng trả về kết quả dưới định dạng JSON như sau:
{{
  ""Action"": ""BUY/SELL/HOLD"",
  ""StrategyApplied"": ""Tên chiến lược áp dụng"",
  ""Rationale"": ""Giải thích chi tiết lý do và lưu ý bài học quá khứ tại đây"",
  ""TargetPrice"": 0,
  ""StopLossPrice"": 0
}}";

            // 6. Thực thi thông qua LLM (Semantic Kernel hoặc Mock Fallback nếu chưa cấu hình Key)
            string aiResponse;
            if (string.IsNullOrEmpty(_configuration["AiConfig:ApiKey"]) || _configuration["AiConfig:ApiKey"] == "MOCK_KEY")
            {
                // Fallback giả lập logic suy luận của AI
                aiResponse = SimulateAiReasoning(symbol, currentPrice, rsi, marketTrend, pref, pastLessons, positionGoal);
            }
            else
            {
                // Gọi AI thật qua Semantic Kernel
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                var history = new ChatHistory();
                history.AddUserMessage(prompt);
                var response = await chatService.GetChatMessageContentAsync(history);
                aiResponse = response.Content ?? string.Empty;
            }

            try
            {
                var result = JsonSerializer.Deserialize<OrderSignalResult>(aiResponse);
                return result ?? GetDefaultHoldResult();
            }
            catch
            {
                return GetDefaultHoldResult();
            }
        }

        private string SimulateAiReasoning(string symbol, decimal currentPrice, decimal rsi, string marketTrend, UserPreference pref, List<TradeEpisode> pastLessons, TradePosition? positionGoal)
        {
            string action = "HOLD";
            string strategy = "None";
            string rationale = "";

            decimal? customTakeProfit = positionGoal?.TakeProfitPrice;
            decimal? customStopLoss = positionGoal?.StopLossPrice;

            // Tính toán giá chốt/cắt dựa trên cài đặt riêng hoặc mặc định
            decimal targetPrice = customTakeProfit.HasValue && customTakeProfit.Value > 0
                ? customTakeProfit.Value
                : currentPrice * (1 + pref.TargetProfitPercentage / 100);

            decimal stopLoss = customStopLoss.HasValue && customStopLoss.Value > 0
                ? customStopLoss.Value
                : currentPrice * (1 - pref.MaxLossPercentage / 100);

            // Kiểm tra xem có bài học quá khứ nào cảnh báo không
            var hasRsiWarning = pastLessons.Any(l => l.LessonLearned.Contains("RSI") && l.Result.Contains("Lỗ"));

            // Tính toán thời gian nắm giữ
            int daysHeld = 0;
            int? remainingDays = null;
            if (positionGoal != null)
            {
                daysHeld = (DateTime.Now.Date - positionGoal.EntryDate.Date).Days;
                if (daysHeld < 0) daysHeld = 0;
                if (positionGoal.ExpectedHoldDays.HasValue && positionGoal.ExpectedHoldDays.Value > 0)
                {
                    remainingDays = positionGoal.ExpectedHoldDays.Value - daysHeld;
                }
            }

            // Nếu giá đã chạm hoặc vượt mốc mục tiêu chốt lời riêng biệt đặt ra
            if (currentPrice >= targetPrice)
            {
                action = "SELL";
                strategy = "Chốt lời chủ động (Take Profit)";
                rationale = $"Giá hiện tại ({currentPrice:N0} đ) đã đạt hoặc vượt mốc mục tiêu chốt lời riêng biệt đặt ra là {targetPrice:N0} đ. Khuyến nghị BÁN chốt lời ngay để bảo toàn thành quả đầu tư.";
            }
            // Nếu giá đã chạm hoặc giảm dưới mốc cắt lỗ riêng biệt đặt ra
            else if (currentPrice <= stopLoss)
            {
                action = "SELL";
                strategy = "Cắt lỗ chủ động (Stop Loss)";
                rationale = $"Giá hiện tại ({currentPrice:N0} đ) đã chạm hoặc giảm dưới ngưỡng cắt lỗ riêng biệt đặt ra là {stopLoss:N0} đ. Khuyến nghị BÁN cắt lỗ ngay để quản lý rủi ro danh mục.";
            }
            // Nếu đã quá hạn nắm giữ dự kiến
            else if (remainingDays.HasValue && remainingDays.Value <= 0)
            {
                action = "SELL";
                strategy = "Hết thời hạn đầu tư";
                var pnlVal = (currentPrice - (positionGoal?.EntryPrice ?? currentPrice)) * (positionGoal?.Quantity ?? 1);
                if (pnlVal < 0)
                {
                    rationale = $"Mã {symbol} đã giữ {daysHeld}/{positionGoal.ExpectedHoldDays.Value} ngày (quá hạn). Vị thế đang lỗ {Math.Abs(pnlVal):N0} đ. Khuyến nghị BÁN dứt khoát để cơ cấu danh mục.";
                }
                else
                {
                    rationale = $"Mã {symbol} đã giữ {daysHeld}/{positionGoal.ExpectedHoldDays.Value} ngày (quá hạn). Vị thế đang có lãi {pnlVal:N0} đ. Khuyến nghị BÁN chốt vị thế để thu hồi vốn.";
                }
            }
            // Nếu sắp đến hạn nắm giữ dự kiến (còn 1-2 ngày)
            else if (remainingDays.HasValue && remainingDays.Value <= 2)
            {
                var pnlVal = (currentPrice - (positionGoal?.EntryPrice ?? currentPrice)) * (positionGoal?.Quantity ?? 1);
                if (pnlVal < 0)
                {
                    action = "SELL"; // Cảnh báo quá sát hạn, khuyên cắt
                    strategy = "Cắt lỗ cận ngày đáo hạn";
                    rationale = $"Sắp hết thời gian đầu tư dự kiến (Còn {remainingDays.Value} ngày). Vị thế đang lỗ {Math.Abs(pnlVal):N0} đ. Đề xuất bán cơ cấu sớm.";
                }
                else
                {
                    action = "HOLD";
                    strategy = "Theo dõi sát đáo hạn";
                    rationale = $"Sắp hết thời gian đầu tư dự kiến (Còn {remainingDays.Value} ngày). Vị thế đang có lãi {pnlVal:N0} đ. Theo dõi sát để chốt lời.";
                }
            }
            else if (rsi < 35 && marketTrend == "Sideways")
            {
                action = "BUY";
                strategy = "RSI Quá mua / Quá bán (RSI Overbought/Oversold)";
                rationale = $"Bối cảnh thị trường {marketTrend} rất phù hợp để áp dụng chiến lược RSI vùng quá bán. " +
                            $"Mã {symbol} có RSI đạt {rsi} (dưới 35). " +
                            $"Khối lượng phân bổ khuyến nghị tương ứng vốn {pref.AmountPerTrade:N0} đ.";

                if (hasRsiWarning)
                {
                    rationale += " Lưu ý từ bài học quá khứ: Tránh mua đuổi nếu giá chưa có tín hiệu rút chân phục hồi.";
                }
            }
            else if (rsi > 70)
            {
                action = "SELL";
                strategy = "RSI Quá mua / Quá bán (RSI Overbought/Oversold)";
                rationale = $"Chỉ báo RSI đạt {rsi} ở vùng quá mua cực đại. Khuyến nghị bán chốt lời bảo vệ thành quả.";
            }
            else if (marketTrend == "Uptrend")
            {
                action = "BUY";
                strategy = "MACD Cắt đường tín hiệu (MACD Signal Line Crossover)";
                rationale = $"Thị trường đang trong xu hướng tăng ({marketTrend}). Chỉ báo động lượng MACD ủng hộ xu hướng. Áp dụng chiến lược Trend Following.";
            }
            else
            {
                rationale = $"Hiện tại mã {symbol} đang biến động trong vùng an toàn (RSI: {rsi}, Giá hiện tại: {currentPrice:N0} đ). Chưa đạt các điều kiện kích hoạt chốt lời ({targetPrice:N0} đ) hay cắt lỗ ({stopLoss:N0} đ).";
            }

            var mockResult = new OrderSignalResult
            {
                Action = action,
                StrategyApplied = strategy,
                Rationale = rationale,
                TargetPrice = targetPrice,
                StopLossPrice = stopLoss
            };

            return JsonSerializer.Serialize(mockResult);
        }

        private OrderSignalResult GetDefaultHoldResult()
        {
            return new OrderSignalResult
            {
                Action = "HOLD",
                StrategyApplied = "None",
                Rationale = "Chưa đủ dữ liệu phân tích tín hiệu.",
                TargetPrice = 0,
                StopLossPrice = 0
            };
        }

        // Lập kế hoạch tối ưu danh mục tổng thể dựa trên các tham số:
        // - Mục tiêu lợi nhuận tổng thể (pref.TargetAmount)
        // - Vốn đầu tư (pref.AmountPerTrade hoặc tổng)
        // - Số ngày kế hoạch (tính từ pref.InvestmentHorizon)
        // - Ngày bắt đầu kế hoạch (pref.PlanStartDate)
        // - Các mã cp đang nắm giữ (positions)
        public async Task<GlobalPortfolioPlanResult> GenerateGlobalPortfolioPlanAsync(List<TradePosition> positions, UserPreference pref, List<StockViewModel> stocks, decimal cumulativePnL, decimal totalTargetAmount)
        {
            DateTime startDate = pref.PlanStartDate ?? DateTime.Today;

            // Lấy số ngày kế hoạch từ InvestmentHorizon
            int totalDays = 30;
            if (!string.IsNullOrEmpty(pref.InvestmentHorizon))
            {
                var match = System.Text.RegularExpressions.Regex.Match(pref.InvestmentHorizon, @"\d+");
                if (match.Success) int.TryParse(match.Value, out totalDays);
            }

            DateTime endDate = startDate.AddDays(totalDays);
            int remainingDays = (endDate.Date - DateTime.Today.Date).Days;
            if (remainingDays < 0) remainingDays = 0;

            decimal targetAmountToUse = pref.TargetAmount;
            decimal remainingAmountTotal = Math.Max(0m, targetAmountToUse - cumulativePnL);

            decimal allocatedBudget = 0;
            foreach (var pos in positions)
            {
                var currentPrice = stocks.FirstOrDefault(s => s.Symbol == pos.Symbol)?.CurrentPrice ?? pos.EntryPrice;
                var investedAmount = pos.InvestedAmount.HasValue && pos.InvestedAmount.Value > 0
                    ? pos.InvestedAmount.Value
                    : pos.EntryPrice * pos.Quantity;
                allocatedBudget += investedAmount;
            }
            decimal remainingOtherBudget = Math.Max(0m, pref.AmountPerTrade - allocatedBudget);

            // Bối cảnh vị thế đang mở
            var positionsContext = positions.Any()
                ? string.Join("\n", positions.Select(p => $"- {p.Symbol}: SL {p.Quantity}, Giá vào {p.EntryPrice:N0} đ, Lãi/Lỗ hiện tại {p.PnL:N0} đ, Chốt lời {p.TakeProfitPrice?.ToString("N0") ?? "N/A"} đ, Cắt lỗ {p.StopLossPrice?.ToString("N0") ?? "N/A"} đ"))
                : "Không có vị thế mở nào.";

            // Bối cảnh giá hiện tại
            var stocksContext = string.Join("\n", stocks.Select(s => $"- {s.Symbol}: Giá hiện tại {s.CurrentPrice:N0} đ, Thay đổi {s.ChangePercentage:N2}%, RSI {s.Rsi:N1}"));

            var prompt = $@"
Bạn là AI Trading Copilot chuyên nghiệp tối ưu hóa danh mục đầu tư.
Nhiệm vụ của bạn là lập một KẾ HOẠCH TỐI ƯU HÓA DANH MỤC tổng thể cho người dùng từ ngày bắt đầu đến ngày kết thúc kế hoạch.

[THÔNG TIN KẾ HOẠCH CỦA NGƯỜI DÙNG]
- Ngày bắt đầu kế hoạch: {startDate:dd/MM/yyyy}
- Số ngày kế hoạch: {totalDays} ngày (Ngày kết thúc: {endDate:dd/MM/yyyy}, Số ngày còn lại từ hôm nay: {remainingDays} ngày)
- Vốn đầu tư mỗi lệnh: {pref.AmountPerTrade:N0} đ
- Vốn đã phân bổ giải ngân: {allocatedBudget:N0} đ
- Vốn khả dụng còn lại (Sức mua còn lại): {remainingOtherBudget:N0} đ
- Mục tiêu lợi nhuận tổng thể: {targetAmountToUse:N0} đ
- Lợi nhuận lũy kế hiện tại (đã bán + đang mở): {cumulativePnL:N0} đ

[DANH MỤC CÁC VỊ THẾ ĐANG MỞ]
{positionsContext}

[GIÁ THỊ TRƯỜNG HIỆN TẠI VÀ CHỈ BÁO KỸ THUẬT]
{stocksContext}

### YÊU CẦU LẬP KẾ HOẠCH & HẠN CHẾ SỨC MUA

1. **Đánh giá xác suất thành công (%)** để đạt được mục tiêu lợi nhuận tổng thể còn thiếu (**{remainingAmountTotal:N0} đ**) trong **{remainingDays}** ngày còn lại. Đánh giá cần dựa trên tình trạng hiện tại của danh mục, diễn biến thị trường, mức độ biến động của từng cổ phiếu, tiến độ đạt mục tiêu và các yếu tố rủi ro có thể ảnh hưởng đến kết quả.

2. **Đề xuất hành động cụ thể cho từng mã cổ phiếu** trong danh mục, bao gồm các hành động **MUA, MUA THÊM, BÁN, NẮM GIỮ (HOLD) hoặc CHUYỂN VỐN**, nhằm tối ưu hóa tỷ trọng rủi ro/lợi nhuận của toàn bộ danh mục. Mỗi đề xuất cần nêu rõ lý do, mức độ ưu tiên và tác động kỳ vọng đến mục tiêu lợi nhuận chung.

3. **HẠN CHẾ SỨC MUA:** Mọi đề xuất **MUA** hoặc **MUA THÊM** phải tuân thủ tuyệt đối giới hạn vốn khả dụng còn lại của người dùng là **{remainingOtherBudget:N0} đ**.

   * Tổng giá trị giải ngân của tất cả các lệnh mua được đề xuất **không được vượt quá {remainingOtherBudget:N0} đ**.
   * AI phải **tự động tính toán và đề xuất số lượng cổ phiếu phù hợp cho từng giao dịch**, dựa trên các yếu tố như: giá hiện tại, số vốn khả dụng, tỷ trọng danh mục, mục tiêu lợi nhuận, mức độ rủi ro, thanh khoản, mức độ tin cậy của cơ hội đầu tư và hiệu quả sử dụng vốn.
   * **Không được áp dụng số lượng cố định hoặc bất kỳ quy tắc cứng nào** (ví dụ: luôn mua 100, 200, 500 hoặc bội số của 100 cổ phiếu). AI cần chủ động lựa chọn số lượng giao dịch tối ưu theo từng trường hợp cụ thể, đồng thời tuân thủ các quy định giao dịch của thị trường (nếu có).
   * AI cần ưu tiên sử dụng nguồn vốn một cách hiệu quả, tránh để vốn nhàn rỗi quá lớn nhưng cũng không giải ngân vượt quá mức rủi ro hợp lý.
   * Nếu số vốn khả dụng còn lại không đủ để thực hiện một giao dịch mua hợp lệ theo quy định của thị trường hoặc không đủ để tạo ra một giao dịch có ý nghĩa về mặt đầu tư, AI **không được khuyến nghị MUA hoặc MUA THÊM**, mà phải đề xuất **THEO DÕI (HOLD)** hoặc **BÁN/CHUYỂN VỐN** để cơ cấu lại danh mục trước khi xem xét giải ngân.

4. **Lập LỊCH TRÌNH HOẠT ĐỘNG HẰNG NGÀY (Daily Action Calendar)** từ hôm nay (**{DateTime.Today:dd/MM/yyyy}**) đến ngày kết thúc (**{endDate:dd/MM/yyyy}**).

   Lịch trình cần chỉ rõ:
   * Ngày thực hiện.
   * Mã cổ phiếu liên quan.
   * Hành động được đề xuất (MUA, MUA THÊM, BÁN, HOLD hoặc CHUYỂN VỐN).
   * Điều kiện kích hoạt hành động (ví dụ: giá đạt ngưỡng, khối lượng tăng, tín hiệu kỹ thuật xuất hiện...).
   * Mục tiêu kỳ vọng sau khi thực hiện hành động.
   * Mức độ ưu tiên của từng hành động nếu có nhiều giao dịch trong cùng một ngày.

5. **Yêu cầu tối ưu hóa danh mục tổng thể**

   AI không được đánh giá từng mã một cách độc lập mà phải tối ưu trên toàn bộ danh mục đầu tư. Mỗi quyết định mua, bán hoặc giữ cần xem xét đồng thời các yếu tố sau:
   * Khả năng hoàn thành mục tiêu lợi nhuận tổng thể.
   * Phân bổ tỷ trọng vốn hợp lý giữa các mã.
   * Mức độ rủi ro của từng vị thế và của toàn danh mục.
   * Tính thanh khoản của cổ phiếu.
   * Khả năng xoay vòng vốn để tận dụng các cơ hội có xác suất thành công cao hơn.
   * Chi phí giao dịch, thuế và ảnh hưởng đến lợi nhuận thực tế.
   * Tương quan giữa các mã nhằm tránh tập trung rủi ro vào cùng một nhóm ngành hoặc cùng xu hướng thị trường.

6. **Yêu cầu giải thích quyết định**

   Với mỗi khuyến nghị, AI cần giải thích ngắn gọn nhưng rõ ràng:
   * Vì sao lựa chọn hành động đó.
   * Vì sao lựa chọn số lượng cổ phiếu như đề xuất.
   * Ảnh hưởng của quyết định đến mục tiêu lợi nhuận và mức độ rủi ro của danh mục.
   * Những điều kiện có thể khiến khuyến nghị cần thay đổi trong các ngày tiếp theo.

7. **Nguyên tắc bắt buộc**

   * Không đề xuất bất kỳ giao dịch nào vượt quá số vốn khả dụng.
   * Không giả định người dùng có thể bổ sung thêm vốn nếu không được cung cấp thông tin.
   * Không sử dụng các quy tắc cố định về số lượng cổ phiếu; AI phải tự tính toán và tối ưu theo từng tình huống cụ thể.
   * Ưu tiên tối đa hóa xác suất hoàn thành mục tiêu lợi nhuận trong khi vẫn kiểm soát rủi ro của toàn bộ danh mục.

Vui lòng trả về kết quả dưới định dạng JSON khớp chính xác với lớp GlobalPortfolioPlanResult như sau:
{{
  ""SuccessProbability"": 75,
  ""PlanSummary"": ""Tóm tắt kế hoạch hành động tổng thể..."",
  ""Actions"": [
    {{ ""Ticker"": ""VCG"", ""Action"": ""BÁN (CHỐT LỜI)"", ""Description"": ""Giá đạt mốc kỳ vọng..."" }}
  ],
  ""ExpectedContributions"": [
    {{ ""Ticker"": ""VCG"", ""ExpectedProfit"": 500000, ""Description"": ""Mục tiêu chốt tại..."" }}
  ],
  ""Rationale"": ""Giải thích cơ sở phân tích danh mục..."",
  ""DailyCalendar"": [
    {{
      ""Date"": ""26/06/2026"",
      ""DayOfWeek"": ""Friday"",
      ""ActionType"": ""BÁN"",
      ""Description"": ""Hành động chi tiết cho ngày này..."",
      ""Target"": ""22,000 đ"",
      ""CumulativePnL"": ""100,000 đ"",
      ""ActualAction"": ""-""
    }}
  ]
}}";

            string aiResponse = string.Empty;
            if (string.IsNullOrEmpty(_configuration["AiConfig:ApiKey"]) || _configuration["AiConfig:ApiKey"] == "MOCK_KEY")
            {
                // Fallback giả lập lập kế hoạch thông minh
                aiResponse = SimulatePlanReasoning(positions, pref, stocks, cumulativePnL, totalDays, startDate, endDate, remainingDays, remainingAmountTotal, totalTargetAmount);
            }
            else
            {
                try
                {
                    var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                    var history = new ChatHistory();
                    history.AddUserMessage(prompt);
                    var response = await chatService.GetChatMessageContentAsync(history);
                    aiResponse = response.Content ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI Planner] Lỗi gọi LLM: {ex.Message}");
                    aiResponse = SimulatePlanReasoning(positions, pref, stocks, cumulativePnL, totalDays, startDate, endDate, remainingDays, remainingAmountTotal, totalTargetAmount);
                }
            }

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<GlobalPortfolioPlanResult>(aiResponse, options);
                if (result != null)
                {
                    return result;
                }
            }
            catch (Exception parseEx)
            {
                Console.WriteLine($"[AI Planner] Lỗi parse JSON kết quả kế hoạch: {parseEx.Message}");
            }

            // Fallback cuối cùng nếu có lỗi parse
            var fallbackJson = SimulatePlanReasoning(positions, pref, stocks, cumulativePnL, totalDays, startDate, endDate, remainingDays, remainingAmountTotal, totalTargetAmount);
            return JsonSerializer.Deserialize<GlobalPortfolioPlanResult>(fallbackJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        private string SimulatePlanReasoning(List<TradePosition> positions, UserPreference pref, List<StockViewModel> stocks, decimal cumulativePnL, int totalDays, DateTime startDate, DateTime endDate, int remainingDays, decimal remainingAmountTotal, decimal totalTargetAmount)
        {
            decimal targetAmountToUse = pref.TargetAmount;
            // Tính xác suất thành công
            decimal successProbability = 50;
            decimal targetProgress = 0;
            if (targetAmountToUse <= 0)
            {
                successProbability = 100;
            }
            else
            {
                targetProgress = cumulativePnL / targetAmountToUse * 100;
                if (cumulativePnL >= targetAmountToUse)
                {
                    successProbability = Math.Min(99m, 95m + (remainingDays > 0 ? 3m : 4m));
                }
                else
                {
                    double daysRatio = (double)remainingDays / (totalDays > 0 ? totalDays : 30);
                    decimal progressWeight = Math.Clamp(targetProgress, 0, 100);
                    successProbability = Math.Clamp(progressWeight + (decimal)(daysRatio * 40.0), 10, 95);

                    int winningPositions = positions.Count(p => p.PnL > 0);
                    int totalPositions = positions.Count;
                    if (totalPositions > 0)
                    {
                        decimal winRate = (decimal)winningPositions / totalPositions;
                        successProbability += (winRate - 0.5m) * 15m;
                    }
                    successProbability = Math.Round(Math.Clamp(successProbability, 5, 98), 1);
                }
            }

            var actions = new List<PlanAction>();
            var expectedContributions = new List<ExpectedContribution>();
            var dailyCalendar = new List<AITradingSystem.Models.DailyCalendarItem>();

            foreach (var pos in positions)
            {
                var currentPrice = stocks.FirstOrDefault(s => s.Symbol == pos.Symbol)?.CurrentPrice ?? pos.EntryPrice;
                decimal targetPrice = pos.TakeProfitPrice ?? (pos.EntryPrice * (1 + (pref.TargetProfitPercentage > 0 ? pref.TargetProfitPercentage / 100m : 0.15m)));
                decimal stopLossPrice = pos.StopLossPrice ?? (pos.EntryPrice * (1 - (pref.MaxLossPercentage > 0 ? pref.MaxLossPercentage / 100m : 0.07m)));
                decimal expectedProfit = pos.Quantity * Math.Max(0, targetPrice - pos.EntryPrice);

                string actionStr = "NẮM GIỮ";
                string descStr = "";

                if (currentPrice >= targetPrice)
                {
                    actionStr = "BÁN (CHỐT LỜI)";
                    descStr = $"Giá hiện tại {currentPrice:N0} đ đã đạt mục tiêu chốt lời {targetPrice:N0} đ. Khuyến nghị BÁN chốt lời để hiện thực hóa lợi nhuận.";
                }
                else if (currentPrice <= stopLossPrice)
                {
                    actionStr = "BÁN (CẮT LỖ)";
                    descStr = $"Giá hiện tại {currentPrice:N0} đ vi phạm mốc cắt lỗ {stopLossPrice:N0} đ. Khuyến nghị BÁN cắt lỗ ngay quản trị rủi ro.";
                }
                else if (pos.PnL > 0)
                {
                    actionStr = "NẮM GIỮ (GỒNG LÃI)";
                    descStr = $"Đang lãi tạm tính {pos.PnL:N0} đ. Giá giữ vững trên hỗ trợ. Tiếp tục gồng lãi hướng tới {targetPrice:N0} đ.";
                }
                else
                {
                    actionStr = "NẮM GIỮ (CHỜ HỒI)";
                    descStr = $"Đang lỗ nhẹ {Math.Abs(pos.PnL):N0} đ. Vẫn trên hỗ trợ cứng {stopLossPrice:N0} đ. Tiếp tục nắm giữ theo dõi phản ứng giá.";
                }

                actions.Add(new PlanAction { Ticker = pos.Symbol, Action = actionStr, Description = descStr });
                expectedContributions.Add(new ExpectedContribution { Ticker = pos.Symbol, ExpectedProfit = expectedProfit, Description = $"Mục tiêu chốt tại {targetPrice:N0} đ (Khối lượng: {pos.Quantity} CP)" });
            }

            if (!positions.Any())
            {
                actions.Add(new PlanAction { Ticker = "DANH MỤC", Action = "MUA", Description = "Không có vị thế mở nào. Đề xuất giải ngân 20-30% vốn vào các mã tiềm năng khi thị trường điều chỉnh." });
            }

            // Tạo lịch giao dịch tối ưu cho tất cả các ngày từ startDate đến endDate
            var calendarDays = new List<DateTime>();
            var currentDay = startDate.Date;
            var maxDate = endDate.Date;
            while (currentDay <= maxDate)
            {
                calendarDays.Add(currentDay);
                currentDay = currentDay.AddDays(1);
            }

            // AI tự động đánh giá và chọn lọc các mã tiềm năng dựa trên tín hiệu AI (AiSignal), chỉ số RSI và xu hướng giá
            var evaluatedCandidates = stocks
                .Where(s => s.Rsi >= 30 && s.Rsi <= 70)
                .OrderByDescending(s => s.AiSignal == "BUY" ? 2 : (s.AiSignal == "HOLD" ? 1 : 0))
                .ThenByDescending(s => s.ChangePercentage)
                .Select(s => s.Symbol)
                .ToList();

            // Dự phòng nếu không tìm thấy mã nào tối ưu
            if (!evaluatedCandidates.Any())
            {
                evaluatedCandidates = stocks.Select(s => s.Symbol).ToList();
            }
            if (!evaluatedCandidates.Any())
            {
                evaluatedCandidates = new List<string> { "HPG", "SSI", "MWG", "FPT", "VIC", "VNM" };
            }

            // Đưa các mã người dùng đang nắm giữ lên đầu danh sách để ưu tiên cơ cấu/giao dịch
            var candidateStocks = new List<string>();
            foreach (var pos in positions)
            {
                if (!candidateStocks.Contains(pos.Symbol))
                {
                    candidateStocks.Add(pos.Symbol);
                }
            }
            foreach (var sym in evaluatedCandidates)
            {
                if (!candidateStocks.Contains(sym))
                {
                    candidateStocks.Add(sym);
                }
            }

            decimal allocatedBudget = 0;
            foreach (var pos in positions)
            {
                var currentPrice = stocks.FirstOrDefault(s => s.Symbol == pos.Symbol)?.CurrentPrice ?? pos.EntryPrice;
                var investedAmount = pos.InvestedAmount.HasValue && pos.InvestedAmount.Value > 0
                    ? pos.InvestedAmount.Value
                    : pos.EntryPrice * pos.Quantity;
                allocatedBudget += investedAmount;
            }
            decimal remainingOtherBudget = Math.Max(0m, pref.AmountPerTrade - allocatedBudget);
            decimal simulatedRemainingBudget = remainingOtherBudget;

            // Lưu trữ trạng thái nắm giữ động của từng mã để truyền qua các ngày
            var symbolHoldings = new Dictionary<string, int>();
            foreach (var pos in positions)
            {
                symbolHoldings[pos.Symbol] = pos.Quantity;
            }

            int totalBusinessDays = calendarDays.Count(d => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday);
            int sellCyclesCount = Math.Max(1, totalBusinessDays / 4); // Cứ mỗi 4 ngày giao dịch có 1 nhịp chốt lời xoay vòng
            decimal profitPerCycle = Math.Round(targetAmountToUse / sellCyclesCount / 1000m) * 1000m;
            decimal currentProjectedPnL = cumulativePnL;
            int businessDayIndex = 0;

            foreach (var date in calendarDays)
            {
                string actionType = "GIỮ";
                string description = "";
                string target = "-";

                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    actionType = "SÀN ĐÓNG CỬA";
                    description = "Thị trường nghỉ giao dịch cuối tuần. Hãy nghiên cứu tin tức vĩ mô và phân tích các mã tiềm năng cho tuần mới.";
                }
                else
                {
                    var syncThresholdDate = DateTime.Now.TimeOfDay >= new TimeSpan(15, 30, 0)
                        ? DateTime.Today.Date.AddDays(1)
                        : DateTime.Today.Date;

                    if (date <= syncThresholdDate)
                    {
                        foreach (var pos in positions)
                        {
                            symbolHoldings[pos.Symbol] = pos.Quantity;
                        }
                        var posSymbols = positions.Select(p => p.Symbol).ToHashSet();
                        foreach (var key in symbolHoldings.Keys.ToList())
                        {
                            if (!posSymbols.Contains(key))
                            {
                                symbolHoldings[key] = 0;
                            }
                        }
                        simulatedRemainingBudget = remainingOtherBudget;
                    }

                    businessDayIndex++;
                    int cycleStep = (businessDayIndex - 1) % 4; // 0 = MUA, 1 = THEO DÕI/MUA GIA TĂNG, 2 = GỒNG LÃI/QUẢN TRỊ, 3 = BÁN CHỐT LỜI XOAY VÒNG
                    int stockIndex = ((businessDayIndex - 1) / 4) % candidateStocks.Count;
                    string symbol = candidateStocks[stockIndex];

                    var stockInfo = stocks.FirstOrDefault(s => s.Symbol == symbol);
                    decimal currentPrice = stockInfo?.CurrentPrice ?? 30000m;
                    if (currentPrice <= 0) currentPrice = 30000m;

                    int currentQtyHeld = symbolHoldings.ContainsKey(symbol) ? symbolHoldings[symbol] : 0;

                    // Khởi tạo bộ sinh số ngẫu nhiên mô phỏng quyết định AI
                    var rand = new Random(businessDayIndex + symbol.GetHashCode());
                    double allocationPct = 0.5; // Mặc định giải ngân 50% vốn mỗi lệnh
                    if (stockInfo != null)
                    {
                        if (stockInfo.Rsi < 40)
                            allocationPct = 0.8; // Quá bán -> mua mạnh
                        else if (stockInfo.Rsi > 60)
                            allocationPct = 0.3; // Quá mua nhẹ -> mua phòng thủ
                    }
                    allocationPct += (rand.NextDouble() * 0.2 - 0.1); // Biến động +/- 10% mô phỏng AI tối ưu
                    allocationPct = Math.Clamp(allocationPct, 0.2, 0.9);

                    decimal targetBudget = pref.AmountPerTrade * (decimal)allocationPct;
                    int qty = (int)(targetBudget / currentPrice / 10) * 10;
                    if (qty == 0) qty = 10;

                    // Khối lượng mua thêm (mô phỏng AI gom hàng từng phần)
                    double addAllocationPct = 0.3; // Mặc định mua thêm 30%
                    if (stockInfo != null)
                    {
                        if (stockInfo.Rsi < 40)
                            addAllocationPct = 0.5;
                        else if (stockInfo.Rsi > 60)
                            addAllocationPct = 0.15;
                    }
                    addAllocationPct += (rand.NextDouble() * 0.1 - 0.05); // Biến động +/- 5%
                    addAllocationPct = Math.Clamp(addAllocationPct, 0.1, 0.6);

                    decimal addBudget = pref.AmountPerTrade * (decimal)addAllocationPct;
                    int buyMoreQty = (int)(addBudget / currentPrice / 10) * 10;
                    if (buyMoreQty == 0) buyMoreQty = 10;

                    if (cycleStep == 0)
                    {
                        if (currentQtyHeld > 0)
                        {
                            var existingPos = positions.FirstOrDefault(p => p.Symbol == symbol);
                            decimal entryPrice = existingPos?.EntryPrice ?? currentPrice;
                            actionType = "GIỮ";
                            description = $"Nắm giữ vị thế thực tế hiện tại của **{symbol}** (Khối lượng đang có: **{currentQtyHeld:N0} CP**, giá vốn trung bình: **{entryPrice:N0} đ**). Tiếp tục gồng lãi hướng tới kháng cự.";
                            target = $"Giữ {currentQtyHeld:N0} CP {symbol}";
                        }
                        else
                        {
                            // Điều chỉnh số lượng mua dựa trên sức mua còn lại
                            decimal cost = qty * currentPrice;
                            if (simulatedRemainingBudget < cost)
                            {
                                qty = (int)(simulatedRemainingBudget / currentPrice / 10) * 10;
                            }

                            if (qty >= 10)
                            {
                                var buyMin = Math.Round(currentPrice * 0.985m / 100m) * 100m;
                                var buyMax = Math.Round(currentPrice * 1.005m / 100m) * 100m;
                                actionType = "MUA";
                                description = $"Giải ngân mua mới **{symbol}**. Đề xuất số lượng đặt lệnh: **{qty:N0} CP**, giá mua tích lũy vùng: **{buyMin:N0} - {buyMax:N0} đ** (khuyến nghị rải lệnh nhịp rung lắc). Sức mua khả dụng còn lại: {simulatedRemainingBudget - (qty * currentPrice):N0} đ.";
                                target = $"Mua {qty:N0} CP {symbol} giá {buyMax:N0} đ";
                                simulatedRemainingBudget -= qty * currentPrice;
                                symbolHoldings[symbol] = qty;
                            }
                            else
                            {
                                actionType = "THEO DÕI";
                                description = $"Theo dõi mã **{symbol}**. Do sức mua khả dụng còn lại ({simulatedRemainingBudget:N0} đ) thấp hơn số tiền tối thiểu để đặt lệnh 10 CP ({10 * currentPrice:N0} đ), không khuyến nghị giải ngân mới.";
                                target = $"Theo dõi {symbol}";
                            }
                        }
                    }
                    else if (cycleStep == 1)
                    {
                        if (currentQtyHeld == 0)
                        {
                            actionType = "THEO DÕI";
                            description = $"Theo dõi sát diễn biến giao dịch của mã **{symbol}** để tìm điểm giải ngân tối ưu khi thị trường thuận lợi.";
                            target = $"Theo dõi {symbol}";
                        }
                        else
                        {
                            // Điều chỉnh số lượng mua thêm dựa trên sức mua còn lại
                            decimal cost = buyMoreQty * currentPrice;
                            if (simulatedRemainingBudget < cost)
                            {
                                buyMoreQty = (int)(simulatedRemainingBudget / currentPrice / 10) * 10;
                            }

                            if (buyMoreQty >= 10)
                            {
                                var addPriceMin = Math.Round(currentPrice * 0.98m / 100m) * 100m;
                                var addPriceMax = Math.Round(currentPrice * 0.995m / 100m) * 100m;
                                actionType = "MUA THÊM";
                                description = $"Gia tăng tỷ trọng **{symbol}**. Đề xuất mua thêm: **{buyMoreQty:N0} CP**, giá gom vùng: **{addPriceMin:N0} - {addPriceMax:N0} đ** khi hỗ trợ kỹ thuật được giữ vững. Sức mua khả dụng còn lại: {simulatedRemainingBudget - (buyMoreQty * currentPrice):N0} đ.";
                                target = $"Mua thêm {buyMoreQty:N0} CP {symbol} giá {addPriceMax:N0} đ";
                                simulatedRemainingBudget -= buyMoreQty * currentPrice;
                                symbolHoldings[symbol] = currentQtyHeld + buyMoreQty;
                            }
                            else
                            {
                                actionType = "GIỮ";
                                description = $"Tiếp tục nắm giữ vị thế hiện tại của **{symbol}** (Khối lượng: **{currentQtyHeld:N0} CP**). Do sức mua khả dụng còn lại ({simulatedRemainingBudget:N0} đ) thấp hơn số tiền tối thiểu để mua thêm 10 CP ({10 * currentPrice:N0} đ), đề xuất tiếp tục nắm giữ theo dõi thay vì mua gia tăng.";
                                target = $"Giữ {currentQtyHeld:N0} CP {symbol}";
                            }
                        }
                    }
                    else if (cycleStep == 2)
                    {
                        if (currentQtyHeld > 0)
                        {
                            var stopLossPrice = Math.Round(currentPrice * (1 - (pref.MaxLossPercentage > 0 ? pref.MaxLossPercentage / 100m : 0.07m)) / 100m) * 100m;
                            actionType = "GIỮ";
                            description = $"Tiếp tục nắm giữ **{symbol}** (Tổng khối lượng: **{currentQtyHeld:N0} CP**). Đặt mốc cắt lỗ (Stop-loss) tại giá **{stopLossPrice:N0} đ** để bảo vệ vị thế.";
                            target = $"Giữ {currentQtyHeld:N0} CP {symbol} (SL: {stopLossPrice:N0} đ)";
                        }
                        else
                        {
                            actionType = "THEO DÕI";
                            description = $"Tiếp tục theo dõi biến động giá của **{symbol}** trong vùng tích lũy.";
                            target = $"Theo dõi {symbol}";
                        }
                    }
                    else // cycleStep == 3
                    {
                        if (currentQtyHeld > 0)
                        {
                            var sellMin = Math.Round(currentPrice * 1.035m / 100m) * 100m;
                            var sellMax = Math.Round(currentPrice * 1.05m / 100m) * 100m;
                            actionType = "BÁN";
                            currentProjectedPnL += profitPerCycle;
                            description = $"Bán chốt lời xoay vòng toàn bộ vị thế **{symbol}** (**{currentQtyHeld:N0} CP**). Giá bán chốt lời đề xuất vùng: **{sellMin:N0} - {sellMax:N0} đ**, chốt lời ngắn hạn: **+{profitPerCycle:N0} đ**. Thu hồi sức mua: +{currentQtyHeld * currentPrice:N0} đ.";
                            target = $"Bán {currentQtyHeld:N0} CP {symbol} giá {sellMin:N0} đ";
                            simulatedRemainingBudget += currentQtyHeld * currentPrice;
                            symbolHoldings[symbol] = 0;
                        }
                        else
                        {
                            actionType = "THEO DÕI";
                            description = $"Theo dõi sát nhịp hồi phục kỹ thuật của mã **{symbol}**.";
                            target = $"Theo dõi {symbol}";
                        }
                    }
                }

                // Nếu là ngày làm việc cuối cùng của chu kỳ kế hoạch, tiến hành tất toán tổng
                bool isLastBusinessDay = (date.Date == endDate.Date) || (date.Date.AddDays(1) > endDate.Date && date.DayOfWeek == DayOfWeek.Friday);
                if (isLastBusinessDay && date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    actionType = "TẤT TOÁN";
                    description = "Đáo hạn kế hoạch giao dịch chứng khoán. Tất toán toàn bộ các vị thế mở còn lại, thu hồi tiền mặt và chốt tổng kết quả kế hoạch để đạt mục tiêu.";
                    target = "Tất toán toàn bộ danh mục";
                }

                dailyCalendar.Add(new AITradingSystem.Models.DailyCalendarItem
                {
                    Date = date.ToString("dd/MM/yyyy"),
                    DayOfWeek = date.ToString("dddd"),
                    ActionType = actionType,
                    Description = description,
                    Target = target,
                    CumulativePnL = $"{currentProjectedPnL:N0} đ",
                    ActualAction = "-"
                });
            }

            var planResult = new GlobalPortfolioPlanResult
            {
                SuccessProbability = Math.Round(successProbability, 1),
                PlanSummary = $"Kế hoạch đầu tư tối ưu cho thời gian {totalDays} ngày từ {startDate:dd/MM/yyyy} đến {endDate:dd/MM/yyyy}. Mục tiêu tổng thể: {targetAmountToUse:N0} đ. Vốn đầu tư: {pref.AmountPerTrade:N0} đ. Hiện tại đã hoàn thành {targetProgressPercent(cumulativePnL, targetAmountToUse):N1}% mục tiêu.",
                Actions = actions,
                ExpectedContributions = expectedContributions,
                Rationale = $"Kế hoạch được lập dựa trên bối cảnh thị trường thực tế với vốn ban đầu {pref.AmountPerTrade:N0} đ và số ngày còn lại là {remainingDays} ngày. Phân bổ vốn tập trung vào các mã có xu hướng tốt và tuân thủ chặt chẽ nguyên tắc quản lý rủi ro.",
                DailyCalendar = dailyCalendar,
                StartDate = startDate,
                EndDate = endDate,
                RemainingDays = remainingDays
            };

            return JsonSerializer.Serialize(planResult);
        }

        private static decimal targetProgressPercent(decimal current, decimal target)
        {
            if (target <= 0) return 100m;
            return Math.Round(current / target * 100m, 2);
        }
    }

    public class OrderSignalResult
    {
        public string Action { get; set; } = "HOLD"; // BUY, SELL, HOLD
        public string StrategyApplied { get; set; } = "None";
        public string Rationale { get; set; } = string.Empty;
        public decimal TargetPrice { get; set; }
        public decimal StopLossPrice { get; set; }
    }
}
