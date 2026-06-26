using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AITradingSystem.Services;

namespace AITradingSystem.Pages
{
    public class AdminDataCleanupModel : PageModel
    {
        private readonly DataCleanupService _cleanupService;
        private readonly ILogger<AdminDataCleanupModel> _logger;

        public Dictionary<string, int> DataCounts { get; set; } = new();
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public AdminDataCleanupModel(DataCleanupService cleanupService, ILogger<AdminDataCleanupModel> logger)
        {
            _cleanupService = cleanupService;
            _logger = logger;
        }

        public async Task OnGetAsync()
        {
            DataCounts = await _cleanupService.GetDataCountAsync();
        }

        public async Task<IActionResult> OnPostDeleteAllAsync()
        {
            var result = await _cleanupService.DeleteAllUserDataAsync();
            IsSuccess = result;
            Message = result 
                ? "✅ Đã xóa tất cả dữ liệu thành công!" 
                : "❌ Lỗi khi xóa dữ liệu!";

            DataCounts = await _cleanupService.GetDataCountAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeletePositionsOrdersAsync()
        {
            var result = await _cleanupService.DeleteTradePositionsAndOrdersAsync();
            IsSuccess = result;
            Message = result 
                ? "✅ Đã xóa TradePositions + Orders thành công!" 
                : "❌ Lỗi khi xóa dữ liệu!";

            DataCounts = await _cleanupService.GetDataCountAsync();
            return Page();
        }
    }
}
