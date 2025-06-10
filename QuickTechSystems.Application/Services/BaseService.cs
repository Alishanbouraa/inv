// QuickTechSystems.Application/Services/BaseService.cs
using AutoMapper;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces; // Updated this import
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Interfaces.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services
{
    public abstract class BaseService<TEntity, TDto> : IBaseService<TDto>
        where TEntity : class
        where TDto : class
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected readonly IMapper _mapper;
        protected readonly IGenericRepository<TEntity> _repository;
        protected readonly IEventAggregator _eventAggregator;
        protected readonly IDbContextScopeService _dbContextScopeService;

        protected BaseService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _repository = unitOfWork.GetRepository<TEntity>();
            _eventAggregator = eventAggregator;
            _dbContextScopeService = dbContextScopeService;
        }

        public virtual async Task<TDto?> GetByIdAsync(int id)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = await _repository.GetByIdAsync(id);
                return _mapper.Map<TDto>(entity);
            });
        }

        public virtual async Task<IEnumerable<TDto>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entities = await _repository.GetAllAsync();
                return _mapper.Map<IEnumerable<TDto>>(entities);
            });
        }

        public virtual async Task<TDto> CreateAsync(TDto dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = _mapper.Map<TEntity>(dto);
                var result = await _repository.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();
                var resultDto = _mapper.Map<TDto>(result);
                _eventAggregator.Publish(new EntityChangedEvent<TDto>("Create", resultDto));
                return resultDto;
            });
        }

        public virtual async Task UpdateAsync(TDto dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = _mapper.Map<TEntity>(dto);
                await _repository.UpdateAsync(entity);
                await _unitOfWork.SaveChangesAsync();
                _eventAggregator.Publish(new EntityChangedEvent<TDto>("Update", dto));
            });
        }

        public virtual async Task DeleteAsync(int id)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var entity = await _repository.GetByIdAsync(id);
                if (entity != null)
                {
                    await _repository.DeleteAsync(entity);
                    await _unitOfWork.SaveChangesAsync();
                    var dto = _mapper.Map<TDto>(entity);
                    _eventAggregator.Publish(new EntityChangedEvent<TDto>("Delete", dto));
                }
            });
        }
    }
}