// Path: QuickTechSystems.Application.DTOs/InventoryHistoryDTO.cs
namespace QuickTechSystems.Application.DTOs
{
    public class InventoryHistoryDTO
    {
        public int InventoryHistoryId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal QuantityChange { get; set; }
        public decimal NewQuantity { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime Timestamp { get; set; }
    }
}