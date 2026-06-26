import pymssql

def main():
    conn = pymssql.connect(server='WIN-NT0F62URHT9\\ANND', database='AITradingDB_v1')
    cursor = conn.cursor()
    
    # Check columns of StockTransactions
    print("Checking columns for StockTransactions...")
    cursor.execute("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'StockTransactions'")
    cols = [r[0] for r in cursor.fetchall()]
    print("Current columns:", cols)
    
    if 'Tax' not in cols:
        print("Adding Tax column to StockTransactions...")
        cursor.execute("ALTER TABLE StockTransactions ADD Tax DECIMAL(18,4) NULL")
        conn.commit()
        print("Tax column added successfully.")
    else:
        print("Tax column already exists in StockTransactions.")
        
    # Check columns of AiStockTransactions
    print("\nChecking columns for AiStockTransactions...")
    cursor.execute("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'AiStockTransactions'")
    cols_ai = [r[0] for r in cursor.fetchall()]
    print("Current columns:", cols_ai)
    
    if cols_ai:
        if 'Tax' not in cols_ai:
            print("Adding Tax column to AiStockTransactions...")
            cursor.execute("ALTER TABLE AiStockTransactions ADD Tax DECIMAL(18,4) NULL")
            conn.commit()
            print("Tax column added successfully.")
        else:
            print("Tax column already exists in AiStockTransactions.")
    else:
        print("Table AiStockTransactions not found in INFORMATION_SCHEMA.")
        
    conn.close()

if __name__ == '__main__':
    main()
