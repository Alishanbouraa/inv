// Path: QuickTechSystems.Application/Services/RestaurantTableService.cs
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services
{
    public class RestaurantTableService : BaseService<RestaurantTable, RestaurantTableDTO>, IRestaurantTableService
    {
        public RestaurantTableService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
        }

        public async Task<IEnumerable<RestaurantTableDTO>> GetActiveTablesAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var tables = await _repository.Query()
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.TableNumber)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<RestaurantTableDTO>>(tables);
            });
        }

        public async Task<bool> IsTableNumberUniqueAsync(int tableNumber, int? excludeId = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var query = _repository.Query()
                    .Where(t => t.TableNumber == tableNumber);

                if (excludeId.HasValue)
                {
                    query = query.Where(t => t.Id != excludeId.Value);
                }

                return !await query.AnyAsync();
            });
        }

        public override async Task UpdateAsync(RestaurantTableDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                // Get the entity from the database
                var entity = await _repository.GetByIdAsync(dto.Id);
                if (entity == null)
                {
                    throw new Exception($"Entity with ID {dto.Id} not found");
                }

                // Update properties manually instead of replacing the whole entity
                entity.TableNumber = dto.TableNumber;
                entity.Status = dto.Status;
                entity.Description = dto.Description;
                entity.UpdatedAt = DateTime.Now;

                await _repository.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                // Publish event
                _eventAggregator.Publish(new EntityChangedEvent<RestaurantTableDTO>("Update", dto));
            });
        }
        public async Task UpdateTableStatusAsync(int tableId, string status)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var table = await _repository.GetByIdAsync(tableId);
                if (table != null)
                {
                    table.Status = status;
                    table.UpdatedAt = DateTime.Now;
                    await _repository.UpdateAsync(table);
                    await _unitOfWork.SaveChangesAsync();

                    // Publish event for table status change
                    var dto = _mapper.Map<RestaurantTableDTO>(table);
                    _eventAggregator.Publish(new EntityChangedEvent<RestaurantTableDTO>("Update", dto));
                }
            });
        }
    }
}