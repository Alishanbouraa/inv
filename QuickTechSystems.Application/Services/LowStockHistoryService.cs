// Path: QuickTechSystems.Application.Services/LowStockHistoryService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;

namespace QuickTechSystems.Application.Services
{
    public class LowStockHistoryService : BaseService<LowStockHistory, LowStockHistoryDTO>, ILowStockHistoryService
    {
        public LowStockHistoryService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
        }

        public async Task<IEnumerable<LowStockHistoryDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Normalize dates to start and end of day
                    var normalizedStartDate = startDate.Date;
                    var normalizedEndDate = endDate.Date.AddDays(1).AddTicks(-1);

                    // Get history data with AsNoTracking for better performance and to avoid DbContext tracking issues
                    var history = await _repository.Query()
                        .AsNoTracking()
                        .Include(h => h.Product)
                        .Where(h => h.AlertDate >= normalizedStartDate && h.AlertDate <= normalizedEndDate)
                        .OrderByDescending(h => h.AlertDate)
                        .ToListAsync();

                    return _mapper.Map<IEnumerable<LowStockHistoryDTO>>(history);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetByDateRangeAsync: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<LowStockHistoryDTO> LogLowStockAlertAsync(
            int productId,
            string productName,
            int currentStock,
            int minimumStock,
            string cashierId,
            string cashierName)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Create a new low stock history entry
                    var lowStockHistory = new LowStockHistory
                    {
                        ProductId = productId,
                        ProductName = productName,
                        CurrentStock = currentStock,
                        MinimumStock = minimumStock,
                        AlertDate = DateTime.Now,
                        CashierId = cashierId,
                        CashierName = cashierName,
                        IsResolved = false
                    };

                    // Add to repository in a fresh DbContext scope to avoid concurrency issues
                    using (var scope = _unitOfWork.Context.Database.BeginTransaction())
                    {
                        try
                        {
                            var result = await _repository.AddAsync(lowStockHistory);
                            await _unitOfWork.SaveChangesAsync();
                            await scope.CommitAsync();

                            // Map result to DTO and publish event
                            var resultDto = _mapper.Map<LowStockHistoryDTO>(result);
                            _eventAggregator.Publish(new EntityChangedEvent<LowStockHistoryDTO>("Create", resultDto));

                            Debug.WriteLine($"Low stock alert logged for product {productName}");
                            return resultDto;
                        }
                        catch
                        {
                            await scope.RollbackAsync();
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error logging low stock alert: {ex.Message}");
                    throw;
                }
            });
        }

        public override async Task<LowStockHistoryDTO> UpdateAsync(LowStockHistoryDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using (var transaction = await _unitOfWork.BeginTransactionAsync())
                {
                    try
                    {
                        var entity = await _repository.GetByIdAsync(dto.LowStockHistoryId);
                        if (entity == null)
                        {
                            throw new InvalidOperationException($"Low Stock History with ID {dto.LowStockHistoryId} not found");
                        }

                        // Update the entity properties
                        entity.IsResolved = dto.IsResolved;
                        entity.ResolvedDate = dto.ResolvedDate;
                        entity.ResolvedBy = dto.ResolvedBy;
                        entity.Notes = dto.Notes;

                        await _repository.UpdateAsync(entity);
                        await _unitOfWork.SaveChangesAsync();
                        await transaction.CommitAsync();

                        var updatedDto = _mapper.Map<LowStockHistoryDTO>(entity);
                        _eventAggregator.Publish(new EntityChangedEvent<LowStockHistoryDTO>("Update", updatedDto));

                        return updatedDto;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error updating low stock history: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            });
        }
    }
}