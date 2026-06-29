using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AITradingSystem.Models
{
    /// <summary>
    /// Lưu trữ các dự đoán kế hoạch từ AI model
    /// Được tạo khi người dùng khởi chạy AI Planning
    /// </summary>
    public class AiPlanPrediction
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID của InvestmentPlan được tạo từ dự đoán này (nullable)
        /// </summary>
        [ForeignKey("InvestmentPlan")]
        public int? InvestmentPlanId { get; set; }
        public virtual InvestmentPlan InvestmentPlan { get; set; }

        /// <summary>
        /// Thời điểm AI tạo dự đoán
        /// </summary>
        public DateTime PredictionDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Mục tiêu lợi nhuận do người dùng đưa ra
        /// </summary>
        public decimal TargetProfit { get; set; }

        /// <summary>
        /// Vốn khởi động
        /// </summary>
        public decimal Capital { get; set; }

        /// <summary>
        /// Deadline (ngày kết thúc dự kiến)
        /// </summary>
        public DateTime DeadlineDate { get; set; }

        /// <summary>
        /// Độ chấp nhận rủi ro (Low/Medium/High)
        /// </summary>
        public string RiskTolerance { get; set; } = "Medium";

        /// <summary>
        /// Kế hoạch chi tiết được AI dự đoán (JSON format)
        /// </summary>
        public string PredictedPlanJson { get; set; } = string.Empty;

        /// <summary>
        /// Xác suất thành công theo AI (0-100%)
        /// </summary>
        public decimal SuccessProbability { get; set; }

        /// <summary>
        /// Phân tích lý do (tiếng Việt) từ AI
        /// </summary>
        public string ReasoningAnalysis { get; set; } = string.Empty;

        /// <summary>
        /// Danh sách các chiến lược được AI đề xuất (JSON array)
        /// </summary>
        public string RecommendedStrategiesJson { get; set; } = string.Empty;

        /// <summary>
        /// Các rủi ro được AI xác định (JSON array)
        /// </summary>
        public string IdentifiedRisksJson { get; set; } = string.Empty;

        /// <summary>
        /// Số ngày dự kiến để đạt mục tiêu
        /// </summary>
        public int EstimatedDaysToTarget { get; set; }

        /// <summary>
        /// Trạng thái của dự đoán (Created/Approved/Implemented/Completed/Failed)
        /// </summary>
        public string PredictionStatus { get; set; } = "Created";

        /// <summary>
        /// Mô tả chi tiết về kế hoạch
        /// </summary>
        public string DetailedDescription { get; set; } = string.Empty;

        /// <summary>
        /// Phiên bản model AI được sử dụng (ví dụ: v1.0, v1.1, etc.)
        /// </summary>
        public string AiModelVersion { get; set; } = "v1.0";

        /// <summary>
        /// Metadata bổ sung (JSON format) cho việc learn từ dự đoán
        /// </summary>
        public string MetadataJson { get; set; } = string.Empty;
    }
}
