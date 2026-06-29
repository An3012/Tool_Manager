using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AITradingSystem.Data;
using AITradingSystem.Models;
using AITradingSystem.Services;

namespace AITradingSystem.Pages
{
    public class AiPlanGeneratorModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly AiPlanGenerationService _aiPlanService;

        [BindProperty]
        public decimal Capital { get; set; } = 5000000m;

        [BindProperty]
        public decimal TargetProfit { get; set; } = 500000m;

        [BindProperty]
        public DateTime Deadline { get; set; } = DateTime.Today.AddDays(30);

        [BindProperty]
        public string RiskTolerance { get; set; } = "Medium";

        [BindProperty]
        public string PreferredStrategy { get; set; } = "Balanced";

        public AiPlanPrediction GeneratedPlan { get; set; }
        public List<AiPlanPrediction> PreviousPredictions { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;

        public AiPlanGeneratorModel(AppDbContext context, AiPlanGenerationService aiPlanService)
        {
            _context = context;
            _aiPlanService = aiPlanService;
        }

        public async Task OnGetAsync()
        {
            // Tải các dự đoán trước đó
            PreviousPredictions = await _context.AiPlanPredictions?
                .OrderByDescending(p => p.PredictionDate)
                .Take(10)
                .ToListAsync() ?? new List<AiPlanPrediction>();
        }

        public async Task<IActionResult> OnPostGenerateAsync()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ErrorMessage = "Vui lòng kiểm tra các thông tin đã nhập";
                    return Page();
                }

                // Validate input
                if (Capital <= 0)
                {
                    ErrorMessage = "Vốn khởi động phải lớn hơn 0";
                    return Page();
                }

                if (TargetProfit <= 0)
                {
                    ErrorMessage = "Mục tiêu lợi nhuận phải lớn hơn 0";
                    return Page();
                }

                if (Deadline <= DateTime.Today)
                {
                    ErrorMessage = "Deadline phải là ngày trong tương lai";
                    return Page();
                }

                // Gọi dịch vụ AI
                GeneratedPlan = await _aiPlanService.GeneratePlanPredictionAsync(
                    Capital,
                    TargetProfit,
                    Deadline,
                    RiskTolerance,
                    PreferredStrategy);

                SuccessMessage = "✅ Kế hoạch đã được tạo thành công từ AI!";

                // Tải lại danh sách dự đoán
                PreviousPredictions = await _context.AiPlanPredictions?
                    .OrderByDescending(p => p.PredictionDate)
                    .Take(10)
                    .ToListAsync() ?? new List<AiPlanPrediction>();

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Lỗi khi tạo kế hoạch: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostImplementAsync(int planId)
        {
            try
            {
                var plan = await _context.AiPlanPredictions?
                    .FirstOrDefaultAsync(p => p.Id == planId);
                if (plan == null)
                {
                    return NotFound("Không tìm thấy kế hoạch");
                }

                // Tạo InvestmentPlan từ AiPlanPrediction
                var investmentPlan = new InvestmentPlan
                {
                    RunDate = DateTime.Today,
                    StartDate = DateTime.Today,
                    EndDate = plan.DeadlineDate,
                    Capital = plan.Capital,
                    TargetProfit = plan.TargetProfit,
                    ActualProfit = 0m,
                    RemainingProfitNeeded = plan.TargetProfit,
                    DaysRemainingAtRun = (plan.DeadlineDate.Date - DateTime.Today).Days,
                    SuccessProbability = plan.SuccessProbability,
                    Status = "Implemented",
                    DailyCalendarJson = plan.PredictedPlanJson
                };

                _context.InvestmentPlans?.Add(investmentPlan);
                plan.PredictionStatus = "Implemented";
                plan.InvestmentPlanId = investmentPlan.Id;
                _context.AiPlanPredictions?.Update(plan);
                await _context.SaveChangesAsync();

                SuccessMessage = $"✅ Kế hoạch đã được triển khai! ID: {investmentPlan.Id}";
                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Lỗi khi triển khai kế hoạch: {ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int planId)
        {
            try
            {
                var plan = await _context.AiPlanPredictions?
                    .FirstOrDefaultAsync(p => p.Id == planId);
                if (plan == null)
                {
                    return NotFound("Không tìm thấy kế hoạch");
                }

                _context.AiPlanPredictions?.Remove(plan);
                await _context.SaveChangesAsync();

                SuccessMessage = "✅ Kế hoạch đã được xóa";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ Lỗi khi xóa kế hoạch: {ex.Message}";
                return Page();
            }
        }
    }
}
