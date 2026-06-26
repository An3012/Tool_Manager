filepath = r"d:\NDA\NEW\Tool_Manager\AITradingSystem\Views\Copilot\Portfolio.cshtml"

with open(filepath, "r", encoding="utf-8") as f:
    code = f.read()

# 1. Update the setup block at the top to define openPositions
target_setup = """    var positions = ViewBag.Positions as List<TradePosition> ?? new List<TradePosition>();
    var stocks = ViewBag.Stocks as List<StockViewModel> ?? new List<StockViewModel>();"""

replacement_setup = """    var positions = ViewBag.Positions as List<TradePosition> ?? new List<TradePosition>();
    var openPositions = positions.Where(p => p.Status == "OPEN").ToList();
    var stocks = ViewBag.Stocks as List<StockViewModel> ?? new List<StockViewModel>();"""

if target_setup not in code:
    target_setup = target_setup.replace('\n', '\r\n')
    replacement_setup = replacement_setup.replace('\n', '\r\n')

if target_setup in code:
    code = code.replace(target_setup, replacement_setup)
    print("Fixed setup block")
else:
    print("Could not find setup block")

# 2. Update the main loop to only iterate over openPositions
# We find:
#     @if (!positions.Any())
# and
#         foreach (var pos in positions)
# wait, if positions is empty, it shows 'Bạn chưa có cổ phiếu nào'. Since positions can contain closed positions, it is better if we check openPositions.Any() to show the active holdings message!
# So:
#     @if (!openPositions.Any())
#     ...
#     foreach (var pos in openPositions)

target_loop_check = "@if (!positions.Any())"
replacement_loop_check = "@if (!openPositions.Any())"

if target_loop_check in code:
    code = code.replace(target_loop_check, replacement_loop_check)
    print("Fixed loop check")

target_loop = "        foreach (var pos in positions)"
replacement_loop = "        foreach (var pos in openPositions)"

if target_loop in code:
    code = code.replace(target_loop, replacement_loop)
    print("Fixed main loop")

# 3. Replace the closed positions block at the bottom
target_closed_block = """    <!-- Danh Mục Vị Thế Đã Đóng / Lịch Sử Đầu Tư -->
    <div class="glass-card mt-4 mb-4">
        <h4 class="fw-bold text-white mb-3" style="font-size: 1.1rem;"><span class="gradient-text">Danh mục vị thế đã đóng / Lịch sử đầu tư</span></h4>
        @{
            var closedPositions = ViewBag.ClosedPositions as List<TradePosition> ?? new List<TradePosition>();
        }
        @if (!closedPositions.Any())
        {
            <p class="text-secondary small mb-0">Chưa có vị thế nào được chốt (đã đóng).</p>
        }
        else
        {
            <div class="row g-3">
                @foreach (var cp in closedPositions)
                {
                    <div class="col-md-4">
                        <div class="p-3 rounded" style="background: rgba(255, 255, 255, 0.02); border: 1px solid rgba(255, 255, 255, 0.05);">
                            <div class="d-flex justify-content-between align-items-center mb-2">
                                <span class="fw-bold fs-5 text-white">@cp.Symbol</span>
                                <span class="badge bg-secondary">ĐÃ ĐÓNG (0 CP)</span>
                            </div>
                            <div class="d-flex justify-content-between mb-1">
                                <span class="text-secondary" style="font-size: 0.85rem;">Lãi/Lỗ thực tế:</span>
                                <span class="fw-bold @(cp.PnL >= 0 ? "pnl-positive" : "pnl-negative")">@(cp.PnL >= 0 ? "+" : "")@cp.PnL.ToString("N0") đ</span>
                            </div>
                            <div class="d-flex justify-content-between mb-1">
                                <span class="text-secondary" style="font-size: 0.85rem;">Giá mua TB:</span>
                                <span class="text-white">@cp.EntryPrice.ToString("N0") đ</span>
                            </div>
                            <div class="d-flex justify-content-between">
                                <span class="text-secondary" style="font-size: 0.85rem;">Đã giữ:</span>
                                <span class="text-white">@((DateTime.Now - cp.EntryDate).Days) ngày</span>
                            </div>
                        </div>
                    </div>
                }
            </div>
        }
    </div>"""

