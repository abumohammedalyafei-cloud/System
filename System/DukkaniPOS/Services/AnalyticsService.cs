using System;
using System.Collections.Generic;
using System.Data.SQLite;
using DukkaniPOS.Data;
using DukkaniPOS.Models;

namespace DukkaniPOS.Services
{
    public class AnalyticsService
    {
        private readonly InventoryService _inventoryService;

        public AnalyticsService(InventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        public DailyReport GetDailyReport(DateTime targetDate)
        {
            var report = new DailyReport
            {
                Date = targetDate.Date,
                LowStockProducts = _inventoryService.GetLowStockProducts()
            };

            string datePattern = targetDate.ToString("yyyy-MM-dd") + "%";

            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT s.SaleID, s.ProductID, p.ProductName, s.QuantitySold, s.SellingPrice, s.CostPrice, s.SaleDate, s.CustomerID 
                FROM Sales s
                JOIN Products p ON s.ProductID = p.ProductID
                WHERE s.SaleDate LIKE @datePattern
                ORDER BY s.SaleDate DESC;";
            cmd.Parameters.AddWithValue("@datePattern", datePattern);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var sale = new Sale
                {
                    SaleID = reader.GetInt64(0),
                    ProductID = reader.GetString(1),
                    ProductName = reader.GetString(2),
                    QuantitySold = reader.GetInt32(3),
                    SellingPrice = Convert.ToDecimal(reader.GetDouble(4)),
                    CostPrice = Convert.ToDecimal(reader.GetDouble(5)),
                    SaleDate = DateTime.Parse(reader.GetString(6)),
                    CustomerID = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                };

                report.SalesDetails.Add(sale);
                report.TotalRevenue += sale.TotalRevenue;
                report.TotalCapitalCost += sale.TotalCost;
                report.TotalItemsSold += sale.QuantitySold;
            }

            report.TotalTransactions = report.SalesDetails.Count;
            return report;
        }

        public bool CloseDailyShift(DateTime targetDate)
        {
            var report = GetDailyReport(targetDate);

            using var conn = DatabaseHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Shifts (ShiftDate, TotalRevenue, TotalCost, NetProfit, TotalTransactions, ClosedAt)
                VALUES (@sdate, @rev, @cost, @profit, @txs, @closed);";

            cmd.Parameters.AddWithValue("@sdate", targetDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@rev", report.TotalRevenue);
            cmd.Parameters.AddWithValue("@cost", report.TotalCapitalCost);
            cmd.Parameters.AddWithValue("@profit", report.NetProfit);
            cmd.Parameters.AddWithValue("@txs", report.TotalTransactions);
            cmd.Parameters.AddWithValue("@closed", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
