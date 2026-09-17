using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using DukkaniPOS.Data;
using DukkaniPOS.Models;

namespace DukkaniPOS.Services
{
    public class POSService
    {
        private readonly DebtService _debtService;

        public POSService(DebtService debtService)
        {
            _debtService = debtService;
        }

        public long ProcessSale(List<CartItem> cart, int? customerId = null)
        {
            if (cart == null || cart.Count == 0)
                throw new ArgumentException("Cart cannot be empty.");

            using var conn = DatabaseHelper.GetConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                decimal totalSaleAmount = 0;
                string saleDateStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Build Itemized Description for Receipt & Debt Transaction Notes
                var itemSummaries = new List<string>();
                foreach (var item in cart)
                {
                    itemSummaries.Add($"{item.Product.ProductName} ({item.Quantity} قطعة × {item.Product.SellingPrice:N0} = {item.TotalAmount:N0} YER)");
                }
                string itemizedNote = string.Join(" | ", itemSummaries);

                // 1. Check stock availability
                foreach (var item in cart)
                {
                    using var checkCmd = conn.CreateCommand();
                    checkCmd.Transaction = trans;
                    checkCmd.CommandText = "SELECT StockQuantity, ProductName, CostPrice, SellingPrice FROM Products WHERE ProductID = @id;";
                    checkCmd.Parameters.AddWithValue("@id", item.Product.ProductID);

                    using var reader = checkCmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException($"Product '{item.Product.ProductName}' (ID: {item.Product.ProductID}) no longer exists.");
                    }

                    int currentStock = reader.GetInt32(0);
                    if (currentStock < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for '{item.Product.ProductName}'! Available: {currentStock}, Requested: {item.Quantity}.");
                    }

                    item.Product.CostPrice = Convert.ToDecimal(reader.GetDouble(2));
                    item.Product.SellingPrice = Convert.ToDecimal(reader.GetDouble(3));

                    totalSaleAmount += item.TotalAmount;
                }

                // 2. If Debt Sale, verify credit limit
                if (customerId.HasValue)
                {
                    using var custCmd = conn.CreateCommand();
                    custCmd.Transaction = trans;
                    custCmd.CommandText = "SELECT TotalDebt, CreditLimit, CustomerName FROM Customers WHERE CustomerID = @cid;";
                    custCmd.Parameters.AddWithValue("@cid", customerId.Value);

                    using var custReader = custCmd.ExecuteReader();
                    if (!custReader.Read())
                    {
                        throw new InvalidOperationException($"Customer ID #{customerId.Value} not found.");
                    }

                    decimal currentDebt = Convert.ToDecimal(custReader.GetDouble(0));
                    decimal creditLimit = Convert.ToDecimal(custReader.GetDouble(1));
                    string customerName = custReader.GetString(2);

                    if (currentDebt + totalSaleAmount > creditLimit)
                    {
                        throw new InvalidOperationException(
                            $"Debt Sale rejected for {customerName}! Sale amount ({totalSaleAmount:N0} YER) exceeds remaining credit limit ({(creditLimit - currentDebt):N0} YER).");
                    }

                    // Update Customer Debt
                    using var updateDebtCmd = conn.CreateCommand();
                    updateDebtCmd.Transaction = trans;
                    updateDebtCmd.CommandText = "UPDATE Customers SET TotalDebt = TotalDebt + @amt WHERE CustomerID = @cid;";
                    updateDebtCmd.Parameters.AddWithValue("@amt", totalSaleAmount);
                    updateDebtCmd.Parameters.AddWithValue("@cid", customerId.Value);
                    updateDebtCmd.ExecuteNonQuery();

                    // Log Debt Transaction with Itemized Notes!
                    using var logDebtCmd = conn.CreateCommand();
                    logDebtCmd.Transaction = trans;
                    logDebtCmd.CommandText = @"
                        INSERT INTO DebtTransactions (CustomerID, Type, Amount, TransactionDate, Notes)
                        VALUES (@cid, 'DEBT', @amt, @date, @notes);";
                    logDebtCmd.Parameters.AddWithValue("@cid", customerId.Value);
                    logDebtCmd.Parameters.AddWithValue("@amt", totalSaleAmount);
                    logDebtCmd.Parameters.AddWithValue("@date", saleDateStr);
                    logDebtCmd.Parameters.AddWithValue("@notes", $"شراء آجل: {itemizedNote}");
                    logDebtCmd.ExecuteNonQuery();
                }

