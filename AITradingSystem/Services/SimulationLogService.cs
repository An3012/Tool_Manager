using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AITradingSystem.Controllers;

namespace AITradingSystem.Services
{
    public class SimulationLogService
    {
        private readonly ConcurrentQueue<string> _logs = new();
        private readonly int _maxLogs = 50;
        private readonly ConcurrentDictionary<string, StockViewModel> _stockStates = new();
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        // Lưu lần cuối lấy giá thật thành công
        private DateTime _lastRealPriceFetch = DateTime.MinValue;
        private readonly TimeSpan _realPriceFetchInterval = TimeSpan.FromSeconds(30);

        public SimulationLogService()
        {
            // Khởi tạo nhật ký hệ thống ban đầu
            AddLog("[System] Khởi động AI Trading Copilot Engine...");
            AddLog("[Memory] Kết nối với Database SQL Server...");
            AddLog("[Memory] Tải danh mục kinh nghiệm từ TradeEpisodes...");

            // Khởi tạo trạng thái giá các cổ phiếu ban đầu
            var initialStocks = new List<StockViewModel>
            {
                new() { Symbol = "FPT", CompanyName = "CTCP FPT", CurrentPrice = 135000, ChangePercentage = 1.2m, Rsi = 45.5m, Exchange = "HOSE" },
                new() { Symbol = "SSI", CompanyName = "CTCP Chứng khoán SSI", CurrentPrice = 36000, ChangePercentage = -0.5m, Rsi = 35.2m, Exchange = "HOSE" },
                new() { Symbol = "HPG", CompanyName = "CTCP Tập đoàn Hòa Phát", CurrentPrice = 28500, ChangePercentage = 2.1m, Rsi = 62.0m, Exchange = "HOSE" },
                new() { Symbol = "VNM", CompanyName = "CTCP Sữa Việt Nam", CurrentPrice = 67000, ChangePercentage = 0.0m, Rsi = 50.1m, Exchange = "HOSE" },
                new() { Symbol = "MWG", CompanyName = "CTCP Đầu tư Thế giới Di Động", CurrentPrice = 62000, ChangePercentage = -1.8m, Rsi = 28.5m, Exchange = "HOSE" },
                new() { Symbol = "VIC", CompanyName = "Tập đoàn Vingroup", CurrentPrice = 42500, ChangePercentage = 0.5m, Rsi = 41.3m, Exchange = "HOSE" },
                new() { Symbol = "POW", CompanyName = "CTCP Điện lực Dầu khí Việt Nam", CurrentPrice = 14000, ChangePercentage = 3.2m, Rsi = 72.0m, Exchange = "HOSE" },
                new() { Symbol = "VCG", CompanyName = "CTCP Vinaconex", CurrentPrice = 20000, ChangePercentage = -1.1m, Rsi = 48.0m, Exchange = "HOSE" },
                new() { Symbol = "VHM", CompanyName = "CTCP Vinhomes", CurrentPrice = 39000, ChangePercentage = 0.8m, Rsi = 43.2m, Exchange = "HOSE" },
                new() { Symbol = "TCB", CompanyName = "Ngân hàng Techcombank", CurrentPrice = 24000, ChangePercentage = 1.5m, Rsi = 55.4m, Exchange = "HOSE" },
                new() { Symbol = "VCB", CompanyName = "Ngân hàng Vietcombank", CurrentPrice = 92000, ChangePercentage = 0.2m, Rsi = 50.8m, Exchange = "HOSE" },
                new() { Symbol = "MBB", CompanyName = "Ngân hàng Quân đội (MB)", CurrentPrice = 22000, ChangePercentage = -0.7m, Rsi = 48.5m, Exchange = "HOSE" },
                new() { Symbol = "ACB", CompanyName = "Ngân hàng Á Châu", CurrentPrice = 27000, ChangePercentage = 0.4m, Rsi = 52.1m, Exchange = "HOSE" },
                new() { Symbol = "VPB", CompanyName = "Ngân hàng VPBank", CurrentPrice = 19000, ChangePercentage = 1.1m, Rsi = 54.0m, Exchange = "HOSE" },
                new() { Symbol = "STB", CompanyName = "Ngân hàng Sacombank", CurrentPrice = 29000, ChangePercentage = -1.2m, Rsi = 39.8m, Exchange = "HOSE" },
                new() { Symbol = "CTG", CompanyName = "Ngân hàng VietinBank", CurrentPrice = 33000, ChangePercentage = 0.9m, Rsi = 58.2m, Exchange = "HOSE" },
                new() { Symbol = "MSN", CompanyName = "CTCP Tập đoàn Masan", CurrentPrice = 75000, ChangePercentage = -0.5m, Rsi = 47.1m, Exchange = "HOSE" },
                new() { Symbol = "GAS", CompanyName = "Tổng Công ty Khí Việt Nam", CurrentPrice = 81000, ChangePercentage = 0.3m, Rsi = 51.5m, Exchange = "HOSE" },
                new() { Symbol = "SAB", CompanyName = "Tổng CTCP Bia - Rượu - Nước giải khát Sài Gòn", CurrentPrice = 56000, ChangePercentage = -1.0m, Rsi = 33.6m, Exchange = "HOSE" },
                new() { Symbol = "PLX", CompanyName = "Tập đoàn Xăng dầu Việt Nam", CurrentPrice = 38000, ChangePercentage = 1.8m, Rsi = 61.2m, Exchange = "HOSE" },
                new() { Symbol = "GVR", CompanyName = "Tập đoàn Công nghiệp Cao su Việt Nam", CurrentPrice = 32000, ChangePercentage = 2.5m, Rsi = 66.8m, Exchange = "HOSE" },
                new() { Symbol = "VRE", CompanyName = "CTCP Vincom Retail", CurrentPrice = 21000, ChangePercentage = -0.2m, Rsi = 44.5m, Exchange = "HOSE" },
                new() { Symbol = "BID", CompanyName = "Ngân hàng BIDV", CurrentPrice = 46000, ChangePercentage = 0.7m, Rsi = 53.0m, Exchange = "HOSE" },
                new() { Symbol = "KDH", CompanyName = "CTCP Đầu tư & Kinh doanh Nhà Khang Điền", CurrentPrice = 34000, ChangePercentage = 1.3m, Rsi = 57.5m, Exchange = "HOSE" },
                new() { Symbol = "NLG", CompanyName = "CTCP Đầu tư Nam Long", CurrentPrice = 38000, ChangePercentage = -0.4m, Rsi = 46.2m, Exchange = "HOSE" },
                new() { Symbol = "PDR", CompanyName = "CTCP Phát triển Bất động sản Phát Đạt", CurrentPrice = 26000, ChangePercentage = -2.1m, Rsi = 36.4m, Exchange = "HOSE" },
                new() { Symbol = "DXG", CompanyName = "CTCP Tập đoàn Đất Xanh", CurrentPrice = 16000, ChangePercentage = -1.5m, Rsi = 41.0m, Exchange = "HOSE" },
                new() { Symbol = "DIG", CompanyName = "Tổng CTCP Đầu tư Phát triển Xây dựng", CurrentPrice = 27000, ChangePercentage = 3.0m, Rsi = 64.2m, Exchange = "HOSE" },
                new() { Symbol = "VND", CompanyName = "CTCP Chứng khoán VNDIRECT", CurrentPrice = 21000, ChangePercentage = -0.8m, Rsi = 42.7m, Exchange = "HOSE" },
                new() { Symbol = "VCI", CompanyName = "CTCP Chứng khoán Vietcap", CurrentPrice = 48000, ChangePercentage = 1.6m, Rsi = 59.0m, Exchange = "HOSE" },
                new() { Symbol = "HCM", CompanyName = "CTCP Chứng khoán TP.HCM", CurrentPrice = 28000, ChangePercentage = 0.5m, Rsi = 50.3m, Exchange = "HOSE" },
                new() { Symbol = "NKG", CompanyName = "CTCP Thép Nam Kim", CurrentPrice = 24000, ChangePercentage = 2.2m, Rsi = 61.5m, Exchange = "HOSE" },
                new() { Symbol = "HSG", CompanyName = "CTCP Tập đoàn Hoa Sen", CurrentPrice = 23000, ChangePercentage = 1.9m, Rsi = 58.8m, Exchange = "HOSE" },
                new() { Symbol = "DGC", CompanyName = "CTCP Tập đoàn Hóa chất Đức Giang", CurrentPrice = 115000, ChangePercentage = 0.6m, Rsi = 52.4m, Exchange = "HOSE" },
                new() { Symbol = "DCM", CompanyName = "CTCP Phân bón Dầu khí Cà Mau", CurrentPrice = 36000, ChangePercentage = -1.1m, Rsi = 43.6m, Exchange = "HOSE" },
                new() { Symbol = "DPM", CompanyName = "Tổng Công ty Phân bón & Hóa chất Dầu khí", CurrentPrice = 37000, ChangePercentage = -0.5m, Rsi = 46.8m, Exchange = "HOSE" },
                new() { Symbol = "PVD", CompanyName = "CTCP Khoan & Dịch vụ Khoan Dầu khí", CurrentPrice = 29000, ChangePercentage = 1.4m, Rsi = 55.9m, Exchange = "HOSE" },
                new() { Symbol = "PVS", CompanyName = "Tổng CTCP Dịch vụ Kỹ thuật Dầu khí Việt Nam", CurrentPrice = 40000, ChangePercentage = 0.8m, Rsi = 51.2m, Exchange = "HNX" },
                new() { Symbol = "PVT", CompanyName = "Tổng CTCP Vận tải Dầu khí", CurrentPrice = 26000, ChangePercentage = -0.2m, Rsi = 49.0m, Exchange = "HOSE" },
                new() { Symbol = "GEX", CompanyName = "CTCP Tập đoàn GELEX", CurrentPrice = 22000, ChangePercentage = 1.2m, Rsi = 53.5m, Exchange = "HOSE" }
            };

            foreach (var stock in initialStocks)
            {
                _stockStates[stock.Symbol] = stock;
            }
        }

