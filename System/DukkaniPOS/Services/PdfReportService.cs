using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using DukkaniPOS.Models;

namespace DukkaniPOS.Services
{
    public class PdfReportService
    {
        public static string GenerateCustomerStatementHtmlPdf(Customer customer, List<DebtTransaction> transactions)
        {
            string fileName = $"Customer_Statement_{customer.CustomerID}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Dukkani_Reports");
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, fileName);

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ar\" dir=\"rtl\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<title>كشف حساب عميل تفصيلي - متاجر دكاني POS</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Tahoma, Arial, sans-serif; background-color: #F8FAFC; margin: 0; padding: 24px; direction: rtl; color: #0F172A; }");
            sb.AppendLine(".container { max-width: 850px; margin: 0 auto; background: #ffffff; padding: 32px; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); }");
            sb.AppendLine(".header { text-align: center; border-bottom: 2px solid #E2E8F0; padding-bottom: 16px; margin-bottom: 24px; }");
            sb.AppendLine(".header h1 { margin: 0; color: #2563EB; font-size: 26px; }");
            sb.AppendLine(".header p { margin: 4px 0 0 0; color: #64748B; font-size: 14px; }");
            sb.AppendLine(".info-grid { display: flex; justify-content: space-between; background: #F1F5F9; padding: 16px; border-radius: 8px; margin-bottom: 24px; }");
            sb.AppendLine(".info-item { font-size: 14px; }");
            sb.AppendLine(".info-item strong { color: #334155; }");
            sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 16px; }");
            sb.AppendLine("th, td { padding: 12px; text-align: right; border-bottom: 1px solid #E2E8F0; font-size: 13px; vertical-align: top; }");
            sb.AppendLine("th { background-color: #1E293B; color: #FFFFFF; font-weight: 600; }");
            sb.AppendLine("tr:nth-child(even) { background-color: #F8FAFC; }");
            sb.AppendLine(".type-debt { color: #DC2626; font-weight: bold; }");
            sb.AppendLine(".type-repay { color: #16A34A; font-weight: bold; }");
            sb.AppendLine(".item-list { margin: 4px 0 0 0; padding-right: 18px; color: #475569; font-size: 12px; }");
            sb.AppendLine(".footer { text-align: center; margin-top: 32px; font-size: 12px; color: #94A3B8; border-top: 1px solid #E2E8F0; padding-top: 16px; }");
            sb.AppendLine("@media print { body { background: white; padding: 0; } .container { box-shadow: none; border-radius: 0; } }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine("<div class=\"container\">");
            sb.AppendLine("  <div class=\"header\">");
            sb.AppendLine("    <h1>🛒 متاجر دكاني DUKKANI POS</h1>");
            sb.AppendLine("    <p>مدينة البيضاء - اليمن | هاتف الخدمة: 770000000</p>");
            sb.AppendLine("    <h2 style=\"margin-top:12px; color:#1E293B;\">كشف حساب عميل رسمي تفصيلي</h2>");
            sb.AppendLine("  </div>");

            sb.AppendLine("  <div class=\"info-grid\">");
            sb.AppendLine($"    <div class=\"info-item\"><strong>اسم العميل:</strong> {customer.CustomerName}</div>");
            sb.AppendLine($"    <div class=\"info-item\"><strong>رقم الهاتف:</strong> {customer.Phone}</div>");
            sb.AppendLine($"    <div class=\"info-item\"><strong>الدين الحالي:</strong> <span style=\"color:#DC2626; font-weight:bold;\">{customer.TotalDebt:N0} YER</span></div>");
            sb.AppendLine($"    <div class=\"info-item\"><strong>السقف الائتماني:</strong> {customer.CreditLimit:N0} YER</div>");
            sb.AppendLine("  </div>");

            sb.AppendLine("  <h3>سجل المبيعات والمنتجات المشتراة والسدادات:</h3>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead>");
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <th># الرقم</th>");
            sb.AppendLine("        <th>نوع المعاملة</th>");
            sb.AppendLine("        <th>المبلغ الإجمالي (YER)</th>");
            sb.AppendLine("        <th>التاريخ والوقت</th>");
            sb.AppendLine("        <th>تفاصيل المنتجات والبيان</th>");
            sb.AppendLine("      </tr>");
            sb.AppendLine("    </thead>");
            sb.AppendLine("    <tbody>");

            foreach (var tx in transactions)
            {
                string typeStr = tx.Type == "DEBT" ? "<span class=\"type-debt\">➕ شراء آجل</span>" : "<span class=\"type-repay\">💵 تسديد مبلغ</span>";
                
                string notesFormatted = tx.Notes ?? "";
                if (notesFormatted.StartsWith("شراء آجل: "))
                {
                    string itemsPart = notesFormatted.Substring("شراء آجل: ".Length);
                    var items = itemsPart.Split('|');
                    var itemHtml = new StringBuilder("<ul class=\"item-list\">");
                    foreach (var it in items)
                    {
                        itemHtml.AppendLine($"<li>{it.Trim()}</li>");
                    }
                    itemHtml.AppendLine("</ul>");
                    notesFormatted = itemHtml.ToString();
                }

                sb.AppendLine("      <tr>");
                sb.AppendLine($"        <td>#{tx.TransactionID}</td>");
                sb.AppendLine($"        <td>{typeStr}</td>");
                sb.AppendLine($"        <td>{tx.Amount:N0} YER</td>");
                sb.AppendLine($"        <td>{tx.TransactionDate:yyyy-MM-dd HH:mm:ss}</td>");
                sb.AppendLine($"        <td>{notesFormatted}</td>");
                sb.AppendLine("      </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");

            sb.AppendLine("  <div class=\"footer\">");
            sb.AppendLine($"    <p>تم إصدار كشف الحساب التفصيلي بتاريخ {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

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
