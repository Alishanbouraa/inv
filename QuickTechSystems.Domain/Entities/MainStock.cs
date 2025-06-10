// Path: QuickTechSystems.Domain.Entities/MainStock.cs
using System;
using System.Collections.Generic;

namespace QuickTechSystems.Domain.Entities
{
    public class MainStock
    {
        public int MainStockId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal CurrentStock { get; set; }
        public int MinimumStock { get; set; }
        public int? SupplierId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string? Speed { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? ImagePath { get; set; }
        public byte[]? BarcodeImage { get; set; }

        // Box-related properties
        public string BoxBarcode { get; set; } = string.Empty;
        public int NumberOfBoxes { get; set; }
        public int ItemsPerBox { get; set; } = 0;
        public decimal BoxPurchasePrice { get; set; }
        public decimal BoxSalePrice { get; set; }
        public int MinimumBoxStock { get; set; }

        // Path: QuickTechSystems.Domain.Entities/MainStock.cs

        // Add these properties
        public decimal WholesalePrice { get; set; }
        public decimal BoxWholesalePrice { get; set; }
        // Navigation properties
        public virtual Category? Category { get; set; }
        public virtual Supplier? Supplier { get; set; }

        // Products that reference this MainStock item
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}