import pymssql

def main():
    conn = pymssql.connect(server='WIN-NT0F62URHT9\\ANND', database='AITradingDB_v1')
    cursor = conn.cursor()
    
    print("=== USER PREFERENCES ===")
    cursor.execute("SELECT DnseUsername, DnsePassword, TargetAmount, TargetProfitPercentage, MaxLossPercentage, PlanStartDate, InvestmentHorizon FROM UserPreferences")
    for row in cursor.fetchall():
        print(row)
        
    print("\n=== TRADE POSITIONS ===")
    cursor.execute("SELECT Id, Symbol, Quantity, EntryPrice, Status, PnL, InvestedAmount, BudgetAmount, ExpectedHoldDays FROM TradePositions")
    for row in cursor.fetchall():
        print(row)
        
    print("\n=== STOCK TRANSACTIONS ===")
    cursor.execute("SELECT Id, Symbol, TransactionType, Quantity, Price, TransactionDate, PnlAmount, Source, TimingScore FROM StockTransactions")
    for row in cursor.fetchall():
        print(row)
        
    conn.close()

if __name__ == "__main__":
    main()
