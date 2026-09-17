using System;
using System.Collections.Generic;

namespace DukkaniPOS.Models
{
    public class DailyReport
    {
        public DateTime Date { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCapitalCost { get; set; }
        public decimal NetProfit => TotalRevenue - TotalCapitalCost;
        public int TotalItemsSold { get; set; }
        public int TotalTransactions { get; set; }
        public List<Sale> SalesDetails { get; set; } = new List<Sale>();
        public List<Product> LowStockProducts { get; set; } = new List<Product>();
    }
}
