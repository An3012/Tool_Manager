using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using AITradingSystem.Models;
using AITradingSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
Bạn là AI Trading Copilot chuyên nghiệp tối ưu riêng cho thị trường chứng khoán Việt Nam.
Nhiệm vụ của bạn là đưa ra tín hiệu cố vấn giao dịch (MUA, BÁN, hoặc THEO DÕI) dựa trên bối cảnh thị trường, mục tiêu của người dùng, thư viện chiến lược (Knowledge) và các bài học kinh nghiệm trong quá khứ (Memory).

[MỤC TIÊU CỦA NGƯỜI DÙNG]
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
1. Nếu giá hiện tại đạt hoặc vượt mốc chốt lời, đề xuất hành động phải là BÁN (SELL) với lý do chốt lời bảo vệ thành quả.
2. Nếu giá hiện tại chạm hoặc giảm dưới mốc cắt lỗ, đề xuất hành động phải là BÁN (SELL) với lý do cắt lỗ quản trị rủi ro.
3. Nếu chưa chạm các mốc mục tiêu riêng biệt đặt ra, hãy kết hợp chỉ báo kỹ thuật để đưa ra tín hiệu MUA (BUY) hoặc THEO DÕI (HOLD).

Vui lòng trả về kết quả dưới định dạng JSON như sau:
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
