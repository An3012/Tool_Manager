using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    public class UserPreference
    {
        [Key]
        public int Id { get; set; }

        // Mục tiêu đầu tư (ngắn hạn, trung hạn, dài hạn)
        public string InvestmentHorizon { get; set; } = "Short-term";

        // Mức lợi nhuận kỳ vọng (%)
        public decimal TargetProfitPercentage { get; set; }

        // Mức cắt lỗ tối đa cho phép (%)
        public decimal MaxLossPercentage { get; set; }

        // Số tiền đầu tư mỗi lệnh (VD: 5,000,000 VND)
        public decimal AmountPerTrade { get; set; }

        // Số tiền mục tiêu mong muốn đạt được (VD: 10,000,000 VND)
        public decimal TargetAmount { get; set; }

        // Số tiền chốt lời mong muốn cho mỗi lệnh (VND)
        public decimal TakeProfitAmount { get; set; }

        // Số tiền cắt lỗ mong muốn cho mỗi lệnh (VND)
        public decimal StopLossAmount { get; set; }

        // Sở thích rủi ro (Low, Medium, High)
        public string RiskTolerance { get; set; } = "Medium";

        // THÔNG TIN ĐĂNG NHẬP DNSE/DNSE ĐỂ ĐỒNG BỘ
        public string DnseUsername { get; set; } = string.Empty;
        public string DnsePassword { get; set; } = string.Empty;
        public string DnseToken { get; set; } = string.Empty;
    }
}
