using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface ICategoryService : IBaseService<CategoryDTO>
    {
        Task<CategoryDTO?> GetByNameAsync(string name);
        Task<IEnumerable<CategoryDTO>> GetActiveAsync();
        Task<IEnumerable<CategoryDTO>> GetByTypeAsync(string type);
        Task<IEnumerable<CategoryDTO>> GetProductCategoriesAsync();
        Task<IEnumerable<CategoryDTO>> GetExpenseCategoriesAsync();
        Task<bool> IsNameUniqueWithinTypeAsync(string name, string type, int? excludeId = null);
    }
}