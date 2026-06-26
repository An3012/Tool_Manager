import os

def main():
    file_path = r"d:\NDA\NEW\Tool_Manager\AITradingSystem\Services\DnseService.cs"
    with open(file_path, "r", encoding="utf-8", newline="") as f:
        content = f.read()

    # 1. Backfill Tax and Fee block
    target_import = """                                     if (existingTx == null)
                                     {
                                         // Link to position if possible
                                         var position = await _context.TradePositions
                                             .FirstOrDefaultAsync(p => p.Symbol == symbol && p.Status == "OPEN");

                                         _context.StockTransactions.Add(new StockTransaction
                                         {
                                             Symbol = symbol,
                                             TransactionType = typeText,
                                             Quantity = qty,
                                             Price = price,
                                             TransactionDate = txDate,
                                             Fee = fee,
                                             Tax = tax,
                                             TotalAmount = qty * price,
                                             PnlAmount = null, // Will be recalculated by RecalculateAllTransactionsPnlAsync
                                             Source = "DNSE",
                                             PositionId = position?.Id,
                                             Notes = "Imported from DNSE Order History"
                                         });
                                         importCount++;
                                     }"""

    replacement_import = """                                     if (existingTx == null)
                                     {
                                         // Link to position if possible
                                         var position = await _context.TradePositions
                                             .FirstOrDefaultAsync(p => p.Symbol == symbol && p.Status == "OPEN");

                                         _context.StockTransactions.Add(new StockTransaction
                                         {
                                             Symbol = symbol,
                                             TransactionType = typeText,
                                             Quantity = qty,
                                             Price = price,
                                             TransactionDate = txDate,
                                             Fee = fee,
                                             Tax = tax,
                                             TotalAmount = qty * price,
                                             PnlAmount = null, // Will be recalculated by RecalculateAllTransactionsPnlAsync
                                             Source = "DNSE",
                                             PositionId = position?.Id,
                                             Notes = "Imported from DNSE Order History"
                                         });
                                         importCount++;
                                     }
                                     else
                                     {
                                         bool updated = false;
                                         if (existingTx.Tax == null && tax > 0)
                                         {
                                             existingTx.Tax = tax;
                                             updated = true;
                                         }
                                         if (existingTx.Fee == null && fee > 0)
                                         {
                                             existingTx.Fee = fee;
                                             updated = true;
                                         }
                                         if (updated)
                                         {
                                             _context.StockTransactions.Update(existingTx);
                                             importCount++;
                                         }
                                     }"""

    # 2. Caching quantity in auto-close loop
    target_autoclose = """                    foreach (var pos in existingOpenPositions)
                    {
                        if (!processedSymbols.Contains(pos.Symbol))
                        {
                            pos.Status = "CLOSED";
                            pos.Quantity = 0;
                            pos.ExitDate = DateTime.Now;
                            
                            // Try to find if there is a SELL price in detailed transactions
                            decimal exitPrice = pos.EntryPrice;
                            var lastSell = await _context.StockTransactions
                                .Where(t => t.Symbol == pos.Symbol && t.TransactionType == "SELL")
                                .OrderByDescending(t => t.TransactionDate)
                                .FirstOrDefaultAsync();
                                
                            if (lastSell != null)
                            {
                                exitPrice = lastSell.Price;
                            }
                            else
                            {
                                var stockState = _simulationLogService.GetStockState(pos.Symbol);
                                if (stockState != null)
                                {
                                    exitPrice = stockState.CurrentPrice;
                                }
                            }
                            
                            pos.ExitPrice = exitPrice;
                            
                            // Recalculate final realized PnL from transactions
                            var symbolSells = await _context.StockTransactions
                                .Where(t => t.Symbol == pos.Symbol && t.TransactionType == "SELL" && t.PnlAmount.HasValue)
                                .ToListAsync();
                                
                            if (symbolSells.Any())
                            {
                                pos.PnL = symbolSells.Sum(t => t.PnlAmount.Value);
                            }
                            else
                            {
                                pos.PnL = (exitPrice - pos.EntryPrice) * pos.Quantity;
                            }
                            
                            Console.WriteLine($"[DNSE Sync] Tự động đóng vị thế do không còn trong Deal thực tế: {pos.Symbol}");
                        }
                    }"""

    replacement_autoclose = """                    foreach (var pos in existingOpenPositions)
                    {
                        if (!processedSymbols.Contains(pos.Symbol))
                        {
                            int oldQty = pos.Quantity;
                            pos.Status = "CLOSED";
                            pos.Quantity = 0;
                            pos.ExitDate = DateTime.Now;
                            
                            // Try to find if there is a SELL price in detailed transactions
                            decimal exitPrice = pos.EntryPrice;
                            var lastSell = await _context.StockTransactions
                                .Where(t => t.Symbol == pos.Symbol && t.TransactionType == "SELL")
                                .OrderByDescending(t => t.TransactionDate)
                                .FirstOrDefaultAsync();
                                
                            if (lastSell != null)
                            {
                                exitPrice = lastSell.Price;
                            }
                            else
                            {
                                var stockState = _simulationLogService.GetStockState(pos.Symbol);
                                if (stockState != null)
                                {
                                    exitPrice = stockState.CurrentPrice;
                                }
                            }
                            
                            pos.ExitPrice = exitPrice;
                            
                            // Recalculate final realized PnL from transactions
                            var symbolSells = await _context.StockTransactions
                                .Where(t => t.Symbol == pos.Symbol && t.TransactionType == "SELL" && t.PnlAmount.HasValue)
                                .ToListAsync();
                                
                            if (symbolSells.Any())
                            {
                                pos.PnL = symbolSells.Sum(t => t.PnlAmount.Value);
                            }
                            else
                            {
                                pos.PnL = (exitPrice - pos.EntryPrice) * oldQty;
                            }
                            
                            Console.WriteLine($"[DNSE Sync] Tự động đóng vị thế do không còn trong Deal thực tế: {pos.Symbol}");
                        }
                    }"""

    # 3. Recalculate closedPosition.PnL in RecalculateAllTransactionsPnlAsync
    target_recalc = """        private async Task RecalculateAllTransactionsPnlAsync()
        {
            var allSymbols = await _context.StockTransactions
                .Select(t => t.Symbol)
                .Distinct()
                .ToListAsync();

            foreach (var symbol in allSymbols)
            {
                var txs = await _context.StockTransactions
                    .Where(t => t.Symbol == symbol)
                    .OrderBy(t => t.TransactionDate)
                    .ToListAsync();

                var buyLots = new List<TransactionLot>();
                foreach (var tx in txs)
                {
                    if (tx.TransactionType == "BUY")
                    {
                        buyLots.Add(new TransactionLot { Price = tx.Price, RemainingQuantity = tx.Quantity });
                    }
                    else if (tx.TransactionType == "SELL")
                    {
                        int sellQty = tx.Quantity;
                        decimal costOfGoodsSold = 0;
                        
                        while (sellQty > 0 && buyLots.Any())
                        {
                            var lot = buyLots.First();
                            int matchedQty = Math.Min(sellQty, lot.RemainingQuantity);
                            
                            costOfGoodsSold += matchedQty * lot.Price;
                            sellQty -= matchedQty;
                            lot.RemainingQuantity -= matchedQty;
                            
                            if (lot.RemainingQuantity == 0)
                            {
                                buyLots.RemoveAt(0);
                            }
                        }
                        
                        if (sellQty > 0)
                        {
                            costOfGoodsSold += sellQty * tx.Price;
                        }
                        
                        var fee = tx.Fee ?? 0m;
                        var tax = tx.Tax ?? 0m;
                        tx.PnlAmount = (tx.Quantity * tx.Price) - costOfGoodsSold - fee - tax;
                        _context.StockTransactions.Update(tx);
                    }
                }
            }
            await _context.SaveChangesAsync();
        }"""

    replacement_recalc = """        private async Task RecalculateAllTransactionsPnlAsync()
        {
            var allSymbols = await _context.StockTransactions
                .Select(t => t.Symbol)
                .Distinct()
                .ToListAsync();

            foreach (var symbol in allSymbols)
            {
                var txs = await _context.StockTransactions
                    .Where(t => t.Symbol == symbol)
                    .OrderBy(t => t.TransactionDate)
                    .ToListAsync();

                var buyLots = new List<TransactionLot>();
                foreach (var tx in txs)
                {
                    if (tx.TransactionType == "BUY")
                    {
                        buyLots.Add(new TransactionLot { Price = tx.Price, RemainingQuantity = tx.Quantity });
                    }
                    else if (tx.TransactionType == "SELL")
                    {
                        int sellQty = tx.Quantity;
                        decimal costOfGoodsSold = 0;
                        
                        while (sellQty > 0 && buyLots.Any())
                        {
                            var lot = buyLots.First();
                            int matchedQty = Math.Min(sellQty, lot.RemainingQuantity);
                            
                            costOfGoodsSold += matchedQty * lot.Price;
                            sellQty -= matchedQty;
                            lot.RemainingQuantity -= matchedQty;
                            
                            if (lot.RemainingQuantity == 0)
                            {
                                buyLots.RemoveAt(0);
                            }
                        }
                        
                        if (sellQty > 0)
                        {
                            costOfGoodsSold += sellQty * tx.Price;
                        }
                        
                        var fee = tx.Fee ?? 0m;
                        var tax = tx.Tax ?? 0m;
                        tx.PnlAmount = (tx.Quantity * tx.Price) - costOfGoodsSold - fee - tax;
                        _context.StockTransactions.Update(tx);
                    }
                }

                // Recalculate and update CLOSED positions PnL for this symbol
                var closedPosition = await _context.TradePositions
                    .FirstOrDefaultAsync(p => p.Symbol == symbol && p.Status == "CLOSED");
                if (closedPosition != null)
                {
                    var symbolSells = txs.Where(t => t.TransactionType == "SELL" && t.PnlAmount.HasValue).ToList();
                    if (symbolSells.Any())
                    {
                        closedPosition.PnL = symbolSells.Sum(t => t.PnlAmount.Value);
                        _context.TradePositions.Update(closedPosition);
                    }
                }
            }
            await _context.SaveChangesAsync();
        }"""

    # Normalize content to LF to do replacements safely
    lf_content = content.replace("\r\n", "\n")
    
    modified = False
    
    # 1
    lf_target_import = target_import.replace("\r\n", "\n")
    lf_repl_import = replacement_import.replace("\r\n", "\n")
    if lf_target_import in lf_content:
        lf_content = lf_content.replace(lf_target_import, lf_repl_import)
        print("Import block replaced.")
        modified = True
    else:
        print("Import block target not found!")
        
    # 2
    lf_target_autoclose = target_autoclose.replace("\r\n", "\n")
    lf_repl_autoclose = replacement_autoclose.replace("\r\n", "\n")
    if lf_target_autoclose in lf_content:
        lf_content = lf_content.replace(lf_target_autoclose, lf_repl_autoclose)
        print("Auto-close block replaced.")
        modified = True
    else:
        print("Auto-close block target not found!")
        
    # 3
    lf_target_recalc = target_recalc.replace("\r\n", "\n")
    lf_repl_recalc = replacement_recalc.replace("\r\n", "\n")
    if lf_target_recalc in lf_content:
        lf_content = lf_content.replace(lf_target_recalc, lf_repl_recalc)
        print("Recalc block replaced.")
        modified = True
    else:
        print("Recalc block target not found!")

    if modified:
        # Write back preserving Windows CRLF endings
        crlf_content = lf_content.replace("\n", "\r\n")
        with open(file_path, "w", encoding="utf-8", newline="") as f:
            f.write(crlf_content)
        print("All changes successfully applied to DnseService.cs.")
    else:
        print("No changes were applied.")

if __name__ == "__main__":
    main()
