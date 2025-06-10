// Path: QuickTechSystems.Application.DTOs/InventoryTransferDTO.cs
using System;

namespace QuickTechSystems.Application.DTOs
{
    public class InventoryTransferDTO : BaseDTO
    {
        public int InventoryTransferId { get; set; }
        public int MainStockId { get; set; }
        public string MainStockName { get; set; } = string.Empty;
        public string MainStockBarcode { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductBarcode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public DateTime TransferDate { get; set; }
        public string? Notes { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string TransferredBy { get; set; } = string.Empty;
    }
}