using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using AITradingSystem.Models;
using AITradingSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using AITradingSystem.Controllers;
using System.Collections.Generic;

namespace AITradingSystem.Services
{
    public class DnseService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly SimulationLogService _simulationLogService;

        public DnseService(HttpClient httpClient, IConfiguration configuration, AppDbContext context, SimulationLogService simulationLogService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _context = context;
            _simulationLogService = simulationLogService;
        }

        private static string GetAppBaseDirectory()
        {
            var baseDir = AppContext.BaseDirectory;
            return string.IsNullOrWhiteSpace(baseDir) ? Directory.GetCurrentDirectory() : baseDir;
        }

        /// <summary>
        /// Tìm thư mục chứa file dnse_scraper.py, thử nhiều đường dẫn.
        /// </summary>
        private static string FindScriptDirectory()
        {
            // Ưu tiên 1: AppContext.BaseDirectory (bin/Debug/net9.0/ khi build)
            var baseDir = GetAppBaseDirectory();
            if (File.Exists(Path.Combine(baseDir, "dnse_scraper.py")))
                return baseDir;

            // Ưu tiên 2: Thư mục hiện tại (Directory.GetCurrentDirectory())
            var cwd = Directory.GetCurrentDirectory();
            if (File.Exists(Path.Combine(cwd, "dnse_scraper.py")))
                return cwd;

            // Ưu tiên 3: Đi lên 3 cấp từ bin/Debug/net9.0/ về project root
            var projectRoot = baseDir;
            for (int i = 0; i < 4; i++)
            {
                var parent = Directory.GetParent(projectRoot);
                if (parent == null) break;
                projectRoot = parent.FullName;
                if (File.Exists(Path.Combine(projectRoot, "dnse_scraper.py")))
                    return projectRoot;
            }

            // Fallback: trả về baseDir (sẽ báo lỗi không tìm thấy file ở sau)
            return baseDir;
        }

        private static string GetPythonExecutable()
        {
            var pythonExe = Environment.GetEnvironmentVariable("PYTHON_EXECUTABLE");
            if (!string.IsNullOrWhiteSpace(pythonExe))
            {
                return pythonExe;
            }

            return "python";
        }

        private static decimal? CalculateTargetPrice(decimal entryPrice, decimal targetProfitPercentage)
        {
            if (entryPrice <= 0 || targetProfitPercentage <= 0)
            {
                return null;
            }

            return Math.Round(entryPrice * (1 + (targetProfitPercentage / 100m)), 4);
        }

        private static decimal? CalculateStopLossPrice(decimal entryPrice, decimal maxLossPercentage)
        {
            if (entryPrice <= 0 || maxLossPercentage <= 0)
            {
                return null;
            }

            var stopLoss = entryPrice * (1 - (maxLossPercentage / 100m));
            return Math.Round(Math.Max(stopLoss, 0), 4);
        }

