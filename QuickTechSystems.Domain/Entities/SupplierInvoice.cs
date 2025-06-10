// Path: QuickTechSystems.Domain.Entities/SupplierInvoice.cs
namespace QuickTechSystems.Domain.Entities
{
    public class SupplierInvoice
    {
        public int SupplierInvoiceId { get; set; }
        public int SupplierId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CalculatedAmount { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Validated, Settled
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Supplier? Supplier { get; set; }
        public virtual ICollection<SupplierInvoiceDetail> Details { get; set; } = new List<SupplierInvoiceDetail>();
    }
}