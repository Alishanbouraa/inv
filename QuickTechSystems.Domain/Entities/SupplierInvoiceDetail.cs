// Path: QuickTechSystems.Domain.Entities/SupplierInvoiceDetail.cs
namespace QuickTechSystems.Domain.Entities
{
    public class SupplierInvoiceDetail
    {
        public int SupplierInvoiceDetailId { get; set; }
        public int SupplierInvoiceId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal TotalPrice { get; set; }

        // Box-related properties
        public string BoxBarcode { get; set; } = string.Empty;
        public int NumberOfBoxes { get; set; }
        public int ItemsPerBox { get; set; } = 1;
        public decimal BoxPurchasePrice { get; set; }
        public decimal BoxSalePrice { get; set; }

        // Navigation properties
        public virtual SupplierInvoice? SupplierInvoice { get; set; }
        public virtual Product? Product { get; set; }
    }
}