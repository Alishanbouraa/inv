// Path: QuickTechSystems.Application.DTOs/SupplierInvoiceDetailDTO.cs
namespace QuickTechSystems.Application.DTOs
{
    public class SupplierInvoiceDetailDTO
    {
        public int SupplierInvoiceDetailId { get; set; }
        public int SupplierInvoiceId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductBarcode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal TotalPrice { get; set; }

        // Box-related properties
        public string BoxBarcode { get; set; } = string.Empty;
        public int NumberOfBoxes { get; set; }
        public int ItemsPerBox { get; set; } = 1;
        public decimal BoxPurchasePrice { get; set; }
        public decimal BoxSalePrice { get; set; }
    }
}