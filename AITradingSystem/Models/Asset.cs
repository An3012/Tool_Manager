using System.ComponentModel.DataAnnotations;

namespace AITradingSystem.Models
{
    public class Asset
    {
        [Key]
        public string Symbol { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
    }
}
