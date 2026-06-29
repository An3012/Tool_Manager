using System;
using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    /// <summary>
    /// Lưu trữ mục tiêu giao dịch được người dùng cài đặt để AI học hỏi và phân tích
    /// </summary>
    public class UserAiGoal
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Thời điểm người dùng cài đặt mục tiêu
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Vốn khởi động (VND)
        /// </summary>
        public decimal InitialCapital { get; set; }

        /// <summary>
        /// Mục tiêu lợi nhuận (VND)
        /// </summary>
        public decimal TargetProfit { get; set; }

        /// <summary>
        /// Deadline - ngày hết hạn để đạt mục tiêu
        /// </summary>
        public DateTime Deadline { get; set; }

        /// <summary>
        /// Độ chấp nhận rủi ro (Low/Medium/High)
        /// </summary>
        public string RiskTolerance { get; set; } = "Medium";

        /// <summary>
        /// Chiến lược ưu tiên (Aggressive/Balanced/Conservative)
        /// </summary>
        public string PreferredStrategy { get; set; } = "Balanced";

        /// <summary>
        /// Các cổ phiếu được ưu tiên (JSON array)
        /// </summary>
        public string PreferredStocks { get; set; } = string.Empty;

        /// <summary>
        /// Các cổ phiếu cấm (JSON array)
        /// </summary>
        public string RestrictedStocks { get; set; } = string.Empty;

        /// <summary>
        /// Mức lợi nhuận tối thiểu chấp nhận được hàng ngày (%)
        /// </summary>
        public decimal? MinDailyReturnPct { get; set; }

        /// <summary>
        /// Mức lỗ tối đa chấp nhận được hàng ngày (%)
        /// </summary>
        public decimal? MaxDailyLossPct { get; set; }

        /// <summary>
        /// Số lượng vị thế tối đa được phép giữ
        /// </summary>
        public int? MaxOpenPositions { get; set; }

        /// <summary>
        /// Trạng thái mục tiêu (Active/Paused/Completed/Failed)
        /// </summary>
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Mô tả chi tiết về mục tiêu
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Lợi nhuận thực tế đạt được cho đến nay (VND)
        /// </summary>
        public decimal? ActualProfitToDate { get; set; }

        /// <summary>
        /// Tiến độ hoàn thành (%)
        /// </summary>
        public decimal ProgressPercentage { get; set; }

        /// <summary>
        /// Số ngày còn lại để đạt mục tiêu
        /// </summary>
        public int DaysRemaining { get; set; }

        /// <summary>
        /// Metadata bổ sung (JSON)
        /// </summary>
        public string MetadataJson { get; set; } = string.Empty;

        /// <summary>
        /// Ghi chú riêng
        /// </summary>
        public string Notes { get; set; } = string.Empty;
    }
}