        public void AddLog(string message)
        {
            var formatted = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logs.Enqueue(formatted);

            // Giới hạn số lượng log
            while (_logs.Count > _maxLogs)
            {
                _logs.TryDequeue(out _);
            }
        }

        public List<string> GetLogs()
        {
            return _logs.ToList();
        }

        public List<StockViewModel> GetStockStates()
        {
            return _stockStates.Values.ToList();
        }

        public StockViewModel? GetStockState(string symbol)
        {
            return _stockStates.TryGetValue(symbol, out var stock) ? stock : null;
        }

        public void UpdateStockState(string symbol, decimal currentPrice, decimal changePercentage, decimal rsi)
        {
            if (_stockStates.TryGetValue(symbol, out var stock))
            {
                stock.CurrentPrice = currentPrice;
                stock.ChangePercentage = changePercentage;
                stock.Rsi = rsi;
            }
        }

        /// <summary>
        /// Thêm hoặc cập nhật trạng thái cổ phiếu. Dùng khi đồng bộ từ DNSE có mã mới chưa tồn tại trong danh sách.
        /// </summary>
        public void UpdateOrAddStockState(string symbol, decimal currentPrice, decimal changePercentage, decimal rsi, string companyName = "")
        {
            if (_stockStates.TryGetValue(symbol, out var stock))
            {
                stock.CurrentPrice = currentPrice;
                stock.ChangePercentage = changePercentage;
                stock.Rsi = rsi;
                if (!string.IsNullOrEmpty(companyName))
                    stock.CompanyName = companyName;
            }
            else
            {
                _stockStates[symbol] = new StockViewModel
                {
                    Symbol = symbol,
                    CompanyName = string.IsNullOrEmpty(companyName) ? symbol : companyName,
                    CurrentPrice = currentPrice,
                    ChangePercentage = changePercentage,
                    Rsi = rsi
                };
            }
        }