replacement_closed_block = """    <!-- Danh Mục Vị Thế Đã Đóng / Lịch Sử Đầu Tư -->
    <div class="glass-card mt-4 mb-4">
        <h4 class="fw-bold text-white mb-3" style="font-size: 1.1rem;"><span class="gradient-text">Danh mục vị thế đã đóng / Lịch sử đầu tư (Chi tiết giao dịch)</span></h4>
        @{
            var closedPositions = ViewBag.ClosedPositions as List<TradePosition> ?? new List<TradePosition>();
        }
        @if (!closedPositions.Any())
        {
            <p class="text-secondary small mb-0">Chưa có vị thế nào được chốt (đã đóng).</p>
        }
        else
        {
            <div class="d-flex flex-column gap-4">
                @foreach (var cp in closedPositions)
                {
                    var symbolTransactions = (ViewBag.Transactions as List<StockTransaction> ?? new List<StockTransaction>())
                        .Where(t => t.Symbol == cp.Symbol)
                        .OrderBy(t => t.TransactionDate)
                        .ToList();

                    <div class="position-card">
                        <div class="position-header" style="background: rgba(255,255,255,0.01); padding: 16px 20px;">
                            <div class="d-flex align-items-center gap-3">
                                <div>
                                    <span class="fw-bold text-white" style="font-size: 1.15rem;">@cp.Symbol</span>
                                    <span class="source-badge bg-secondary text-white-50 ms-2" style="font-size: 0.73rem; border: 1px solid rgba(255,255,255,0.1); border-radius: 4px; padding: 4px 8px; display: inline-block;">⚪ Đã tất toán (0 CP)</span>
                                </div>
                            </div>
                            <div class="text-end">
                                <div class="@(cp.PnL >= 0 ? "pnl-positive" : "pnl-negative")" style="font-size: 1.25rem; font-weight: 700;">
                                    @(cp.PnL >= 0 ? "+" : "")@cp.PnL.ToString("N0") đ
                                </div>
                                <div class="small text-secondary" style="font-size: 0.82rem;">
                                    Lãi/Lỗ thực tế chốt
                                </div>
                            </div>
                        </div>
                        <div class="position-body" style="padding: 16px 20px;">
                            <div class="stat-grid mb-3">
                                <div class="stat-item">
                                    <div class="stat-label" style="font-size: 0.72rem; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.04em;">Khối lượng nắm giữ</div>
                                    <div class="stat-value text-white-50" style="font-size: 0.95rem; font-weight: 600; margin-top: 2px;">0 CP</div>
                                </div>
                                <div class="stat-item">
                                    <div class="stat-label" style="font-size: 0.72rem; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.04em;">Giá mua trung bình</div>
                                    <div class="stat-value text-white-50" style="font-size: 0.95rem; font-weight: 600; margin-top: 2px;">@cp.EntryPrice.ToString("N0") đ</div>
                                </div>
                                <div class="stat-item">
                                    <div class="stat-label" style="font-size: 0.72rem; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.04em;">Thời gian nắm giữ</div>
                                    <div class="stat-value text-white-50" style="font-size: 0.95rem; font-weight: 600; margin-top: 2px;">@((DateTime.Now.Date - cp.EntryDate.Date).Days) ngày</div>
                                </div>
                            </div>

                            <!-- Timeline Lịch sử giao dịch chi tiết cho vị thế đã chốt -->
                            @if (symbolTransactions.Any())
                            {
                                <div class="transaction-section mb-2 mt-2" style="background: rgba(255, 255, 255, 0.015); border: 1px solid var(--border-color); border-radius: 12px; padding: 16px;">
                                    <div class="d-flex align-items-center gap-2 mb-2">
                                        <span style="font-size: 1rem;">📜</span>
                                        <span style="font-size: 0.75rem; color: var(--accent-cyan); font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;">Lịch sử giao dịch chi tiết của mã</span>
                                    </div>
                                    <div class="timeline-container" style="border-left: 2px solid var(--border-color); padding-left: 16px; margin-left: 6px; display: flex; flex-direction: column; gap: 12px;">
                                        @foreach (var tx in symbolTransactions)
                                        {
                                            var isBuy = tx.TransactionType == "BUY";
                                            var dotColor = isBuy ? "var(--accent-green)" : "var(--accent-amber)";
                                            var actionText = isBuy ? "MUA" : "BÁN";
                                            var actionClass = isBuy ? "pnl-positive" : "text-warning";
                                            
                                            var timingClass = "";
                                            var timingLabel = "";
                                            if (tx.TimingScore.HasValue)
                                            {
                                                timingClass = tx.TimingScore.Value >= 80m ? "pnl-positive" : tx.TimingScore.Value >= 50m ? "text-warning" : "pnl-negative";
                                                timingLabel = tx.TimingScore.Value >= 80m ? "Tốt" : tx.TimingScore.Value >= 50m ? "Trung bình" : "Kém";
                                            }

                                            <div class="timeline-item" style="position: relative; padding-bottom: 4px;">
                                                <div class="timeline-dot" style="position: absolute; left: -21px; top: 4px; width: 10px; height: 10px; border-radius: 50%; background: @dotColor; border: 2px solid var(--bg-dark);"></div>
                                                <div class="d-flex justify-content-between align-items-center">
                                                    <div style="font-size: 0.85rem;">
                                                        <span class="fw-bold @actionClass">
                                                            @actionText
                                                        </span>
                                                        @tx.Quantity.ToString("N0") CP × @tx.Price.ToString("N0") đ
                                                    </div>
                                                    <div style="font-size: 0.72rem; color: var(--text-secondary);">
                                                        @tx.TransactionDate.ToString("dd/MM/yyyy HH:mm")
                                                    </div>
                                                </div>
                                                @if (tx.TransactionType == "SELL")
                                                {
                                                    <div class="d-flex justify-content-between align-items-center mt-1" style="font-size: 0.78rem;">
                                                        @if (tx.PnlAmount.HasValue)
                                                        {
                                                            <span class="@(tx.PnlAmount.Value >= 0 ? "pnl-positive" : "pnl-negative")">
                                                                Lãi/Lỗ: @(tx.PnlAmount.Value >= 0 ? "+" : "")@tx.PnlAmount.Value.ToString("N0") đ
                                                            </span>
                                                        }
                                                        @if (tx.TimingScore.HasValue)
                                                        {
                                                            <span style="font-size: 0.72rem; color: var(--text-secondary);">
                                                                Timing Score: <span class="fw-bold @timingClass">@tx.TimingScore.Value% (@timingLabel)</span>
                                                            </span>
                                                        }
                                                    </div>
                                                    @if (tx.PriceHighSinceBuy.HasValue && tx.PriceLowSinceBuy.HasValue && tx.PriceHighSinceBuy.Value > tx.PriceLowSinceBuy.Value)
                                                    {
                                                        <div class="price-range-bar mt-2" style="font-size: 0.72rem; color: var(--text-secondary);">
                                                            <div class="d-flex justify-content-between mb-1" style="font-size: 0.68rem;">
                                                                <span>Thấp nhất: @tx.PriceLowSinceBuy.Value.ToString("N0") đ</span>
                                                                <span>Cao nhất: @tx.PriceHighSinceBuy.Value.ToString("N0") đ</span>
                                                            </div>
                                                            <div style="width: 100%; height: 4px; background: rgba(255,255,255,0.08); border-radius: 2px; position: relative;">
                                                                @{
                                                                    var low = tx.PriceLowSinceBuy.Value;
                                                                    var high = tx.PriceHighSinceBuy.Value;
                                                                    var current = tx.Price;
                                                                    var percent = (current - low) / (high - low) * 100m;
                                                                    percent = Math.Clamp(percent, 0m, 100m);
                                                                }
                                                                <div style="position: absolute; left: @percent%; top: -2px; width: 8px; height: 8px; border-radius: 50%; background: var(--accent-cyan); transform: translateX(-50%);"></div>
                                                            </div>
                                                        </div>
                                                    }
                                                }
                                            </div>
                                        }
                                    </div>
                                </div>
                            }
                        </div>
                    </div>
                }
            </div>
        }
    </div>"""

if target_closed_block not in code:
    target_closed_block = target_closed_block.replace('\n', '\r\n')
    replacement_closed_block = replacement_closed_block.replace('\n', '\r\n')

if target_closed_block in code:
    code = code.replace(target_closed_block, replacement_closed_block)
    print("Fixed closed positions block at bottom")
else:
    print("Could not find closed positions block at bottom")

with open(filepath, "w", encoding="utf-8") as f:
    f.write(code)

print("Portfolio.cshtml successfully updated!")
