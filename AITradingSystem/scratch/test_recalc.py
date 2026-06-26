import pymssql

def main():
    conn = pymssql.connect(server='WIN-NT0F62URHT9\\ANND', database='AITradingDB_v1')
    cursor = conn.cursor(as_dict=True)
    
    cursor.execute("SELECT * FROM StockTransactions WHERE Symbol = 'POW' ORDER BY TransactionDate")
    txs = cursor.fetchall()
    
    print(f"Total transactions found: {len(txs)}")
    
    buy_lots = []
    for tx in txs:
        tx_type = tx['TransactionType'].strip()
        print(f"\nProcessing transaction: Id={tx['Id']}, Date={tx['TransactionDate']}, Type={tx_type}, Qty={tx['Quantity']}, Price={tx['Price']}")
        if tx_type == "BUY":
            buy_lots.append({'Price': tx['Price'], 'RemainingQuantity': tx['Quantity']})
            print(f"Added to buy_lots. Current buy_lots: {buy_lots}")
        elif tx_type == "SELL":
            sell_qty = tx['Quantity']
            cost_of_goods_sold = 0
            matched_details = []
            
            while sell_qty > 0 and len(buy_lots) > 0:
                lot = buy_lots[0]
                matched_qty = min(sell_qty, lot['RemainingQuantity'])
                cost_of_goods_sold += matched_qty * lot['Price']
                sell_qty -= matched_qty
                lot['RemainingQuantity'] -= matched_qty
                matched_details.append((matched_qty, lot['Price']))
                
                if lot['RemainingQuantity'] == 0:
                    buy_lots.pop(0)
            
            if sell_qty > 0:
                cost_of_goods_sold += sell_qty * tx['Price']
                matched_details.append((sell_qty, tx['Price']))
            
            fee = tx['Fee'] if tx['Fee'] is not None else 0
            tax = tx['Tax'] if tx['Tax'] is not None else 0
            pnl = (tx['Quantity'] * tx['Price']) - cost_of_goods_sold - fee - tax
            print(f"Calculated costOfGoodsSold={cost_of_goods_sold} (matches: {matched_details})")
            print(f"Fee={fee}, Tax={tax}")
            print(f"Calculated PnlAmount={pnl}")
            
    conn.close()

if __name__ == "__main__":
    main()
