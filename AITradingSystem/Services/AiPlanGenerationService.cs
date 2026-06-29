using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using AITradingSystem.Models;
using AITradingSystem.Data;

namespace AITradingSystem.Services
{
    /// <summary>
    /// Dịch vụ sinh thành kế hoạch giao dịch tối ưu từ mục tiêu của người dùng
    /// Sử dụng AI để dự đoán, phân tích, và lưu kiến thức để huấn luyện model
    /// </summary>
    public class AiPlanGenerationService
    {
        private readonly AppDbContext _context;
        private readonly Kernel _kernel;
        private readonly IConfiguration _configuration;

        public AiPlanGenerationService(AppDbContext context, Kernel kernel, IConfiguration configuration)
        {
            _context = context;
            _kernel = kernel;
            _configuration = configuration;
        }

        /// <summary>
        /// Tạo dự đoán kế hoạch từ mục tiêu người dùng
        /// </summary>
        public async Task<AiPlanPrediction> GeneratePlanPredictionAsync(
            decimal capital,
            decimal targetProfit,
            DateTime deadline,
            string riskTolerance = "Medium",
            string preferredStrategy = "Balanced")
        {
            try
            {
                // 1. Tạo UserAiGoal từ input
                var userGoal = await CreateUserGoalAsync(capital, targetProfit, deadline, riskTolerance, preferredStrategy);

                // 2. Phân tích điều kiện thị trường hiện tại
                var marketAnalysis = await AnalyzeMarketConditionsAsync();

                // 3. Lấy lịch sử giao dịch thành công/thất bại
                var historicalLessons = await GetHistoricalLessonsAsync();

                // 4. Gọi AI để tạo kế hoạch
                var aiPrediction = await CallAiForPlanPredictionAsync(
                    userGoal,
                    marketAnalysis,
                    historicalLessons);

                // 5. Lưu dự đoán vào database
                _context.AiPlanPredictions?.Add(aiPrediction);
                await _context.SaveChangesAsync();

                // 6. Lưu kiến thức từ dự đoán vào AiLearningKnowledge
                await SavePredictionAsKnowledgeAsync(aiPrediction, userGoal, marketAnalysis);

                return aiPrediction;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating plan prediction: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Tạo hoặc cập nhật UserAiGoal
        /// </summary>
        private async Task<UserAiGoal> CreateUserGoalAsync(
            decimal capital,
            decimal targetProfit,
            DateTime deadline,
            string riskTolerance,
            string preferredStrategy)
        {
            var userGoal = new UserAiGoal
            {
                CreatedDate = DateTime.UtcNow,
                InitialCapital = capital,
                TargetProfit = targetProfit,
                Deadline = deadline,
                RiskTolerance = riskTolerance,
                PreferredStrategy = preferredStrategy,
                Status = "Active",
                DaysRemaining = (deadline.Date - DateTime.Today).Days,
                ProgressPercentage = 0m
            };

            _context.UserAiGoals?.Add(userGoal);
            await _context.SaveChangesAsync();

            return userGoal;
        }

        /// <summary>
        /// Phân tích điều kiện thị trường hiện tại
        /// </summary>
        private async Task<Dictionary<string, object>> AnalyzeMarketConditionsAsync()
        {
            var analysis = new Dictionary<string, object>();

            try
            {
                // Lấy thống kê từ StockTransactions gần đây
                var recentTransactions = await _context.StockTransactions?
                    .Where(t => t.TransactionDate >= DateTime.Today.AddDays(-30))
                    .ToListAsync() ?? new List<StockTransaction>();

                var positiveCount = recentTransactions.Count(t => t.PnlAmount > 0);
                var totalCount = recentTransactions.Count;

                analysis["recent_win_rate"] = totalCount > 0 ? (decimal)positiveCount / totalCount * 100 : 0m;
                analysis["recent_transactions_count"] = totalCount;
                analysis["market_trend"] = positiveCount > totalCount / 2 ? "UPTREND" : "DOWNTREND";

                // Lấy chiến lược được sử dụng thành công gần đây
                var successfulStrategies = await _context.TradingStrategies?
                    .OrderByDescending(s => s.WinCount)
                    .Take(5)
                    .ToListAsync() ?? new List<TradingStrategy>();

                analysis["available_strategies"] = successfulStrategies
                    .Select(s => new { s.Name, s.StrategyType, s.Description })
                    .ToList();

                // Lấy mực độ rủi ro hiện tại
                var openPositions = await _context.TradePositions?
                    .Where(p => p.Status == "OPEN")
                    .ToListAsync() ?? new List<TradePosition>();

                analysis["open_positions_count"] = openPositions.Count;
                analysis["current_risk_level"] = openPositions.Count > 10 ? "HIGH" :
                    (openPositions.Count > 5 ? "MEDIUM" : "LOW");

                return analysis;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing market conditions: {ex.Message}");
                return analysis;
            }
        }

        /// <summary>
        /// Lấy bài học từ giao dịch lịch sử
        /// </summary>
        private async Task<List<AiLearningKnowledge>> GetHistoricalLessonsAsync()
        {
            try
            {
                var lessons = await _context.AiLearningKnowledge?
                    .Where(k => k.VerificationLevel == "Verified" || k.VerificationLevel == "HighConfidence")
                    .OrderByDescending(k => k.SuccessCount - k.FailureCount)
                    .Take(10)
                    .ToListAsync() ?? new List<AiLearningKnowledge>();

                return lessons;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting historical lessons: {ex.Message}");
                return new List<AiLearningKnowledge>();
            }
        }

        /// <summary>
        /// Gọi AI model để tạo dự đoán kế hoạch
        /// </summary>
        private async Task<AiPlanPrediction> CallAiForPlanPredictionAsync(
            UserAiGoal userGoal,
            Dictionary<string, object> marketAnalysis,
            List<AiLearningKnowledge> historicalLessons)
        {
            var prediction = new AiPlanPrediction
            {
                PredictionDate = DateTime.UtcNow,
                Capital = userGoal.InitialCapital,
                TargetProfit = userGoal.TargetProfit,
                DeadlineDate = userGoal.Deadline,
                RiskTolerance = userGoal.RiskTolerance,
                EstimatedDaysToTarget = userGoal.DaysRemaining,
                PredictionStatus = "Created",
                AiModelVersion = "v1.0"
            };

            try
            {
                // 1. Tạo prompt cho AI
                var prompt = BuildAiPrompt(userGoal, marketAnalysis, historicalLessons);

                // 2. Gọi AI model (Semantic Kernel) - mô phỏng
                var aiResponse = await CallSemanticKernelAsync(prompt);

                // 3. Phân tích kết quả
                prediction.ReasoningAnalysis = ExtractReasoningFromResponse(aiResponse);
                prediction.SuccessProbability = CalculateSuccessProbability(userGoal, marketAnalysis);
                prediction.PredictedPlanJson = GeneratePredictedPlanJson(userGoal, marketAnalysis);
                prediction.RecommendedStrategiesJson = ExtractStrategiesJson(aiResponse);
                prediction.IdentifiedRisksJson = ExtractRisksJson(userGoal, marketAnalysis);
                prediction.DetailedDescription = ExtractDetailedDescription(aiResponse);

                return prediction;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling AI for prediction: {ex.Message}");

                // Fallback to basic prediction
                prediction.ReasoningAnalysis = $"Kế hoạch tối ưu dựa trên phân tích cơ bản";
                prediction.SuccessProbability = 65m;
                prediction.PredictedPlanJson = GeneratePredictedPlanJson(userGoal, marketAnalysis);

                return prediction;
            }
        }

        /// <summary>
        /// Xây dựng prompt cho AI
        /// </summary>
        private string BuildAiPrompt(
            UserAiGoal userGoal,
            Dictionary<string, object> marketAnalysis,
            List<AiLearningKnowledge> historicalLessons)
        {
            var prompt = $@"
Bạn là một chuyên gia giao dịch chứng chỉ AI. Tôi cần bạn tạo một kế hoạch giao dịch tối ưu.

**MỤC TIÊU:**
- Vốn khởi động: {userGoal.InitialCapital:N0} VND
- Mục tiêu lợi nhuận: {userGoal.TargetProfit:N0} VND
- Deadline: {userGoal.Deadline:dd/MM/yyyy} (còn {userGoal.DaysRemaining} ngày)
- Độ chấp nhận rủi ro: {userGoal.RiskTolerance}
- Chiến lược ưu tiên: {userGoal.PreferredStrategy}

**PHÂN TÍCH THỊ TRƯỜNG HIỆN TẠI:**
- Tỷ lệ thắng gần đây: {marketAnalysis.GetValueOrDefault("recent_win_rate", 0)}%
- Xu hướng thị trường: {marketAnalysis.GetValueOrDefault("market_trend", "UNKNOWN")}
- Mức rủi ro hiện tại: {marketAnalysis.GetValueOrDefault("current_risk_level", "UNKNOWN")}
- Vị thế mở: {marketAnalysis.GetValueOrDefault("open_positions_count", 0)}

**BÀI HỌC TỪ LỊCH SỬ:**
{string.Join("\n", historicalLessons.Take(5).Select(l => $"- {l.LessonLearned} (Đúng {l.SuccessCount} lần, sai {l.FailureCount} lần)"))}

Vui lòng cung cấp:
1. Xác suất thành công của kế hoạch (%)
2. Các chiến lược được đề xuất (JSON array)
3. Các rủi ro được xác định (JSON array)
4. Phân tích chi tiết (tiếng Việt)

Trả lời dưới dạng JSON để dễ phân tích.
";
            return prompt;
        }

        /// <summary>
        /// Gọi Semantic Kernel (mô phỏng)
        /// </summary>
        private async Task<string> CallSemanticKernelAsync(string prompt)
        {
            try
            {
                // TODO: Thay thế bằng thực tế gọi AI model
                // var result = await _kernel.InvokeAsync<string>(/* plugin function */);

                // Mô phỏng response
                var response = @"{
                    ""success_probability"": 72,
                    ""strategies"": [
                        ""Momentum Trading - Mua cổ phiếu tăng mạnh, bán sau 2-3 ngày"",
                        ""Value Trading - Tìm cổ phiếu bị định giá thấp so với giá trị nội tại"",
                        ""Dividend Harvesting - Mua trước ngày cộng tỷ suất cổ tức""
                    ],
                    ""risks"": [
                        ""Rủi ro thị trường - Nếu thị trường giảm, tất cả vị thế sẽ bị lỗ"",
                        ""Rủi ro thanh khoản - Một số cổ phiếu có thể không mua/bán được dễ dàng"",
                        ""Rủi ro tâm lý - Quyết định sai khi sợ hãi hoặc tham lam""
                    ],
                    ""analysis"": ""Dựa trên phân tích thị trường hiện tại, lợi suất kỳ vọng là cao. Tuy nhiên, cần quản lý rủi ro cẩn thận.""
                }";

                return await Task.FromResult(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Semantic Kernel: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// Tính toán xác suất thành công dựa trên dữ liệu
        /// </summary>
        private decimal CalculateSuccessProbability(
            UserAiGoal userGoal,
            Dictionary<string, object> marketAnalysis)
        {
            decimal probability = 50m; // Base probability

            // Điều chỉnh dựa trên tỷ lệ thắng gần đây
            if (marketAnalysis.TryGetValue("recent_win_rate", out var winRate) && winRate is decimal wr)
            {
                probability = 50m + (wr * 0.3m); // +30% nếu win rate cao
            }

            // Điều chỉnh dựa trên độ chấp nhận rủi ro
            if (userGoal.RiskTolerance == "High")
                probability += 5m;
            else if (userGoal.RiskTolerance == "Low")
                probability -= 5m;

            // Điều chỉnh dựa trên thời hạn
            if (userGoal.DaysRemaining > 30)
                probability += 10m;
            else if (userGoal.DaysRemaining < 5)
                probability -= 15m;

            return Math.Min(99m, Math.Max(10m, probability));
        }

        /// <summary>
        /// Sinh kế hoạch dự đoán dưới dạng JSON
        /// </summary>
        private string GeneratePredictedPlanJson(
            UserAiGoal userGoal,
            Dictionary<string, object> marketAnalysis)
        {
            var plan = new
            {
                phases = new object[]
                {
                    new {
                        phase = 1,
                        name = "Thiết lập vị thế cơ bản",
                        duration_days = 3,
                        description = "Xác định 3-5 cổ phiếu mục tiêu, chia vốn thành 3 phần",
                        actions = new[] {
                            "Phân tích kỹ thuật để xác định điểm vào tốt",
                            "Mở vị thế cơ bản với 50% số tiền dự kiến"
                        }
                    },
                    new {
                        phase = 2,
                        name = "Tối ưu hóa vị thế",
                        duration_days = 10,
                        description = "Thêm vị thế nếu trendúng lên, bán cắt lỗ nếu cần",
                        actions = new[] {
                            "Theo dõi hàng ngày, điều chỉnh Stop Loss",
                            "Mua thêm nếu giá hỗ trợ tốt, bán một phần để khóa lợi nhuận"
                        }
                    },
                    new {
                        phase = 3,
                        name = "Chốt lãi",
                        duration_days = 5,
                        description = "Đạt mục tiêu lợi nhuận hoặc deadline",
                        actions = new[] {
                            "Tất toán nếu đạt lợi nhuận mục tiêu",
                            "Cắt lỗ toàn bộ nếu deadline sắp tới"
                        }
                    }
                },
                expected_daily_return = (userGoal.TargetProfit / userGoal.InitialCapital / userGoal.DaysRemaining * 100).ToString("F2"),
                recommended_max_positions = Math.Min(10, Math.Max(3, (int)(userGoal.InitialCapital / 5000000)))
            };

            return JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Trích chiến lược từ AI response
        /// </summary>
        private string ExtractStrategiesJson(string aiResponse)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(aiResponse))
                {
                    if (doc.RootElement.TryGetProperty("strategies", out var strategies))
                    {
                        return strategies.GetRawText();
                    }
                }
            }
            catch { }

            return JsonSerializer.Serialize(new[] {
                "Momentum Trading",
                "Value Trading",
                "Dividend Harvesting"
            });
        }

        /// <summary>
        /// Trích rủi ro từ AI response
        /// </summary>
        private string ExtractRisksJson(
            UserAiGoal userGoal,
            Dictionary<string, object> marketAnalysis)
        {
            var risks = new List<string>
            {
                "Rủi ro thị trường - Thị trường giảm đột ngột",
                "Rủi ro thanh khoản - Không bán được kịp thời",
                "Rủi ro tâm lý - Quyết định cảm xúc"
            };

            if (userGoal.DaysRemaining < 5)
                risks.Add("Rủi ro thời gian - Deadline sắp đến, áp lực cao");

            if (userGoal.RiskTolerance == "Low")
                risks.Add("Rủi ro bảo thủ - Chiến lược quá an toàn, khó đạt mục tiêu");

            return JsonSerializer.Serialize(risks);
        }

        /// <summary>
        /// Trích mô tả chi tiết từ AI response
        /// </summary>
        private string ExtractDetailedDescription(string aiResponse)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(aiResponse))
                {
                    if (doc.RootElement.TryGetProperty("analysis", out var analysis))
                    {
                        return analysis.GetString() ?? "Kế hoạch tối ưu đã được tạo";
                    }
                }
            }
            catch { }

