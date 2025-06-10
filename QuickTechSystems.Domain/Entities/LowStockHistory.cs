// Path: QuickTechSystems.Domain.Entities/LowStockHistory.cs
using System;

namespace QuickTechSystems.Domain.Entities
{
    public class LowStockHistory
    {
        public int LowStockHistoryId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinimumStock { get; set; }
        public DateTime AlertDate { get; set; }
        public string CashierId { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public bool IsResolved { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string ResolvedBy { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        // Navigation properties
        public virtual Product? Product { get; set; }
    }
}