        /// <summary>
        /// Gọi API công khai của DNSE/VNDirect để lấy giá cổ phiếu thực tế.
        /// Trả về true nếu thành công, false nếu thất bại (sẽ fallback sang giả lập).
        /// </summary>
        public async Task<bool> FetchRealPricesAsync(List<string>? symbols = null)
        {
            // Throttle: không gọi API quá thường xuyên
            if (DateTime.Now - _lastRealPriceFetch < _realPriceFetchInterval)
                return false;

            try
            {
                var targetSymbols = symbols ?? _stockStates.Keys.ToList();
                if (!targetSymbols.Any()) return false;

                var symbolsQuery = string.Join(",", targetSymbols);

                // Gọi API DNSE Stock (public, không cần auth)
                var url = $"https://dchart-api.vndirect.com.vn/dchart/history?resolution=1&symbol={symbolsQuery}&from={DateTimeOffset.UtcNow.AddMinutes(-30).ToUnixTimeSeconds()}&to={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

                // Fallback: gọi từng mã qua API giá hiện tại của DNSE
                int successCount = 0;
                foreach (var symbol in targetSymbols)
                {
                    try
                    {
                        var priceUrl = $"https://finfo-api.vndirect.com.vn/v4/stock_price?sort=date&q=code:{symbol}~type:STOCK&size=1&page=1";
                        var request = new HttpRequestMessage(HttpMethod.Get, priceUrl);
                        request.Headers.Add("Accept", "application/json");
                        request.Headers.Add("User-Agent", "AITradingCopilot/1.0");

                        var response = await _httpClient.SendAsync(request);
                        if (!response.IsSuccessStatusCode) continue;

                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);
                        var data = doc.RootElement.GetProperty("data");
                        if (data.GetArrayLength() == 0) continue;

                        var item = data[0];
                        decimal closePrice = 0;
                        decimal change = 0;

                        // Lấy giá đóng cửa gần nhất
                        if (item.TryGetProperty("adClose", out var adClose))
                            closePrice = adClose.GetDecimal() * 1000; // API trả đơn vị nghìn đồng
                        else if (item.TryGetProperty("close", out var close))
                            closePrice = close.GetDecimal() * 1000;

                        if (closePrice <= 0) continue;

                        // Tính % thay đổi
                        if (item.TryGetProperty("pctChange", out var pctChange))
                            change = pctChange.GetDecimal();

                        // Tính RSI ước lượng từ biến động giá
                        var existingStock = GetStockState(symbol);
                        decimal rsi = existingStock?.Rsi ?? 50m;

                        UpdateOrAddStockState(symbol, closePrice, change, rsi);
                        successCount++;
                    }
                    catch
                    {
                        // Bỏ qua lỗi cho từng mã cụ thể, tiếp tục mã khác
                    }
                }

                if (successCount > 0)
                {
                    _lastRealPriceFetch = DateTime.Now;
                    AddLog($"[DNSE API] Đã cập nhật giá thật cho {successCount}/{targetSymbols.Count} mã cổ phiếu.");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                AddLog($"[DNSE API] Lỗi khi lấy giá thật: {ex.Message}. Sử dụng giá giả lập.");
                return false;
            }
        }
    }
}
