// Path: QuickTechSystems.WPF.Helpers/ProductExtensions.cs
using QuickTechSystems.Application.DTOs;
using System.Reflection;

namespace QuickTechSystems.WPF.Helpers
{
    public static class ProductExtensions
    {
        /// <summary>
        /// Refreshes calculated fields on a ProductDTO by re-reading properties
        /// </summary>
        public static void RefreshCalculatedFields(this ProductDTO product)
        {
            if (product == null) return;

            // Create a temporary copy of key values
            var currentStock = product.CurrentStock;
            var purchasePrice = product.PurchasePrice;
            var salePrice = product.SalePrice;

            // Use reflection to force the property changed mechanism
            typeof(ProductDTO).GetProperty("CurrentStock")?.SetValue(product, currentStock);
            typeof(ProductDTO).GetProperty("PurchasePrice")?.SetValue(product, purchasePrice);
            typeof(ProductDTO).GetProperty("SalePrice")?.SetValue(product, salePrice);
        }
    }
}