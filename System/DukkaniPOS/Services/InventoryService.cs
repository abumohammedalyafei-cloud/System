using System;
using System.Collections.Generic;
using System.Data.SQLite;
using DukkaniPOS.Data;
using DukkaniPOS.Models;

namespace DukkaniPOS.Services
{
    public class InventoryService
    {
        public bool AddProduct(Product product)
        {
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Products (ProductID, ProductName, CostPrice, SellingPrice, StockQuantity, MinStockWarning)
                VALUES (@id, @name, @cost, @selling, @qty, @min);";
            
            cmd.Parameters.AddWithValue("@id", product.ProductID);
            cmd.Parameters.AddWithValue("@name", product.ProductName);
            cmd.Parameters.AddWithValue("@cost", product.CostPrice);
            cmd.Parameters.AddWithValue("@selling", product.SellingPrice);
            cmd.Parameters.AddWithValue("@qty", product.StockQuantity);
            cmd.Parameters.AddWithValue("@min", product.MinStockWarning);

            return cmd.ExecuteNonQuery() > 0;
        }

        public bool UpdateProduct(Product product)
        {
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Products 
                SET ProductName = @name, 
                    CostPrice = @cost, 
                    SellingPrice = @selling, 
                    StockQuantity = @qty, 
                    MinStockWarning = @min
                WHERE ProductID = @id;";

            cmd.Parameters.AddWithValue("@id", product.ProductID);
            cmd.Parameters.AddWithValue("@name", product.ProductName);
            cmd.Parameters.AddWithValue("@cost", product.CostPrice);
            cmd.Parameters.AddWithValue("@selling", product.SellingPrice);
            cmd.Parameters.AddWithValue("@qty", product.StockQuantity);
            cmd.Parameters.AddWithValue("@min", product.MinStockWarning);

            return cmd.ExecuteNonQuery() > 0;
        }

        public Product? GetProductById(string productID)
        {
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ProductID, ProductName, CostPrice, SellingPrice, StockQuantity, MinStockWarning FROM Products WHERE ProductID = @id;";
            cmd.Parameters.AddWithValue("@id", productID);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return MapProduct(reader);
            }
            return null;
        }

        public List<Product> GetAllProducts()
        {
            var list = new List<Product>();
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ProductID, ProductName, CostPrice, SellingPrice, StockQuantity, MinStockWarning FROM Products ORDER BY ProductName;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(MapProduct(reader));
            }
            return list;
        }

        public List<Product> GetLowStockProducts()
        {
            var list = new List<Product>();
            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ProductID, ProductName, CostPrice, SellingPrice, StockQuantity, MinStockWarning FROM Products WHERE StockQuantity <= MinStockWarning ORDER BY StockQuantity ASC;";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(MapProduct(reader));
            }
            return list;
        }

        public bool StockIntake(string productID, int quantityAdded, decimal unitCost)
        {
            if (quantityAdded <= 0)
                throw new ArgumentException("Quantity added must be greater than zero.");

            using var conn = DatabaseHelper.GetConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                // 1. Update product stock quantity & update cost price if unit cost specified
                using (var updateCmd = conn.CreateCommand())
                {
                    updateCmd.Transaction = trans;
                    updateCmd.CommandText = @"
                        UPDATE Products 
                        SET StockQuantity = StockQuantity + @qty,
                            CostPrice = CASE WHEN @cost > 0 THEN @cost ELSE CostPrice END
                        WHERE ProductID = @id;";

                    updateCmd.Parameters.AddWithValue("@qty", quantityAdded);
                    updateCmd.Parameters.AddWithValue("@cost", unitCost);
                    updateCmd.Parameters.AddWithValue("@id", productID);

                    int rows = updateCmd.ExecuteNonQuery();
                    if (rows == 0)
                    {
                        trans.Rollback();
                        return false;
                    }
                }

                // 2. Log Purchase record
                using (var logCmd = conn.CreateCommand())
                {
                    logCmd.Transaction = trans;
                    logCmd.CommandText = @"
                        INSERT INTO Purchases (ProductID, QuantityAdded, UnitCost, PurchaseDate)
                        VALUES (@id, @qty, @cost, @date);";

                    logCmd.Parameters.AddWithValue("@id", productID);
                    logCmd.Parameters.AddWithValue("@qty", quantityAdded);
                    logCmd.Parameters.AddWithValue("@cost", unitCost);
                    logCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                    logCmd.ExecuteNonQuery();
                }

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        private static Product MapProduct(SQLiteDataReader reader)
        {
            return new Product
            {
                ProductID = reader.GetString(0),
                ProductName = reader.GetString(1),
                CostPrice = Convert.ToDecimal(reader.GetDouble(2)),
                SellingPrice = Convert.ToDecimal(reader.GetDouble(3)),
                StockQuantity = reader.GetInt32(4),
                MinStockWarning = reader.GetInt32(5)
            };
        }
    }
}
