// Path: QuickTechSystems.Application.Services/DamagedGoodsService.cs
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
    public class DamagedGoodsService : BaseService<DamagedGoods, DamagedGoodsDTO>, IDamagedGoodsService
    {
        private readonly IProductService _productService;

        public DamagedGoodsService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService,
            IProductService productService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        }

        public async Task<IEnumerable<DamagedGoodsDTO>> GetByProductIdAsync(int productId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var damagedGoods = await _repository.Query()
                    .Where(d => d.ProductId == productId)
                    .OrderByDescending(d => d.DateRegistered)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<DamagedGoodsDTO>>(damagedGoods);
            });
        }

        public async Task<IEnumerable<DamagedGoodsDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                // Adjust endDate to include the entire day
                endDate = endDate.Date.AddDays(1).AddSeconds(-1);

                var damagedGoods = await _repository.Query()
                    .Where(d => d.DateRegistered >= startDate && d.DateRegistered <= endDate)
                    .OrderByDescending(d => d.DateRegistered)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<DamagedGoodsDTO>>(damagedGoods);
            });
        }

        public async Task<decimal> GetTotalLossAmountAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var totalLoss = await _repository.Query()
                    .SumAsync(d => d.LossAmount);
                return totalLoss;
            });
        }

        public async Task<decimal> GetTotalLossAmountAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                // Adjust endDate to include the entire day
                endDate = endDate.Date.AddDays(1).AddSeconds(-1);

                var totalLoss = await _repository.Query()
                    .Where(d => d.DateRegistered >= startDate && d.DateRegistered <= endDate)
                    .SumAsync(d => d.LossAmount);
                return totalLoss;
            });
        }

        public async Task<bool> RegisterDamagedGoodsAsync(DamagedGoodsDTO damagedGoodsDTO)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Validate that product exists and has enough stock
                    var product = await _productService.GetByIdAsync(damagedGoodsDTO.ProductId);
                    if (product == null)
                    {
                        Debug.WriteLine($"Product {damagedGoodsDTO.ProductId} not found for damaged goods registration");
                        return false;
                    }

                    if (product.CurrentStock < damagedGoodsDTO.Quantity)
                    {
                        Debug.WriteLine($"Insufficient stock for product {damagedGoodsDTO.ProductId} to register damaged goods");
                        return false;
                    }

                    // Create the damaged goods record
                    var entity = _mapper.Map<DamagedGoods>(damagedGoodsDTO);
                    entity.DateRegistered = DateTime.Now;
                    entity.CreatedAt = DateTime.Now;

                    var result = await _repository.AddAsync(entity);
                    await _unitOfWork.SaveChangesAsync();

                    var resultDto = _mapper.Map<DamagedGoodsDTO>(result);
                    _eventAggregator.Publish(new EntityChangedEvent<DamagedGoodsDTO>("Create", resultDto));

                    Debug.WriteLine($"Damaged goods record created for product {damagedGoodsDTO.ProductId}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error registering damaged goods: {ex.Message}");
                    throw;
                }
            });
        }

        public override async Task<IEnumerable<DamagedGoodsDTO>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var damagedGoods = await _repository.Query()
                    .OrderByDescending(d => d.DateRegistered)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<DamagedGoodsDTO>>(damagedGoods);
            });
        }

        public override async Task<DamagedGoodsDTO> CreateAsync(DamagedGoodsDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = _mapper.Map<DamagedGoods>(dto);
                entity.CreatedAt = DateTime.Now;

                var result = await _repository.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var resultDto = _mapper.Map<DamagedGoodsDTO>(result);
                _eventAggregator.Publish(new EntityChangedEvent<DamagedGoodsDTO>("Create", resultDto));

                return resultDto;
            });
        }

        public override async Task UpdateAsync(DamagedGoodsDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var existingEntity = await _repository.GetByIdAsync(dto.DamagedGoodsId);
                if (existingEntity == null)
                {
                    throw new InvalidOperationException($"Damaged goods with ID {dto.DamagedGoodsId} not found");
                }

                _mapper.Map(dto, existingEntity);
                existingEntity.UpdatedAt = DateTime.Now;

                await _repository.UpdateAsync(existingEntity);
                await _unitOfWork.SaveChangesAsync();

                _eventAggregator.Publish(new EntityChangedEvent<DamagedGoodsDTO>("Update", dto));
            });
        }

        public override async Task DeleteAsync(int id)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity == null)
                {
                    throw new InvalidOperationException($"Damaged goods with ID {id} not found");
                }

                // Pass the entity object instead of just the ID
                await _repository.DeleteAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                var dto = _mapper.Map<DamagedGoodsDTO>(entity);
                _eventAggregator.Publish(new EntityChangedEvent<DamagedGoodsDTO>("Delete", dto));
            });
        }
    }
}