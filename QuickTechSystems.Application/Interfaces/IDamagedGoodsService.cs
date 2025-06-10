// Path: QuickTechSystems.Application.Services.Interfaces/IDamagedGoodsService.cs
using QuickTechSystems.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IDamagedGoodsService
    {
        Task<IEnumerable<DamagedGoodsDTO>> GetAllAsync();
        Task<DamagedGoodsDTO> GetByIdAsync(int id);
        Task<DamagedGoodsDTO> CreateAsync(DamagedGoodsDTO dto);
        Task UpdateAsync(DamagedGoodsDTO dto);
        Task DeleteAsync(int id);
        Task<IEnumerable<DamagedGoodsDTO>> GetByProductIdAsync(int productId);
        Task<IEnumerable<DamagedGoodsDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalLossAmountAsync();
        Task<decimal> GetTotalLossAmountAsync(DateTime startDate, DateTime endDate);
        Task<bool> RegisterDamagedGoodsAsync(DamagedGoodsDTO damagedGoodsDTO);
    }
}