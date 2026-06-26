import os

def main():
    file_path = r"d:\NDA\NEW\Tool_Manager\AITradingSystem\Services\DnseService.cs"
    with open(file_path, "r", encoding="utf-8") as f:
        content = f.read()

    normalized_content = content.replace("\r\n", "\n")

    target = """                                    if (existingTx == null)
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

    replacement = """                                    if (existingTx == null)
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

    if target in normalized_content:
        normalized_content = normalized_content.replace(target, replacement)
        content = normalized_content.replace("\n", "\r\n")
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(content)
        print("Replaced successfully.")
    else:
        print("Target block not found.")

if __name__ == "__main__":
    main()
