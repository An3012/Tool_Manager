using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AITradingSystem.Models
{
    /// <summary>
    /// Lưu trữ kiến thức học máy từ quá trình giao dịch và dự đoán AI
    /// Được sử dụng để nâng cấp và phát triển model AI
    /// </summary>
    public class AiLearningKnowledge
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID của dự đoán kế hoạch liên quan
        /// </summary>
        [ForeignKey("AiPlanPrediction")]
        public int? AiPlanPredictionId { get; set; }
        public virtual AiPlanPrediction AiPlanPrediction { get; set; }

        /// <summary>
        /// ID của TradeEpisode kết quả từ dự đoán này
        /// </summary>
        [ForeignKey("TradeEpisode")]
        public Guid? TradeEpisodeId { get; set; }
        public virtual TradeEpisode TradeEpisode { get; set; }

        /// <summary>
        /// Loại kiến thức (Strategy/RiskPattern/MarketCondition/TradeExecution/ProfitTaking/LossCutting)
        /// </summary>
        public string KnowledgeType { get; set; } = string.Empty;

        /// <summary>
        /// Thời điểm kiến thức được ghi nhận
        /// </summary>
        public DateTime RecordedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Điều kiện thị trường khi kiến thức này được áp dụng (JSON)
        /// </summary>
        public string MarketConditionsJson { get; set; } = string.Empty;

        /// <summary>
        /// Các input (dữ liệu đầu vào) dẫn đến quyết định này (JSON)
        /// </summary>
        public string InputFeatures { get; set; } = string.Empty;

        /// <summary>
        /// Quyết định/Hành động được thực hiện
        /// </summary>
        public string Decision { get; set; } = string.Empty;

        /// <summary>
        /// Kết quả thực tế (thành công/thất bại + giá trị)
        /// </summary>
        public string ActualOutcome { get; set; } = string.Empty;

        /// <summary>
        /// Kết quả dự đoán của AI (trong dự đoán kế hoạch)
        /// </summary>
        public string PredictedOutcome { get; set; } = string.Empty;

        /// <summary>
        /// Độ chính xác của dự đoán (0-100%)
        /// </summary>
        public decimal AccuracyScore { get; set; }

        /// <summary>
        /// Bài học được rút ra (tiếng Việt)
        /// </summary>
        public string LessonLearned { get; set; } = string.Empty;

        /// <summary>
        /// Trọng số của kiến thức này trong huấn luyện (0-1)
        /// </summary>
        public decimal WeightScore { get; set; } = 1.0m;

        /// <summary>
        /// Cấp độ xác thực kiến thức (Unverified/Verified/HighConfidence)
        /// </summary>
        public string VerificationLevel { get; set; } = "Unverified";

        /// <summary>
        /// Số lần kiến thức này được áp dụng thành công
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Số lần kiến thức này được áp dụng thất bại
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Thống kê hiệu quả (JSON) - tỷ lệ thắng/thua, lợi nhuận trung bình, etc.
        /// </summary>
        public string PerformanceStatsJson { get; set; } = string.Empty;

        /// <summary>
        /// Metadata bổ sung (JSON)
        /// </summary>
        public string MetadataJson { get; set; } = string.Empty;

        /// <summary>
        /// Có được sử dụng trong việc huấn luyện model lần tới không?
        /// </summary>
        public bool IsUsedForTraining { get; set; } = true;

        /// <summary>
        /// Ghi chú từ người dùng hoặc admin
        /// </summary>
        public string Notes { get; set; } = string.Empty;
    }
}
