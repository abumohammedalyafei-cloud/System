using System;
using System.Data.SQLite;
using System.IO;

namespace DukkaniPOS.Data
{
    public static class DatabaseHelper
    {
        private static readonly string DbFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string DbPath = Path.Combine(DbFolder, "dukkani.db");
        public static string ConnectionString => $"Data Source={DbPath};Version=3;";

        public static SQLiteConnection GetConnection()
        {
            var conn = new SQLiteConnection(ConnectionString);
            conn.Open();

            // Enable SQLite Foreign Key Constraints
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        public static void InitializeDatabase()
        {
            if (!File.Exists(DbPath))
            {
                SQLiteConnection.CreateFile(DbPath);
            }

            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Products (
                    ProductID TEXT PRIMARY KEY,
                    ProductName TEXT NOT NULL,
                    CostPrice REAL NOT NULL,
                    SellingPrice REAL NOT NULL,
                    StockQuantity INTEGER NOT NULL,
                    MinStockWarning INTEGER NOT NULL DEFAULT 5
                );

                CREATE TABLE IF NOT EXISTS Customers (
                    CustomerID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerName TEXT NOT NULL,
                    Phone TEXT NOT NULL,
                    TotalDebt REAL NOT NULL DEFAULT 0,
                    CreditLimit REAL NOT NULL DEFAULT 50000
                );

                CREATE TABLE IF NOT EXISTS Sales (
                    SaleID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductID TEXT NOT NULL,
                    QuantitySold INTEGER NOT NULL,
                    SellingPrice REAL NOT NULL,
                    CostPrice REAL NOT NULL,
                    SaleDate TEXT NOT NULL,
                    CustomerID INTEGER NULL,
                    FOREIGN KEY (ProductID) REFERENCES Products(ProductID),
                    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID)
                );

                CREATE TABLE IF NOT EXISTS Purchases (
                    PurchaseID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProductID TEXT NOT NULL,
                    QuantityAdded INTEGER NOT NULL,
                    UnitCost REAL NOT NULL,
                    PurchaseDate TEXT NOT NULL,
                    FOREIGN KEY (ProductID) REFERENCES Products(ProductID)
                );

                CREATE TABLE IF NOT EXISTS DebtTransactions (
                    TransactionID INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerID INTEGER NOT NULL,
                    Type TEXT NOT NULL, -- 'DEBT' or 'REPAYMENT'
                    Amount REAL NOT NULL,
                    TransactionDate TEXT NOT NULL,
                    Notes TEXT NULL,
                    FOREIGN KEY (CustomerID) REFERENCES Customers(CustomerID)
                );

                CREATE TABLE IF NOT EXISTS Shifts (
                    ShiftID INTEGER PRIMARY KEY AUTOINCREMENT,
                    ShiftDate TEXT NOT NULL,
                    TotalRevenue REAL NOT NULL,
                    TotalCost REAL NOT NULL,
                    NetProfit REAL NOT NULL,
                    TotalTransactions INTEGER NOT NULL,
                    ClosedAt TEXT NOT NULL
                );
            ";

            cmd.ExecuteNonQuery();

            SeedInitialData(conn);
        }

        private static void SeedInitialData(SQLiteConnection conn)
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Products;";
            long count = (long)checkCmd.ExecuteScalar()!;

            if (count == 0)
            {
                using var trans = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = trans;

                // Seed Products (Prices in YER - Yemeni Rial)
                cmd.CommandText = @"
                    INSERT INTO Products (ProductID, ProductName, CostPrice, SellingPrice, StockQuantity, MinStockWarning) VALUES
                    ('6291001', 'بن يمني فاخر (مطحون) 500ج', 4500, 6000, 20, 5),
                    ('6291002', 'عسل سدر ملكي بيضاني 1 كجم', 25000, 32000, 8, 3),
                    ('6291003', 'دقيق السعيد 10 كجم', 5000, 6500, 15, 4),
                    ('6291004', 'شاي الكبوس ممتاز 227ج', 1200, 1600, 40, 10),
                    ('6291005', 'مياه معدنية شملان 1.5 لتر', 250, 350, 100, 20);

                    INSERT INTO Customers (CustomerName, Phone, TotalDebt, CreditLimit) VALUES
                    ('الشيخ محمد البيضاني', '770123456', 0, 100000),
                    ('أحمد علي سالم', '733987654', 0, 50000);
                ";

                cmd.ExecuteNonQuery();
                trans.Commit();
            }
        }
    }
}
