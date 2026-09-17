using System;

namespace DukkaniPOS.Models
{
    public class Purchase
    {
        public long PurchaseID { get; set; }
        public string ProductID { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int QuantityAdded { get; set; }
        public decimal UnitCost { get; set; }
        public DateTime PurchaseDate { get; set; }
    }
}
