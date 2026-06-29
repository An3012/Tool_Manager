using Microsoft.AspNetCore.Mvc.RazorPages;
using AITradingSystem.Data;
using AITradingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AITradingSystem.Pages
{
    public class PlanAnalyticsModel : PageModel
    {
        private readonly AppDbContext _context;

        public PlanAnalyticsModel(AppDbContext context)
        {
            _context = context;
        }

        public AnalyticsData Analytics { get; set; } = new();
        public List<AiLearningKnowledge> KnowledgeRecords { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Fetch all predictions
            var predictions = await _context.AiPlanPredictions?.ToListAsync() ?? new List<AiPlanPrediction>();

            // Fetch all knowledge records
            var allKnowledge = await _context.AiLearningKnowledge?.ToListAsync() ?? new List<AiLearningKnowledge>();

            // Knowledge records with results
            var completedKnowledge = allKnowledge
                .Where(k => !string.IsNullOrEmpty(k.ActualOutcome) && k.AccuracyScore > 0)
                .OrderByDescending(k => k.RecordedDate)
                .ToList();

            KnowledgeRecords = allKnowledge
                .OrderByDescending(k => k.RecordedDate)
                .ToList();

            // Calculate analytics
            var successCount = completedKnowledge.Count(k => k.VerificationLevel == "Verified");
            var totalCompleted = completedKnowledge.Count;
            var failedCount = totalCompleted - successCount;

            Analytics = new AnalyticsData
            {
                TotalPlansGenerated = predictions.Count,
                SuccessfulPlans = successCount,
                FailedPlans = failedCount,
                SuccessRate = totalCompleted > 0 ? (decimal)successCount / totalCompleted * 100 : 0m,
                OverallAccuracy = completedKnowledge.Any() ? completedKnowledge.Average(k => k.AccuracyScore) : 0m,
                AveragePredictedProbability = predictions.Any() ? predictions.Average(p => p.SuccessProbability) : 0m,
                AverageActualAccuracy = completedKnowledge.Any() ? completedKnowledge.Average(k => k.AccuracyScore) : 0m
            };
        }

        public class AnalyticsData
        {
            public int TotalPlansGenerated { get; set; }
            public int SuccessfulPlans { get; set; }
            public int FailedPlans { get; set; }
            public decimal SuccessRate { get; set; }
            public decimal OverallAccuracy { get; set; }
            public decimal AveragePredictedProbability { get; set; }
            public decimal AverageActualAccuracy { get; set; }
        }
    }
}
