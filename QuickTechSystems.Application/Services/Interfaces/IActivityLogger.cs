using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IActivityLogger
    {
        Task LogActivityAsync(string userId, string action, string details, bool isSuccess = true, string? errorMessage = null);
        Task LogEntityChangeAsync(string userId, string entityType, string entityId, string action, string? oldValue = null, string? newValue = null);
        Task<IEnumerable<ActivityLogDTO>> GetUserActivitiesAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<ActivityLogDTO>> GetModuleActivitiesAsync(string moduleName, DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<ActivityLogDTO>> GetEntityChangesAsync(string entityType, string entityId);
        Task<IEnumerable<ActivityLogDTO>> SearchActivitiesAsync(string searchTerm);
        Task<int> GetUserActivityCountAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task ClearOldLogsAsync(int daysToKeep);
    }
}