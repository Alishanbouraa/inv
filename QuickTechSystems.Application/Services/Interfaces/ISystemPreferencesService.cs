// Path: QuickTechSystems.Application/Services/Interfaces/ISystemPreferencesService.cs
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface ISystemPreferencesService
    {
        Task<IEnumerable<SystemPreferenceDTO>> GetUserPreferencesAsync(string userId);
        Task<string> GetPreferenceValueAsync(string userId, string key, string defaultValue = "");
        Task SavePreferenceAsync(string userId, string key, string value);
        Task SavePreferencesAsync(string userId, Dictionary<string, string> preferences);
        Task InitializeUserPreferencesAsync(string userId);
    }
}