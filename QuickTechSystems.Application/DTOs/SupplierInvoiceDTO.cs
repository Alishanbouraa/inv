// Path: QuickTechSystems.Application.DTOs/SupplierInvoiceDTO.cs
using System.Collections.ObjectModel;

namespace QuickTechSystems.Application.DTOs
{
    public class SupplierInvoiceDTO
    {
        public int SupplierInvoiceId { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CalculatedAmount { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Validated, Settled
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ObservableCollection<SupplierInvoiceDetailDTO> Details { get; set; } = new ObservableCollection<SupplierInvoiceDetailDTO>();
        public decimal Difference => TotalAmount - CalculatedAmount;
        public bool HasDiscrepancy => Math.Abs(Difference) > 0.01m;
    }
}