import pymssql

def main():
    conn = pymssql.connect(server='WIN-NT0F62URHT9\\ANND', database='AITradingDB_v1')
    cursor = conn.cursor(as_dict=True)
    
    # 1. Update Tax columns in StockTransactions
    print("Updating Tax column for POW transactions...")
    cursor.execute("UPDATE StockTransactions SET Tax = 386.0000 WHERE Id = 1004")
    cursor.execute("UPDATE StockTransactions SET Tax = 325.0000 WHERE Id = 1006")
    cursor.execute("UPDATE StockTransactions SET Tax = 0.0000 WHERE Id = 1008")
    conn.commit()
    
    # 2. Recalculate PnlAmount for POW
    cursor.execute("SELECT * FROM StockTransactions WHERE Symbol = 'POW' ORDER BY TransactionDate")
    txs = cursor.fetchall()
    
    buy_lots = []
    for tx in txs:
        tx_type = tx['TransactionType'].strip()
        if tx_type == "BUY":
            buy_lots.append({'Price': tx['Price'], 'RemainingQuantity': tx['Quantity']})
        elif tx_type == "SELL":
            sell_qty = tx['Quantity']
            cost_of_goods_sold = 0
            
            while sell_qty > 0 and len(buy_lots) > 0:
                lot = buy_lots[0]
                matched_qty = min(sell_qty, lot['RemainingQuantity'])
                cost_of_goods_sold += matched_qty * lot['Price']
                sell_qty -= matched_qty
                lot['RemainingQuantity'] -= matched_qty
                
                if lot['RemainingQuantity'] == 0:
                    buy_lots.pop(0)
            
            if sell_qty > 0:
                cost_of_goods_sold += sell_qty * tx['Price']
            
            fee = tx['Fee'] if tx['Fee'] is not None else 0
            tax = tx['Tax'] if tx['Tax'] is not None else 0
            pnl = (tx['Quantity'] * tx['Price']) - cost_of_goods_sold - fee - tax
            
            print(f"Transaction Id {tx['Id']}: Qty={tx['Quantity']}, Price={tx['Price']}, Fee={fee}, Tax={tax}, PnlAmount={pnl}")
            cursor.execute("UPDATE StockTransactions SET PnlAmount = %s WHERE Id = %s", (pnl, tx['Id']))
            
    conn.commit()
    
    # 3. Update TradePositions PnL for POW
    cursor.execute("SELECT SUM(PnlAmount) as total_pnl FROM StockTransactions WHERE Symbol = 'POW' AND TransactionType = 'SELL'")
    total_pnl = cursor.fetchone()['total_pnl']
    print(f"Total POW realized PnL: {total_pnl}")
    
    cursor.execute("UPDATE TradePositions SET PnL = %s WHERE Symbol = 'POW' AND Status = 'CLOSED'", (total_pnl,))
    conn.commit()
    print("Database updated successfully.")
    
    conn.close()

if __name__ == "__main__":
    main()
