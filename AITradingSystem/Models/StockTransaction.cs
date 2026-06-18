using System;
using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    /// <summary>
    /// Lưu chi tiết từng lệnh MUA/BÁN riêng lẻ (không phải vị thế tổng hợp).
    /// Giúp AI phân tích timing: bán sớm/muộn thì lời/lỗ thế nào.
    /// </summary>
    public class StockTransaction
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Mã cổ phiếu (VD: POW, VCG)</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>Loại giao dịch: BUY hoặc SELL</summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>Số lượng cổ phiếu giao dịch</summary>
        public int Quantity { get; set; }

        /// <summary>Giá khớp lệnh</summary>
        public decimal Price { get; set; }

        /// <summary>Ngày giờ khớp lệnh</summary>
        public DateTime TransactionDate { get; set; }

        /// <summary>Phí giao dịch (nếu có)</summary>
        public decimal? Fee { get; set; }

        /// <summary>Tổng giá trị = Quantity × Price</summary>
        public decimal TotalAmount { get; set; }

        /// <summary>Lãi/lỗ thực tế (chỉ cho lệnh SELL)</summary>
        public decimal? PnlAmount { get; set; }

        /// <summary>Nguồn giao dịch: DNSE, AI_SIMULATION</summary>
        public string Source { get; set; } = "DNSE";

        /// <summary>Liên kết vị thế tổng (TradePositions.Id)</summary>
        public int? PositionId { get; set; }

        /// <summary>Ghi chú tự do</summary>
        public string? Notes { get; set; }

        /// <summary>Giá cao nhất từ lúc mua đến lúc bán (dùng tính what-if)</summary>
        public decimal? PriceHighSinceBuy { get; set; }

        /// <summary>Giá thấp nhất từ lúc mua đến lúc bán (dùng tính what-if)</summary>
        public decimal? PriceLowSinceBuy { get; set; }

        /// <summary>Timing Score: tỷ lệ giá bán so với đỉnh/đáy (0-100%, chỉ cho SELL)</summary>
        public decimal? TimingScore { get; set; }
    }
}
