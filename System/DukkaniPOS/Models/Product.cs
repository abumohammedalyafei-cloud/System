namespace DukkaniPOS.Models
{
    public class Product
    {
        public string ProductID { get; set; } = string.Empty; // Barcode / SKU
        public string ProductName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public int StockQuantity { get; set; }
        public int MinStockWarning { get; set; }

        public bool IsLowStock => StockQuantity <= MinStockWarning;

        public string StockStatusText
        {
            get
            {
                if (StockQuantity <= 0) return "🔴 نفد بالكامل";
                if (StockQuantity <= MinStockWarning) return "🟡 قريب من النفاد";
                return "🟢 متوفر بكثرة";
            }
        }

        public string StockStatusColor
        {
            get
            {
                if (StockQuantity <= 0) return "#EF4444";
                if (StockQuantity <= MinStockWarning) return "#D97706";
                return "#10B981";
            }
        }
    }
}
