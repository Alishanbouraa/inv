using System.Diagnostics;
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
    public class CategoryService : BaseService<Category, CategoryDTO>, ICategoryService
    {
        public CategoryService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
        }

        public async Task<CategoryDTO?> GetByNameAsync(string name)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var category = await _repository.Query()
                    .Where(c => c.Name.Contains(name))
                    .FirstOrDefaultAsync();
                return _mapper.Map<CategoryDTO>(category);
            });
        }

        public async Task<IEnumerable<CategoryDTO>> GetByTypeAsync(string type)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var categories = await _repository.Query()
                    .Where(c => c.Type == type)
                    .Include(c => c.Products)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<CategoryDTO>>(categories);
            });
        }

        public async Task<IEnumerable<CategoryDTO>> GetActiveAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var categories = await _repository.Query()
                        .Where(c => c.IsActive)
                        .Include(c => c.Products)
                        .ToListAsync();
                    return _mapper.Map<IEnumerable<CategoryDTO>>(categories);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetActiveAsync: {ex.Message}");
                    throw;
                }
            });
        }

        public override async Task<CategoryDTO> CreateAsync(CategoryDTO dto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Validate unique name within type
                    var existingCategory = await _repository.Query()
                        .FirstOrDefaultAsync(c => c.Name == dto.Name && c.Type == dto.Type);

                    if (existingCategory != null)
                    {
                        throw new InvalidOperationException($"A {dto.Type} category with the name '{dto.Name}' already exists.");
                    }

                    var entity = _mapper.Map<Category>(dto);
                    var result = await _repository.AddAsync(entity);
                    await _unitOfWork.SaveChangesAsync();

                    var resultDto = _mapper.Map<CategoryDTO>(result);
                    _eventAggregator.Publish(new EntityChangedEvent<CategoryDTO>("Create", resultDto));

                    return resultDto;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error creating category: {ex.Message}");
                    throw;
                }
            });
        }

        public override async Task UpdateAsync(CategoryDTO dto)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Check if another category of the same type exists with the same name
                    var existingCategory = await _repository.Query()
                        .FirstOrDefaultAsync(c =>
                            c.Name == dto.Name &&
                            c.Type == dto.Type &&
                            c.CategoryId != dto.CategoryId);

                    if (existingCategory != null)
                    {
                        throw new InvalidOperationException($"Another {dto.Type} category with the name '{dto.Name}' already exists.");
                    }

                    var category = await _repository.GetByIdAsync(dto.CategoryId);
                    if (category == null)
                    {
                        throw new InvalidOperationException($"Category with ID {dto.CategoryId} not found");
                    }

                    _mapper.Map(dto, category);

                    // Explicitly set IsActive property to ensure it's tracked
                    category.IsActive = dto.IsActive;

                    // Note: Category entity doesn't have UpdatedAt property

                    await _repository.UpdateAsync(category);
                    await _unitOfWork.SaveChangesAsync();

                    _eventAggregator.Publish(new EntityChangedEvent<CategoryDTO>("Update", dto));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating category: {ex}");
                    throw;
                }
            });
        }

        public override async Task DeleteAsync(int id)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var category = await _unitOfWork.Context.Set<Category>()
                        .Include(c => c.Products)
                        .FirstOrDefaultAsync(c => c.CategoryId == id);

                    if (category == null)
                    {
                        throw new InvalidOperationException($"Category with ID {id} not found");
                    }

                    // Check if this is a product category with associated products
                    if (category.Type == "Product" && category.Products.Any())
                    {
                        throw new InvalidOperationException(
                            "Cannot delete category because it has associated products. " +
                            "Please reassign or delete the products first.");
                    }

                    // For expense categories, or product categories without products
                    await _repository.DeleteAsync(category);
                    await _unitOfWork.SaveChangesAsync();

                    var categoryDto = _mapper.Map<CategoryDTO>(category);
                    _eventAggregator.Publish(new EntityChangedEvent<CategoryDTO>("Delete", categoryDto));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting category: {ex}");
                    throw;
                }
            });
        }

        public async Task<IEnumerable<CategoryDTO>> GetProductCategoriesAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Get categories without transaction scope to avoid nested transactions
                    var categories = await _repository.Query()
                        .Where(c => c.Type == "Product")
                        .AsNoTracking() // This prevents entity tracking issues
                        .ToListAsync();

                    return _mapper.Map<IEnumerable<CategoryDTO>>(categories);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetProductCategoriesAsync: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<IEnumerable<CategoryDTO>> GetExpenseCategoriesAsync()
        {
            return await GetByTypeAsync("Expense");
        }

        public async Task<bool> IsNameUniqueWithinTypeAsync(string name, string type, int? excludeId = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var query = _repository.Query()
                    .Where(c => c.Name == name && c.Type == type);

                if (excludeId.HasValue)
                {
                    query = query.Where(c => c.CategoryId != excludeId.Value);
                }

                return !await query.AnyAsync();
            });
        }
    }
}