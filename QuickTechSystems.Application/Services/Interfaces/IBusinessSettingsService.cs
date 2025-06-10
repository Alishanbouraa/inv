// Path: QuickTechSystems.Application/Services/Interfaces/IBusinessSettingsService.cs
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IBusinessSettingsService : IBaseService<BusinessSettingDTO>
    {
        Task<BusinessSettingDTO?> GetByKeyAsync(string key);
        Task<IEnumerable<BusinessSettingDTO>> GetByGroupAsync(string group);
        Task<string> GetSettingValueAsync(string key, string defaultValue = "");
        Task UpdateSettingAsync(string key, string value, string modifiedBy);
        Task InitializeDefaultSettingsAsync();
    }
}