        // Đồng bộ danh mục đầu tư (Vị thế thực tế) từ DNSE về Database SQL Server thông qua Python Web Scraping
        public async Task<bool> SyncPortfolioAsync()
        {
            try
            {
                var pref = await _context.UserPreferences.FirstOrDefaultAsync();
                if (pref == null || string.IsNullOrEmpty(pref.DnseUsername) || string.IsNullOrEmpty(pref.DnsePassword))
                {
                    return await SimulateSyncAsync("MOCK_USER");
                }

                var appBaseDir = FindScriptDirectory();
                string pythonScriptPath = Path.Combine(appBaseDir, "dnse_scraper.py");
                string jsonPath = Path.Combine(appBaseDir, "dnse_deals.json");
                Console.WriteLine($"[DNSE] Tìm script tại: {pythonScriptPath} (tồn tại: {File.Exists(pythonScriptPath)})");

                if (!File.Exists(pythonScriptPath))
                {
                    Console.WriteLine($"Không tìm thấy tệp Python scraper tại {pythonScriptPath}. Chuyển sang chế độ giả lập dữ liệu Deal.");
                    return await SimulateSyncAsync(pref.DnseUsername);
                }

                if (File.Exists(jsonPath))
                {
                    try { File.Delete(jsonPath); } catch (Exception ex) { Console.WriteLine($"Không thể xóa JSON cũ: {ex.Message}"); }
                }

                Console.WriteLine($"Bắt đầu kích hoạt tiến trình Python: {GetPythonExecutable()}");
                Console.WriteLine($"Script path: {pythonScriptPath}");
                Console.WriteLine($"Working directory: {appBaseDir}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = GetPythonExecutable(),
                    Arguments = $"\"{pythonScriptPath}\" \"{pref.DnseUsername}\" \"{pref.DnsePassword}\"",
                    WorkingDirectory = appBaseDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = new Process { StartInfo = startInfo })
                {
                    if (!process.Start())
                    {
                        Console.WriteLine("Không thể khởi chạy tiến trình Python. Vui lòng kiểm tra Python/PATH.");
                        return await SimulateSyncAsync(pref.DnseUsername);
                    }

                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    Console.WriteLine($"Python Output: {output}");
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Console.WriteLine($"Python Error: {error}");
                    }

                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"Tiến trình Python thoát với mã lỗi: {process.ExitCode}. Chuyển sang chế độ giả lập dữ liệu Deal.");
                        return await SimulateSyncAsync(pref.DnseUsername);
                    }
                }

                if (!File.Exists(jsonPath))
                {
                    Console.WriteLine($"Không tìm thấy file kết quả dnse_deals.json tại {jsonPath}. Chuyển sang chế độ giả lập dữ liệu Deal.");
                    return await SimulateSyncAsync(pref.DnseUsername);
                }

                string jsonContent = await File.ReadAllTextAsync(jsonPath);
                try { File.Delete(jsonPath); } catch (Exception ex) { Console.WriteLine($"Không thể xóa JSON sau khi đọc: {ex.Message}"); }

                using var doc = JsonDocument.Parse(jsonContent);
                JsonElement root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var userPref = await _context.UserPreferences.FirstOrDefaultAsync();
                    var targetProfitPercentage = userPref?.TargetProfitPercentage ?? 15m;
                    var maxLossPercentage = userPref?.MaxLossPercentage ?? 7m;

                    // Lấy tất cả vị thế OPEN không phải AI để so sánh (UPSERT)
                    var existingOpenPositions = await _context.TradePositions
                        .Where(p => p.Status == "OPEN" && !p.IsAiTrade)
                        .ToListAsync();

                    // Tập hợp các mã đã xử lý từ DNSE (để biết mã nào cần đánh dấu đóng)
                    var processedSymbols = new HashSet<string>();

                    foreach (var d in root.EnumerateArray())
                    {
                        string dealText = d.TryGetProperty("DealText", out var dt) ? dt.GetString() ?? "" : "";
                        string qtyText = d.TryGetProperty("QtyText", out var qt) ? qt.GetString() ?? "" : "";
                        string openTimeText = d.TryGetProperty("OpenTimeText", out var ot) ? ot.GetString() ?? "" : "";
                        string statusText = d.TryGetProperty("StatusText", out var st) ? st.GetString() ?? "" : "";
                        string pnlText = d.TryGetProperty("PnlText", out var pt) ? pt.GetString() ?? "" : "";
                        // Enriched fields from new scraper
                        string avgPriceText = d.TryGetProperty("AvgPrice", out var ap) ? ap.GetString() ?? "" : "";
                        string marketPriceText = d.TryGetProperty("MarketPrice", out var mp) ? mp.GetString() ?? "" : "";
                        string investedValueText = d.TryGetProperty("InvestedValue", out var iv) ? iv.GetString() ?? "" : "";

                        string symbol = dealText.Split(' ')[0].Trim().ToUpper();
                        if (string.IsNullOrEmpty(symbol)) continue;

                        int.TryParse(qtyText.Replace(",", "").Replace(".", "").Trim(), out int quantity);
                        if (quantity <= 0) continue;

                        decimal.TryParse(pnlText.Replace(",", "").Replace(".", "").Replace("+", "").Trim(), out decimal pnl);
                        if (pnlText.Contains("-"))
                        {
                            pnl = -Math.Abs(pnl);
                        }

                        string status = "OPEN";
                        if (statusText.Contains("Đóng") || statusText.Contains("Dong") || statusText.Equals("CLOSED", StringComparison.OrdinalIgnoreCase))
                        {
                            status = "CLOSED";
                        }

                        DateTime openDate = DateTime.Now.AddDays(-2);
                        if (DateTime.TryParseExact(openTimeText.Trim(), new[] { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var tempDate))
                        {
                            openDate = tempDate;
                        }

                        // Parse AvgPrice (giá mua trung bình từ DNSE — chính xác hơn tính ngược từ PnL)
                        decimal.TryParse(avgPriceText.Replace(",", "").Trim(), out decimal avgPrice);
                        // Parse MarketPrice (giá thị trường hiện tại từ DNSE)
                        decimal.TryParse(marketPriceText.Replace(",", "").Trim(), out decimal marketPrice);
                        // Parse InvestedValue (giá trị vốn đầu tư)
                        decimal.TryParse(investedValueText.Replace(",", "").Trim(), out decimal investedValue);

                        // Xác định currentPrice: ưu tiên MarketPrice từ DNSE, sau đó từ hệ thống local
                        decimal currentPrice = 0;
                        if (marketPrice > 0)
                        {
                            currentPrice = marketPrice;
                        }
                        else
                        {
                            var stockState = _simulationLogService.GetStockState(symbol);
                            if (stockState != null)
                            {
                                currentPrice = stockState.CurrentPrice;
                            }
                            else
                            {
                                currentPrice = 15000;
                                _simulationLogService.UpdateOrAddStockState(symbol, currentPrice, 0, 50, symbol);
                                Console.WriteLine($"[DNSE] Tự động đăng ký mã mới: {symbol}");
                            }
                        }

                        // Xác định entryPrice: ưu tiên AvgPrice trực tiếp từ DNSE, fallback tính từ PnL
                        decimal entryPrice;
                        if (avgPrice > 0)
                        {
                            entryPrice = avgPrice;
                            Console.WriteLine($"[DNSE] {symbol}: Dùng giá TB trực tiếp từ DNSE: {entryPrice:N0}đ");
                        }
                        else if (quantity > 0 && currentPrice > 0)
                        {
                            entryPrice = currentPrice - (pnl / quantity);
                            Console.WriteLine($"[DNSE] {symbol}: Tính giá TB từ PnL: {entryPrice:N0}đ (fallback)");
                        }
                        else
                        {
                            entryPrice = currentPrice;
                        }

                        // Xác định investedAmount
                        decimal investedAmount = investedValue > 0 ? investedValue : entryPrice * quantity;

                        processedSymbols.Add(symbol);

                        // UPSERT: Tìm vị thế OPEN hiện có của mã này
                        var existingPos = existingOpenPositions.FirstOrDefault(p => p.Symbol == symbol);

                        if (existingPos != null)
                        {
                            // CẬP NHẬT vị thế đã có - CHỈ cập nhật dữ liệu từ DNSE
                            // GIỮ NGUYÊN các field người dùng đã thao tác
                            existingPos.Quantity = quantity;
                            existingPos.PnL = pnl;
                            existingPos.Status = status;

                            // Cập nhật EntryPrice: ưu tiên giá TB trực tiếp từ DNSE
                            if (avgPrice > 0)
                            {
                                existingPos.EntryPrice = entryPrice; // avgPrice đã được gán vào entryPrice ở trên
                            }
                            else if (existingPos.EntryPrice == 0)
                            {
                                existingPos.EntryPrice = entryPrice;
                            }

                            // Chỉ cập nhật ngày mở nếu chưa có
                            if (existingPos.EntryDate == DateTime.MinValue)
                            {
                                existingPos.EntryDate = openDate;
                            }

                            // KHÔNG ghi đè các field sau (người dùng đã cài đặt):
                            // - TakeProfitPrice
                            // - StopLossPrice
                            // - TargetProfitAmount

                            // Cập nhật InvestedAmount từ DNSE nếu có dữ liệu mới
                            if (investedAmount > 0)
                            {
                                existingPos.InvestedAmount = investedAmount;
                            }

                            // Nếu vị thế được đóng từ DNSE
                            if (status == "CLOSED")
                            {
                                existingPos.ExitPrice = currentPrice;
                                existingPos.ExitDate = DateTime.Now;
                            }

                            Console.WriteLine($"[DNSE Sync] Cập nhật vị thế: {symbol} (SL: {quantity}, Giá vào: {entryPrice:N0}, PnL: {pnl:N0})");
                        }
                        else
                        {
                            // THÊM MỚI vị thế chưa tồn tại
                            var takeProfitPrice = CalculateTargetPrice(entryPrice, targetProfitPercentage);
                            var stopLossPrice = CalculateStopLossPrice(entryPrice, maxLossPercentage);

                            _context.TradePositions.Add(new TradePosition
                            {
                                Symbol = symbol,
                                Quantity = quantity,
                                EntryPrice = entryPrice,
                                EntryDate = openDate,
                                Status = status,
                                PnL = pnl,
                                TakeProfitPrice = takeProfitPrice,
                                StopLossPrice = stopLossPrice,
                                IsAiTrade = false,
                                InvestedAmount = investedAmount
                            });
                            Console.WriteLine($"[DNSE Sync] Thêm mới vị thế: {symbol} (SL: {quantity}, Giá vào: {entryPrice:N0}, Vốn: {investedAmount:N0})");
                        }
                    }

                    await _context.SaveChangesAsync();
                    return true;
                }

                Console.WriteLine("JSON không đúng định dạng mảng.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi đồng bộ DNSE qua Python Scraper: {ex.Message}");
                return await SimulateSyncAsync("MOCK_USER");
            }
        }

        private async Task<bool> SimulateSyncAsync(string accountId)
        {
            var pref = await _context.UserPreferences.FirstOrDefaultAsync();
            var targetProfitPercentage = pref?.TargetProfitPercentage ?? 15m;
            var maxLossPercentage = pref?.MaxLossPercentage ?? 7m;

            var existingOpenPositions = await _context.TradePositions.Where(p => p.Status == "OPEN" && !p.IsAiTrade).ToListAsync();
            _context.TradePositions.RemoveRange(existingOpenPositions);

            _context.TradePositions.AddRange(
                new TradePosition
                {
                    Symbol = "POW",
                    Quantity = 50,
                    EntryPrice = 13554.7m,
                    EntryDate = DateTime.Parse("2026-05-27 09:15:00"),
                    Status = "OPEN",
                    PnL = 22265,
                    TakeProfitPrice = CalculateTargetPrice(13554.7m, targetProfitPercentage),
                    StopLossPrice = CalculateStopLossPrice(13554.7m, maxLossPercentage),
                    IsAiTrade = false,
                    InvestedAmount = 13554.7m * 50,
                    BudgetAmount = pref?.AmountPerTrade
                },
                new TradePosition
                {
                    Symbol = "VCG",
                    Quantity = 50,
                    EntryPrice = 20228.88m,
                    EntryDate = DateTime.Parse("2026-06-01 10:28:48"),
                    Status = "OPEN",
                    PnL = -11444,
                    TakeProfitPrice = CalculateTargetPrice(20228.88m, targetProfitPercentage),
                    StopLossPrice = CalculateStopLossPrice(20228.88m, maxLossPercentage),
                    IsAiTrade = false,
                    InvestedAmount = 20228.88m * 50,
                    BudgetAmount = pref?.AmountPerTrade
                }
            );
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
