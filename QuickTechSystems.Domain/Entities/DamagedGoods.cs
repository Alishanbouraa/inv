// Path: QuickTechSystems.Domain.Entities/DamagedGoods.cs
using System;

namespace QuickTechSystems.Domain.Entities
{
    public class DamagedGoods
    {
        public int DamagedGoodsId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime DateRegistered { get; set; }
        public decimal LossAmount { get; set; }
        public string CategoryName { get; set; } = string.Empty;

        // Standard properties that might match your other entities
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }

        // Navigation property
        public virtual Product Product { get; set; }
    }
}