// Path: QuickTechSystems.Application.Services.Interfaces/IProductService.cs
using QuickTechSystems.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IProductService : IBaseService<ProductDTO>
    {
        Task<IEnumerable<ProductDTO>> GetByCategoryAsync(int categoryId);
        Task<bool> UpdateStockAsync(int productId, decimal quantity);
        Task<IEnumerable<ProductDTO>> GetLowStockProductsAsync();
        Task<ProductDTO?> GetByBarcodeAsync(string barcode);

        // Added back for compatibility with existing code
        Task<List<ProductDTO>> CreateBatchAsync(List<ProductDTO> products, IProgress<string>? progress = null);

        Task<ProductDTO> FindProductByBarcodeAsync(string barcode, int excludeProductId = 0);

        // Method to receive inventory from MainStock
        Task<bool> ReceiveInventoryAsync(int productId, decimal quantity, string source, string reference);

       
    }
}