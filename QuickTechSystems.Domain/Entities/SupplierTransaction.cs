namespace QuickTechSystems.Domain.Entities
{
    public class SupplierTransaction
    {
        public int SupplierTransactionId { get; set; }
        public int SupplierId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty; // Purchase/Payment
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public DateTime TransactionDate { get; set; }

        // Navigation property
        public virtual Supplier? Supplier { get; set; }
    }
}