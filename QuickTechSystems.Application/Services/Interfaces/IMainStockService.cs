// Path: QuickTechSystems.Application.Services.Interfaces/IMainStockService.cs
using QuickTechSystems.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IMainStockService : IBaseService<MainStockDTO>
    {
        Task<IEnumerable<MainStockDTO>> GetByCategoryAsync(int categoryId);
        Task<bool> UpdateStockAsync(int mainStockId, decimal quantity);
        Task<IEnumerable<MainStockDTO>> GetLowStockProductsAsync();
        Task<MainStockDTO?> GetByBarcodeAsync(string barcode);
        Task<MainStockDTO?> GetByBoxBarcodeAsync(string boxBarcode);
        Task<MainStockDTO> UpdateAsync(MainStockDTO dto);
        // in QuickTechSystems.Application.Services.Interfaces/IMainStockService.cs

        Task<MainStockDTO> FindProductByBarcodeAsync(string barcode, int excludeMainStockId = 0);
        Task<List<MainStockDTO>> CreateBatchAsync(List<MainStockDTO> products, IProgress<string>? progress = null);

        Task<bool> TransferToStoreAsync(int mainStockId, int productId, decimal quantity, string transferredBy, string notes, bool isByBoxes = false);
        Task<IEnumerable<MainStockDTO>> SearchAsync(string searchTerm);
    }
}