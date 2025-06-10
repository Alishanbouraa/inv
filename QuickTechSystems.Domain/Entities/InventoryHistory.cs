// Path: QuickTechSystems.Domain.Entities/InventoryHistory.cs
namespace QuickTechSystems.Domain.Entities
{
    public class InventoryHistory
    {
        public int InventoryHistoryId { get; set; }
        public int ProductId { get; set; }
        public decimal QuantityChange { get; set; }
        public decimal NewQuantity { get; set; }
        public string Type { get; set; } = string.Empty; // Purchase, Sale, Adjustment, etc.
        public string? Notes { get; set; }
        public DateTime Timestamp { get; set; }

        // Navigation properties
        public virtual Product? Product { get; set; }
    }
}