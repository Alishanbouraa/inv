// Path: QuickTechSystems.Application.Services.Interfaces/ILowStockHistoryService.cs
using QuickTechSystems.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface ILowStockHistoryService : IBaseService<LowStockHistoryDTO>
    {
        Task<IEnumerable<LowStockHistoryDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<LowStockHistoryDTO> LogLowStockAlertAsync(int productId, string productName, int currentStock, int minimumStock, string cashierId, string cashierName);
    }
}