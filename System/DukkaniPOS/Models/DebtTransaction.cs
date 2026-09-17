using System;

namespace DukkaniPOS.Models
{
    public class DebtTransaction
    {
        public long TransactionID { get; set; }
        public int CustomerID { get; set; }
        public string Type { get; set; } = "DEBT"; // "DEBT" or "REPAYMENT"
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
