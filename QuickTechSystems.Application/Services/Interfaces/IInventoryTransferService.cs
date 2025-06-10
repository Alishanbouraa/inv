// Path: QuickTechSystems.Application.Services.Interfaces/IInventoryTransferService.cs
using QuickTechSystems.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IInventoryTransferService
    {
        Task<IEnumerable<InventoryTransferDTO>> GetAllAsync();
        Task<IEnumerable<InventoryTransferDTO>> GetByMainStockIdAsync(int mainStockId);
        Task<IEnumerable<InventoryTransferDTO>> GetByProductIdAsync(int productId);
        Task<IEnumerable<InventoryTransferDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<InventoryTransferDTO> GetByIdAsync(int id);
        Task<InventoryTransferDTO> CreateAsync(InventoryTransferDTO transferDto);
        Task<bool> CreateBulkTransferAsync(List<(int MainStockId, int ProductId, decimal Quantity)> transfers, string transferredBy, string? notes = null);
    }
}