            return "Dựa trên phân tích thị trường và bài học lịch sử, kế hoạch này được thiết kế để tối đa hóa lợi nhuận trong khi quản lý rủi ro.";
        }

        /// <summary>
        /// Trích reasoning từ AI response
        /// </summary>
        private string ExtractReasoningFromResponse(string aiResponse)
        {
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(aiResponse))
                {
                    if (doc.RootElement.TryGetProperty("analysis", out var reasoning))
                    {
                        return reasoning.GetString() ?? "Phân tích được tạo từ dữ liệu thị trường";
                    }
                }
            }
            catch { }

            return "Phân tích được tạo từ dữ liệu thị trường hiện tại và bài học lịch sử giao dịch";
        }

        /// <summary>
        /// Lưu dự đoán thành kiến thức để huấn luyện
        /// </summary>
        private async Task SavePredictionAsKnowledgeAsync(
            AiPlanPrediction prediction,
            UserAiGoal userGoal,
            Dictionary<string, object> marketAnalysis)
        {
            try
            {
                var knowledge = new AiLearningKnowledge
                {
                    AiPlanPredictionId = prediction.Id,
                    KnowledgeType = "PlanGeneration",
                    RecordedDate = DateTime.UtcNow,
                    MarketConditionsJson = JsonSerializer.Serialize(marketAnalysis),
                    InputFeatures = JsonSerializer.Serialize(new {
                        capital = userGoal.InitialCapital,
                        targetProfit = userGoal.TargetProfit,
                        daysRemaining = userGoal.DaysRemaining,
                        riskTolerance = userGoal.RiskTolerance
                    }),
                    Decision = "Generated AI plan with " + prediction.SuccessProbability + "% success probability",
                    PredictedOutcome = "Awaiting execution",
                    AccuracyScore = 0m, // Sẽ được cập nhật sau khi kế hoạch hoàn thành
                    VerificationLevel = "Unverified",
                    IsUsedForTraining = true,
                    Notes = $"Plan generated for target profit: {userGoal.TargetProfit:N0} VND by {userGoal.Deadline:dd/MM/yyyy}"
                };

                _context.AiLearningKnowledge?.Add(knowledge);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving prediction as knowledge: {ex.Message}");
            }
        }

        /// <summary>
        /// Cập nhật kiến thức khi kế hoạch hoàn thành
        /// </summary>
        public async Task<AiLearningKnowledge> UpdateKnowledgeWithResultAsync(
            int predictionId,
            string actualOutcome,
            decimal actualProfit,
            bool isSuccessful)
        {
            try
            {
                var prediction = await (_context.AiPlanPredictions?.FirstOrDefaultAsync(p => p.Id == predictionId) ?? Task.FromResult<AiPlanPrediction>(null));
                if (prediction == null)
                    throw new ArgumentException($"Prediction with ID {predictionId} not found");

                var knowledge = await _context.AiLearningKnowledge?
                    .FirstOrDefaultAsync(k => k.AiPlanPredictionId == predictionId);

                if (knowledge != null)
                {
                    knowledge.ActualOutcome = actualOutcome;
                    knowledge.AccuracyScore = isSuccessful ? 100m : 50m;
                    knowledge.VerificationLevel = isSuccessful ? "Verified" : "PartiallyVerified";

                    if (isSuccessful)
                        knowledge.SuccessCount++;
                    else
                        knowledge.FailureCount++;

                    knowledge.PerformanceStatsJson = JsonSerializer.Serialize(new {
                        actualProfit = actualProfit,
                        isSuccessful = isSuccessful,
                        completedDate = DateTime.UtcNow
                    });

                    _context.AiLearningKnowledge?.Update(knowledge);
                    await _context.SaveChangesAsync();

                    return knowledge;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating knowledge with result: {ex.Message}");
            }

            return null;
        }
    }
}
