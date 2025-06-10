// Path: QuickTechSystems.Application/Services/Interfaces/IRestaurantTableService.cs
using QuickTechSystems.Application.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IRestaurantTableService : IBaseService<RestaurantTableDTO>
    {
        Task<IEnumerable<RestaurantTableDTO>> GetActiveTablesAsync();
        Task<bool> IsTableNumberUniqueAsync(int tableNumber, int? excludeId = null);
        Task UpdateTableStatusAsync(int tableId, string status);
    }
}