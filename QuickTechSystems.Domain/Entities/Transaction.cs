using QuickTechSystems.Domain.Enums;

namespace QuickTechSystems.Domain.Entities
{
    public class Transaction
    {
        public int TransactionId { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public DateTime TransactionDate { get; set; }
        public TransactionType TransactionType { get; set; }
        public TransactionStatus Status { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string CashierId { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual ICollection<TransactionDetail> TransactionDetails { get; set; } = new List<TransactionDetail>();
        public string CashierRole { get; set; } = string.Empty;
    }
}