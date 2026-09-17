namespace DukkaniPOS.Models
{
    public class Customer
    {
        public int CustomerID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public decimal TotalDebt { get; set; }
        public decimal CreditLimit { get; set; }

        public decimal RemainingCredit => CreditLimit - TotalDebt;
    }
}
