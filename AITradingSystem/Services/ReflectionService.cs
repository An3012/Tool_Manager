using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using AITradingSystem.Models;
using AITradingSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AITradingSystem.Services
{
    public class ReflectionService
    {
        private readonly AppDbContext _context;
        private readonly Kernel _kernel;
        private readonly IConfiguration _configuration;

        public ReflectionService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;

            var builder = Kernel.CreateBuilder();
            _kernel = builder.Build();
        }

        // Tác tử Đánh giá & Phê bình (Critic/Reflection Agent)
        // Gọi định kỳ hoặc khi một vị thế được đóng
        public async Task<bool> ReflectOnClosedPositionAsync(int positionId)
        {
            try
            {
                var position = await _context.TradePositions.FindAsync(positionId);
                if (position == null || position.Status != "CLOSED") return false;

                var originalOrder = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Symbol == position.Symbol && o.OrderDate <= position.EntryDate);

                string outcomeType = position.PnL >= 0 ? "LÃI (Thành công)" : "LỖ (Thất bại)";
                decimal pnlPercentage = position.EntryPrice > 0 ? (position.PnL / (position.EntryPrice * position.Quantity)) * 100 : 0;

                var prompt = $@"
Bạn là Critic Agent (Tác tử Đánh giá & Rút kinh nghiệm) trong hệ thống AI Trading.
Nhiệm vụ của bạn là phân tích một giao dịch vừa đóng, so sánh lý do mua ban đầu với kết quả thực tế để rút ra một BÀI HỌC KINH NGHIỆM đắt giá lưu vào bộ nhớ dài hạn.

[THÔNG TIN GIAO DỊCH]
- Mã cổ phiếu: {position.Symbol}
- Giá mua (Entry): {position.EntryPrice:N0} đ (Ngày mua: {position.EntryDate})
- Giá bán (Exit): {position.ExitPrice:N0} đ (Ngày bán: {position.ExitDate})
- Kết quả: {outcomeType} với mức Lãi/Lỗ: {position.PnL:N0} đ ({pnlPercentage:N1}%)
- Lý do AI khuyến nghị mua ban đầu (Rationale): ""{originalOrder?.Rationale ?? "Không tìm thấy rationale gốc"}""

YÊU CẦU:
1. Đánh giá khách quan xem quyết định ban đầu đúng ở đâu, sai ở đâu.
2. Viết một BÀI HỌC KINH NGHIỆM cực kỳ ngắn gọn (tối đa 2 câu) để răn đe Trading Agent nếu đây là lệnh lỗ, hoặc củng cố chiến thuật nếu đây là lệnh lãi.
3. Không trả về nội dung thừa, chỉ trả về đoạn văn bài học kinh nghiệm trực tiếp.
";

                string lesson;
                if (string.IsNullOrEmpty(_configuration["AiConfig:ApiKey"]) || _configuration["AiConfig:ApiKey"] == "MOCK_KEY")
                {
                    lesson = SimulateCriticReflection(position.Symbol, position.PnL, originalOrder?.Rationale ?? "");
                }
                else
                {
                    var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                    var history = new ChatHistory();
                    history.AddUserMessage(prompt);
                    var response = await chatService.GetChatMessageContentAsync(history);
                    lesson = response.Content ?? string.Empty;
                }

                var episode = new TradeEpisode
                {
                    Id = Guid.NewGuid(),
                    MarketContext = $"Mã {position.Symbol}, Giá vào: {position.EntryPrice:N0}, Giá ra: {position.ExitPrice:N0}, Tỷ lệ: {pnlPercentage:N1}%",
                    ActionTaken = $"BUY -> SELL {position.Symbol}",
                    Rationale = originalOrder?.Rationale ?? "N/A",
                    Result = $"{outcomeType}: {position.PnL:N0} VND",
                    LessonLearned = lesson.Trim(),
                    Timestamp = DateTime.UtcNow
                };

                _context.TradeEpisodes.Add(episode);
                await UpsertKnowledgeEngineAsync(position, episode, originalOrder?.Rationale ?? string.Empty);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong quá trình tự phản biện: {ex.Message}");
                return false;
            }
        }

        // Tác tử Đánh giá & Phê bình (Critic/Reflection Agent) cho các vị thế AI TỰ HỌC
        public async Task<bool> ReflectOnClosedAiPositionAsync(int positionId)
        {
            try
            {
                var position = await _context.AiTradePositions!.FindAsync(positionId);
                if (position == null || position.Status != "CLOSED") return false;

                var originalOrder = await _context.AiOrders!
                    .FirstOrDefaultAsync(o => o.Symbol == position.Symbol && o.OrderDate <= position.EntryDate);

                string outcomeType = position.PnL >= 0 ? "LÃI (Thành công)" : "LỖ (Thất bại)";
                decimal pnlPercentage = position.EntryPrice > 0 ? (position.PnL / (position.EntryPrice * position.Quantity)) * 100 : 0;

                var prompt = $@"
Bạn là Critic Agent (Tác tử Đánh giá & Rút kinh nghiệm) trong hệ thống AI Trading.
Nhiệm vụ của bạn là phân tích một giao dịch tự học vừa đóng, so sánh lý do mua ban đầu với kết quả thực tế để rút ra một BÀI HỌC KINH NGHIỆM đắt giá lưu vào bộ nhớ dài hạn.

[THÔNG TIN GIAO DỊCH HỌC MÁY]
- Mã cổ phiếu: {position.Symbol}
- Giá mua (Entry): {position.EntryPrice:N0} đ (Ngày mua: {position.EntryDate})
- Giá bán (Exit): {position.ExitPrice:N0} đ (Ngày bán: {position.ExitDate})
- Kết quả: {outcomeType} với mức Lãi/Lỗ: {position.PnL:N0} đ ({pnlPercentage:N1}%)
- Lý do AI khuyến nghị mua ban đầu (Rationale): ""{originalOrder?.Rationale ?? "Không tìm thấy rationale gốc"}""

YÊU CẦU:
1. Đánh giá khách quan xem quyết định ban đầu đúng ở đâu, sai ở đâu.
2. Viết một BÀI HỌC KINH NGHIỆM cực kỳ ngắn gọn (tối đa 2 câu) để răn đe Trading Agent nếu đây là lệnh lỗ, hoặc củng cố chiến thuật nếu đây là lệnh lãi.
3. Không trả về nội dung thừa, chỉ trả về đoạn văn bài học kinh nghiệm trực tiếp.
";

                string lesson;
                if (string.IsNullOrEmpty(_configuration["AiConfig:ApiKey"]) || _configuration["AiConfig:ApiKey"] == "MOCK_KEY")
                {
                    lesson = SimulateCriticReflection(position.Symbol, position.PnL, originalOrder?.Rationale ?? "");
                }
                else
                {
                    var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                    var history = new ChatHistory();
                    history.AddUserMessage(prompt);
                    var response = await chatService.GetChatMessageContentAsync(history);
                    lesson = response.Content ?? string.Empty;
                }

                var episode = new AiTradeEpisode
                {
                    Id = Guid.NewGuid(),
                    MarketContext = $"Mã {position.Symbol}, Giá vào: {position.EntryPrice:N0}, Giá ra: {position.ExitPrice:N0}, Tỷ lệ: {pnlPercentage:N1}%",
                    ActionTaken = $"BUY -> SELL {position.Symbol}",
                    Rationale = originalOrder?.Rationale ?? "N/A",
                    Result = $"{outcomeType}: {position.PnL:N0} VND",
                    LessonLearned = lesson.Trim(),
                    Timestamp = DateTime.UtcNow
                };

                _context.AiTradeEpisodes!.Add(episode);
                await UpsertKnowledgeEngineAsync(position, episode, originalOrder?.Rationale ?? string.Empty);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong quá trình tự phản biện AI: {ex.Message}");
                return false;
            }
        }

        private async Task UpsertKnowledgeEngineAsync(TradePosition position, TradeEpisode episode, string originalRationale)
        {
            var analysisText = string.Join(" ",
                position.Symbol,
                episode.MarketContext,
                episode.ActionTaken,
                episode.Result,
                episode.LessonLearned,
                originalRationale).ToUpperInvariant();

            var isWin = position.PnL >= 0;
            var blueprint = BuildStrategyBlueprint(position.Symbol, analysisText, episode.LessonLearned, isWin);

            var strategy = await _context.TradingStrategies
                .FirstOrDefaultAsync(s => s.Name == blueprint.Name && s.StrategyType == blueprint.StrategyType);

            if (strategy == null)
            {
                strategy = new TradingStrategy
                {
                    Name = blueprint.Name,
                    StrategyType = blueprint.StrategyType,
                    Description = blueprint.Description,
                    RuleLogic = blueprint.RuleLogic,
                    IsAutoGenerated = true,
                    LearnCount = 1,
                    WinCount = isWin ? 1 : 0,
                    LossCount = isWin ? 0 : 1,
                    LastLearnedAt = DateTime.UtcNow
                };
                _context.TradingStrategies.Add(strategy);
                return;
            }

            strategy.Description = blueprint.Description;
            strategy.RuleLogic = blueprint.RuleLogic;
            strategy.IsAutoGenerated = true;
            strategy.LearnCount += 1;
            strategy.WinCount += isWin ? 1 : 0;
            strategy.LossCount += isWin ? 0 : 1;
            strategy.LastLearnedAt = DateTime.UtcNow;
            _context.TradingStrategies.Update(strategy);
        }

        private async Task UpsertKnowledgeEngineAsync(AiTradePosition position, AiTradeEpisode episode, string originalRationale)
        {
            var analysisText = string.Join(" ",
                position.Symbol,
                episode.MarketContext,
                episode.ActionTaken,
                episode.Result,
                episode.LessonLearned,
                originalRationale).ToUpperInvariant();

            var isWin = position.PnL >= 0;
            var blueprint = BuildStrategyBlueprint(position.Symbol, analysisText, episode.LessonLearned, isWin);

            var strategy = await _context.TradingStrategies!
                .FirstOrDefaultAsync(s => s.Name == blueprint.Name && s.StrategyType == blueprint.StrategyType);

            if (strategy == null)
            {
                strategy = new TradingStrategy
                {
                    Name = blueprint.Name,
                    StrategyType = blueprint.StrategyType,
                    Description = blueprint.Description,
                    RuleLogic = blueprint.RuleLogic,
                    IsAutoGenerated = true,
                    LearnCount = 1,
                    WinCount = isWin ? 1 : 0,
                    LossCount = isWin ? 0 : 1,
                    LastLearnedAt = DateTime.UtcNow
                };
                _context.TradingStrategies.Add(strategy);
                return;
            }

            strategy.Description = blueprint.Description;
            strategy.RuleLogic = blueprint.RuleLogic;
            strategy.IsAutoGenerated = true;
            strategy.LearnCount += 1;
            strategy.WinCount += isWin ? 1 : 0;
            strategy.LossCount += isWin ? 0 : 1;
            strategy.LastLearnedAt = DateTime.UtcNow;
            _context.TradingStrategies.Update(strategy);
        }

        private static StrategyBlueprint BuildStrategyBlueprint(string symbol, string analysisText, string lessonText, bool isWin)
        {
            if (analysisText.Contains("RSI") || lessonText.Contains("RSI"))
            {
                return new StrategyBlueprint
                {
                    Name = "[AUTO] RSI Rebound",
                    StrategyType = "Indicator",
                    Description = "Chiến lược tự học từ các giao dịch liên quan RSI và vùng quá bán/quá mua.",
                    RuleLogic = isWin
                        ? "Mua khi RSI hỗ trợ tín hiệu hồi phục và có xác nhận xu hướng; chốt lời theo mục tiêu."
                        : "Không mua sớm chỉ vì RSI thấp nếu xu hướng chung chưa xác nhận đảo chiều."
                };
            }

            if (analysisText.Contains("MACD") || lessonText.Contains("MACD") || analysisText.Contains("UPTREND"))
            {
                return new StrategyBlueprint
                {
                    Name = "[AUTO] Trend Following MACD",
                    StrategyType = "Trend",
                    Description = "Chiến lược tự học bám xu hướng, ưu tiên khi tín hiệu MACD và thị trường đồng thuận.",
                    RuleLogic = isWin
                        ? "Giữ theo xu hướng, tăng vị thế khi xu hướng còn khỏe và rủi ro trong ngưỡng."
                        : "Tránh đuổi giá khi xu hướng đã suy yếu hoặc tín hiệu xác nhận chưa đủ mạnh."
                };
            }

            if (analysisText.Contains("STOP LOSS") || analysisText.Contains("CẮT LỖ") || lessonText.Contains("LỖ"))
            {
                return new StrategyBlueprint
                {
                    Name = "[AUTO] Risk Control",
                    StrategyType = "Risk",
                    Description = "Chiến lược tự học tập trung vào quản trị rủi ro và kỷ luật cắt lỗ.",
                    RuleLogic = isWin
                        ? "Giữ kỷ luật stop loss và giảm khối lượng nếu biến động tăng bất thường."
                        : "Ưu tiên cắt lỗ đúng ngưỡng, không bình quân giá khi tín hiệu xấu chưa đảo chiều."
                };
            }

            if (analysisText.Contains("CHỐT LỜI") || analysisText.Contains("TAKE PROFIT") || analysisText.Contains("LÃI"))
            {
                return new StrategyBlueprint
                {
                    Name = "[AUTO] Profit Taking Ladder",
                    StrategyType = "Profit",
                    Description = "Chiến lược tự học về chốt lời từng phần và tái phân bổ vốn.",
                    RuleLogic = isWin
                        ? "Chốt lời từng phần khi đạt mục tiêu và giữ lại một phần nhỏ để quay vòng vốn."
                        : "Nếu chưa đạt mục tiêu nhưng đã vào vùng rủi ro, siết chặt vị thế thay vì giữ quá lâu."
                };
            }

            return new StrategyBlueprint
            {
                Name = $"[AUTO] Position Management - {symbol}",
                StrategyType = "Position",
                Description = "Chiến lược tự học từ quản trị vị thế và phân bổ vốn theo giao dịch thực tế.",
                RuleLogic = isWin
                    ? "Cải thiện cách phân bổ vốn, giữ kỷ luật chốt lời và tái đầu tư có kiểm soát."
                    : "Giảm rủi ro bằng cách chờ xác nhận tốt hơn trước khi gia tăng vị thế."
            };
        }

        private string SimulateCriticReflection(string symbol, decimal pnl, string originalRationale)
        {
            if (pnl >= 0)
            {
                return $"[Bài học thành công] Giao dịch {symbol} thành công nhờ tuân thủ nguyên tắc mua ở vùng quá bán RSI và chốt lời đúng hạn mức kỳ vọng. Tiếp tục củng cố mô hình này.";
            }

            return $"[Bài học thất bại] Giao dịch {symbol} bị lỗ do vội vã mua khi chỉ số RSI ở vùng quá bán nhưng xu hướng thị trường chung đang giảm mạnh. Rút kinh nghiệm: Chỉ mua khi có thêm xác nhận đảo chiều từ MACD.";
        }

        private sealed class StrategyBlueprint
        {
            public string Name { get; set; } = string.Empty;
            public string StrategyType { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string RuleLogic { get; set; } = string.Empty;
        }
    }
}
