using System;

namespace DukkaniPOS.Models
{
    public class Sale
    {
        public long SaleID { get; set; }
        public string ProductID { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal CostPrice { get; set; }
        public DateTime SaleDate { get; set; }
        public int? CustomerID { get; set; }

        public decimal TotalRevenue => SellingPrice * QuantitySold;
        public decimal TotalCost => CostPrice * QuantitySold;
        public decimal Profit => TotalRevenue - TotalCost;
    }
}
