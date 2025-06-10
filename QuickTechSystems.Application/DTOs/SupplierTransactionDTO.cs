namespace QuickTechSystems.Application.DTOs
{
    public class SupplierTransactionDTO
    {
        public int SupplierTransactionId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string? Notes { get; set; }
        public DateTime TransactionDate { get; set; }
    }
}