using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using DukkaniPOS.Models;

namespace DukkaniPOS.Services
{
    public class ExportService
    {
        public static string ExportProductsToCsv(List<Product> products)
        {
            string fileName = $"Inventory_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Dukkani_Exports");
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("الباركود,اسم المنتج,سعر التكلفة (YER),سعر البيع (YER),الكمية المتاحة,حد التنبيه,حالة المخزون");

            foreach (var p in products)
            {
                sb.AppendLine($"\"{p.ProductID}\",\"{p.ProductName}\",{p.CostPrice},{p.SellingPrice},{p.StockQuantity},{p.MinStockWarning},\"{p.StockStatusText}\"");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            TryOpenFile(filePath);
            return filePath;
        }

        public static string ExportCustomersToCsv(List<Customer> customers)
        {
            string fileName = $"Customers_Debts_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Dukkani_Exports");
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("معرف العميل,اسم العميل,رقم الهاتف,إجمالي الدين (YER),السقف الائتماني (YER),الائتمان المتبقي (YER)");

            foreach (var c in customers)
            {
                sb.AppendLine($"{c.CustomerID},\"{c.CustomerName}\",\"{c.Phone}\",{c.TotalDebt},{c.CreditLimit},{c.RemainingCredit}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            TryOpenFile(filePath);
            return filePath;
        }

        public static string ExportDailyReportToCsv(DailyReport report)
        {
            string fileName = $"Daily_Report_{report.Date:yyyyMMdd}.csv";
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Dukkani_Exports");
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, fileName);

            var sb = new StringBuilder();
            sb.AppendLine($"تقرير المبيعات والتحليلات اليومية - متاجر دكاني البيضاء - تاريخ: {report.Date:yyyy-MM-dd}");
            sb.AppendLine($"إجمالي المبيعات (YER),{report.TotalRevenue}");
            sb.AppendLine($"إجمالي رأس مال التكلفة (YER),{report.TotalCapitalCost}");
            sb.AppendLine($"صافي الربح (YER),{report.NetProfit}");
            sb.AppendLine($"عدد العمليات المنفذة,{report.TotalTransactions}");
            sb.AppendLine($"إجمالي القطع المباعة,{report.TotalItemsSold}");
            sb.AppendLine();
            sb.AppendLine("تفاصيل المعاملات والمبيعات:");
            sb.AppendLine("رقم المعاملة,الباركود,اسم المنتج,الكمية المباعة,سعر البيع (YER),التكلفة (YER),الإجمالي (YER),التاريخ والوقت");

            foreach (var s in report.SalesDetails)
            {
                sb.AppendLine($"{s.SaleID},\"{s.ProductID}\",\"{s.ProductName}\",{s.QuantitySold},{s.SellingPrice},{s.CostPrice},{s.TotalRevenue},\"{s.SaleDate:yyyy-MM-dd HH:mm:ss}\"");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            TryOpenFile(filePath);
            return filePath;
        }

        private static void TryOpenFile(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore if process start fails
            }
        }
    }
}
