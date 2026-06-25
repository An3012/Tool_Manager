# pyrefly: ignore [missing-import]
import pymssql

def main():
    conn = pymssql.connect(server='WIN-NT0F62URHT9\\ANND', database='AITradingDB_v1')
    cursor = conn.cursor()
    
    print("Updating active open positions BudgetAmount to NULL...")
    cursor.execute("UPDATE TradePositions SET BudgetAmount = NULL WHERE Status = 'OPEN'")
    conn.commit()
    print(f"Updated {cursor.rowcount} row(s).")
    
    cursor.execute("SELECT Id, Symbol, Quantity, EntryPrice, InvestedAmount, BudgetAmount FROM TradePositions WHERE Status = 'OPEN'")
    for row in cursor.fetchall():
        print(row)
        
    conn.close()

if __name__ == "__main__":
    main()
