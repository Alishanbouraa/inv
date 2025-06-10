// QuickTechSystems.Application/Services/BusinessSettingsService.cs
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
    public class BusinessSettingsService : BaseService<BusinessSetting, BusinessSettingDTO>, IBusinessSettingsService
    {
        public BusinessSettingsService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
        }

        public async Task<BusinessSettingDTO?> GetByKeyAsync(string key)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var setting = await _repository.Query()
                    .FirstOrDefaultAsync(s => s.Key == key);
                return _mapper.Map<BusinessSettingDTO>(setting);
            });
        }

        public async Task<IEnumerable<BusinessSettingDTO>> GetByGroupAsync(string group)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var settings = await _repository.Query()
                    .Where(s => s.Group == group)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<BusinessSettingDTO>>(settings);
            });
        }

        public async Task<string> GetSettingValueAsync(string key, string defaultValue = "")
        {
            var setting = await GetByKeyAsync(key);
            return setting?.Value ?? defaultValue;
        }

        public async Task UpdateSettingAsync(string key, string value, string modifiedBy)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var setting = await _repository.Query()
                    .FirstOrDefaultAsync(s => s.Key == key);

                if (setting == null)
                {
                    setting = new BusinessSetting
                    {
                        Key = key,
                        Value = value,
                        ModifiedBy = modifiedBy,
                        LastModified = DateTime.Now
                    };
                    await _repository.AddAsync(setting);
                }
                else
                {
                    setting.Value = value;
                    setting.ModifiedBy = modifiedBy;
                    setting.LastModified = DateTime.Now;
                    await _repository.UpdateAsync(setting);
                }

                await _unitOfWork.SaveChangesAsync();
            });
        }

        // Add the CompanyLogo setting to the InitializeDefaultSettingsAsync method in BusinessSettingsService.cs
        public async Task InitializeDefaultSettingsAsync()
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var defaultSettings = new List<BusinessSetting>
        {
            new() {
                Key = "CompanyName",
                Value = "QuickTech Systems",
                Group = "General",
                Description = "Company name displayed on receipts and reports",
                IsSystem = true
            },
            new() {
                Key = "Address",
                Value = "",
                Group = "General",
                Description = "Company address",
                IsSystem = true
            },
            new() {
                Key = "Phone",
                Value = "",
                Group = "General",
                Description = "Company phone number",
                IsSystem = true
            },
            new() {
                Key = "Email",
                Value = "",
                Group = "General",
                Description = "Company email address",
                IsSystem = true
            },
            new() {
                Key = "CompanyLogo",
                Value = "", // Will store Base64 encoded image
                Group = "General",
                Description = "Company logo displayed on receipts",
                DataType = "image",
                IsSystem = true
            },
            new() {
                Key = "Currency",
                Value = "USD",
                Group = "Financial",
                Description = "Default currency",
                IsSystem = true
            },
            new() {
                Key = "TaxRate",
                Value = "0",
                Group = "Financial",
                Description = "Default tax rate percentage",
                DataType = "decimal",
                IsSystem = true
            },
            new() {
                Key = "LowStockThreshold",
                Value = "10",
                Group = "Inventory",
                Description = "Low stock alert threshold",
                DataType = "int",
                IsSystem = true
            },
            new() {
                Key = "EnableEmailReceipts",
                Value = "false",
                Group = "Sales",
                Description = "Enable email receipts",
                DataType = "boolean",
                IsSystem = true
            },
            new() {
                Key = "ExchangeRate",
                Value = "90000",  // Default exchange rate
                Group = "Financial",
                Description = "Exchange rate (LBP per USD)",
                DataType = "decimal",
                IsSystem = true
            },
            new() {
                Key = "ReceiptFooter1",
                Value = "Stay caffeinated!!",  // First footer line
                Group = "Sales",
                Description = "Primary message at bottom of receipt",
                IsSystem = true
            },
            new() {
                Key = "ReceiptFooter2",
                Value = "See you next time",  // Second footer line
                Group = "Sales",
                Description = "Secondary message at bottom of receipt",
                IsSystem = true
            }
        };

                foreach (var setting in defaultSettings)
                {
                    var existing = await GetByKeyAsync(setting.Key);
                    if (existing == null)
                    {
                        setting.LastModified = DateTime.Now;
                        setting.ModifiedBy = "System";
                        await _repository.AddAsync(setting);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
            });
        }
    }
}