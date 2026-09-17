namespace DukkaniPOS.Models
{
    public class CartItem
    {
        public Product Product { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal TotalAmount => Product.SellingPrice * Quantity;
        public decimal TotalCost => Product.CostPrice * Quantity;
        public decimal TotalProfit => (Product.SellingPrice - Product.CostPrice) * Quantity;
    }
}
