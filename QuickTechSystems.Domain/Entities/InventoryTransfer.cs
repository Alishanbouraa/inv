// Path: QuickTechSystems.Domain.Entities/InventoryTransfer.cs
using System;

namespace QuickTechSystems.Domain.Entities
{
    public class InventoryTransfer
    {
        public int InventoryTransferId { get; set; }
        public int MainStockId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public DateTime TransferDate { get; set; }
        public string? Notes { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string TransferredBy { get; set; } = string.Empty;

        // Navigation properties
        public virtual MainStock MainStock { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }
}