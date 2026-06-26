using AITradingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace AITradingSystem.Services
{
    /// <summary>
    /// Service để xóa và reset dữ liệu người dùng từ các bảng
    /// </summary>
    public class DataCleanupService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DataCleanupService> _logger;

        public DataCleanupService(AppDbContext context, ILogger<DataCleanupService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Xóa tất cả dữ liệu người dùng (TradePositions, Orders, StockTransactions, TradeEpisodes)
        /// </summary>
        public async Task<bool> DeleteAllUserDataAsync()
        {
            try
            {
                _logger.LogWarning("🗑️ BẮT ĐẦU XÓA TẤT CẢ DỮ LIỆU NGƯỜI DÙNG...");

                // Xóa theo thứ tự dependency
                // 1. Xóa TradeEpisodes (không phụ thuộc gì)
                var episodeCount = _context.TradeEpisodes!.Count();
                _context.TradeEpisodes.RemoveRange(_context.TradeEpisodes);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ Xóa {episodeCount} bản ghi TradeEpisodes");

                // 2. Xóa StockTransactions
                var txCount = _context.StockTransactions!.Count();
                _context.StockTransactions.RemoveRange(_context.StockTransactions);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ Xóa {txCount} bản ghi StockTransactions");

                // 3. Xóa Orders
                var orderCount = _context.Orders!.Count();
                _context.Orders.RemoveRange(_context.Orders);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ Xóa {orderCount} bản ghi Orders");

                // 4. Xóa TradePositions (cuối cùng vì có thể được reference)
                var posCount = _context.TradePositions!.Count();
                _context.TradePositions.RemoveRange(_context.TradePositions);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ Xóa {posCount} bản ghi TradePositions");

                _logger.LogWarning($"🗑️ HOÀN THÀNH: Đã xóa {episodeCount + txCount + orderCount + posCount} bản ghi!");
                _logger.LogWarning("📌 Dữ liệu đã được xóa sạch. Sẵn sàng để import dữ liệu mới từ DNSE.");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LỖI KHI XÓA DỮ LIỆU: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Xóa chỉ TradePositions + Orders (giữ lại StockTransactions để phân tích)
        /// </summary>
        public async Task<bool> DeleteTradePositionsAndOrdersAsync()
        {
            try
            {
                _logger.LogWarning("🗑️ BẮT ĐẦU XÓA TradePositions + Orders...");

                var orderCount = _context.Orders!.Count();
                _context.Orders.RemoveRange(_context.Orders);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ Xóa {orderCount} bản ghi Orders");

                var posCount = _context.TradePositions!.Count();
                _context.TradePositions.RemoveRange(_context.TradePositions);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"✅ Xóa {posCount} bản ghi TradePositions");

                _logger.LogWarning($"✅ HOÀN THÀNH: Đã xóa {orderCount + posCount} bản ghi!");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ LỖI KHI XÓA DỮ LIỆU: {Message}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Lấy số lượng bản ghi hiện tại ở mỗi bảng
        /// </summary>
        public async Task<Dictionary<string, int>> GetDataCountAsync()
        {
            return new Dictionary<string, int>
            {
                { "TradePositions", _context.TradePositions!.Count() },
                { "Orders", _context.Orders!.Count() },
                { "StockTransactions", _context.StockTransactions!.Count() },
                { "TradeEpisodes", _context.TradeEpisodes!.Count() },
                { "AiTradePositions", _context.AiTradePositions!.Count() },
                { "AiOrders", _context.AiOrders!.Count() }
            };
        }
    }
}