                // 3. Process each item: update stock and insert sale record
                long firstSaleId = 0;
                foreach (var item in cart)
                {
                    using var updateStockCmd = conn.CreateCommand();
                    updateStockCmd.Transaction = trans;
                    updateStockCmd.CommandText = "UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE ProductID = @id;";
                    updateStockCmd.Parameters.AddWithValue("@qty", item.Quantity);
                    updateStockCmd.Parameters.AddWithValue("@id", item.Product.ProductID);
                    updateStockCmd.ExecuteNonQuery();

                    using var insertSaleCmd = conn.CreateCommand();
                    insertSaleCmd.Transaction = trans;
                    insertSaleCmd.CommandText = @"
                        INSERT INTO Sales (ProductID, QuantitySold, SellingPrice, CostPrice, SaleDate, CustomerID)
                        VALUES (@pid, @qty, @selling, @cost, @date, @cid);
                        SELECT last_insert_rowid();";
                    insertSaleCmd.Parameters.AddWithValue("@pid", item.Product.ProductID);
                    insertSaleCmd.Parameters.AddWithValue("@qty", item.Quantity);
                    insertSaleCmd.Parameters.AddWithValue("@selling", item.Product.SellingPrice);
                    insertSaleCmd.Parameters.AddWithValue("@cost", item.Product.CostPrice);
                    insertSaleCmd.Parameters.AddWithValue("@date", saleDateStr);
                    insertSaleCmd.Parameters.AddWithValue("@cid", (object?)customerId ?? DBNull.Value);

                    long insertedId = (long)insertSaleCmd.ExecuteScalar()!;
                    if (firstSaleId == 0) firstSaleId = insertedId;
                }

                trans.Commit();
                return firstSaleId;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        public bool VoidSale(long saleId)
        {
            using var conn = DatabaseHelper.GetConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                using var fetchCmd = conn.CreateCommand();
                fetchCmd.Transaction = trans;
                fetchCmd.CommandText = "SELECT ProductID, QuantitySold, SellingPrice, CustomerID FROM Sales WHERE SaleID = @sid;";
                fetchCmd.Parameters.AddWithValue("@sid", saleId);

                using var reader = fetchCmd.ExecuteReader();
                if (!reader.Read())
                {
                    throw new InvalidOperationException($"المعاملة / الفاتورة رقم #{saleId} غير موجودة بالسجلات!");
                }

                string productId = reader.GetString(0);
                int quantitySold = reader.GetInt32(1);
                decimal sellingPrice = Convert.ToDecimal(reader.GetDouble(2));
                int? customerId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                decimal totalAmount = sellingPrice * quantitySold;

                using var restockCmd = conn.CreateCommand();
                restockCmd.Transaction = trans;
                restockCmd.CommandText = "UPDATE Products SET StockQuantity = StockQuantity + @qty WHERE ProductID = @pid;";
                restockCmd.Parameters.AddWithValue("@qty", quantitySold);
                restockCmd.Parameters.AddWithValue("@pid", productId);
                restockCmd.ExecuteNonQuery();

                if (customerId.HasValue)
                {
                    using var revertDebtCmd = conn.CreateCommand();
                    revertDebtCmd.Transaction = trans;
                    revertDebtCmd.CommandText = "UPDATE Customers SET TotalDebt = MAX(0, TotalDebt - @amt) WHERE CustomerID = @cid;";
                    revertDebtCmd.Parameters.AddWithValue("@amt", totalAmount);
                    revertDebtCmd.Parameters.AddWithValue("@cid", customerId.Value);
                    revertDebtCmd.ExecuteNonQuery();

                    using var logDebtCmd = conn.CreateCommand();
                    logDebtCmd.Transaction = trans;
                    logDebtCmd.CommandText = @"
                        INSERT INTO DebtTransactions (CustomerID, Type, Amount, TransactionDate, Notes)
                        VALUES (@cid, 'REPAYMENT', @amt, @date, @notes);";
                    logDebtCmd.Parameters.AddWithValue("@cid", customerId.Value);
                    logDebtCmd.Parameters.AddWithValue("@amt", totalAmount);
                    logDebtCmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    logDebtCmd.Parameters.AddWithValue("@notes", $"إلغاء المعاملة وإسترجاع البضاعة - فاتورة #{saleId}");
                    logDebtCmd.ExecuteNonQuery();
                }

                using var deleteCmd = conn.CreateCommand();
                deleteCmd.Transaction = trans;
                deleteCmd.CommandText = "DELETE FROM Sales WHERE SaleID = @sid;";
                deleteCmd.Parameters.AddWithValue("@sid", saleId);
                deleteCmd.ExecuteNonQuery();

                trans.Commit();
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }
    }
}
