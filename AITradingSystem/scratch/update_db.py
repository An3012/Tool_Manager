import pymssql

def main():
    conn = pymssql.connect(server='WIN-NT0F62URHT9\\ANND', database='AITradingDB_v1')
    cursor = conn.cursor()
    
    print("Updating TradePositions: Setting POW OPEN positions to CLOSED with 0 quantity...")
    cursor.execute("UPDATE TradePositions SET Status = 'CLOSED', Quantity = 0 WHERE Symbol = 'POW' AND Status = 'OPEN'")
    conn.commit()
    print(f"Updated rows: {cursor.rowcount}")
    
    conn.close()

if __name__ == "__main__":
    main()
