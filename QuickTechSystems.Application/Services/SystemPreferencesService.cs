using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;
using System.Diagnostics;

namespace QuickTechSystems.Application.Services
{
    public class SystemPreferencesService : ISystemPreferencesService, IDisposable
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IDbContextScopeService _dbContextScopeService;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private bool _disposed;

        public SystemPreferencesService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IDbContextScopeService dbContextScopeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _dbContextScopeService = dbContextScopeService;
        }

        public async Task<IEnumerable<SystemPreferenceDTO>> GetUserPreferencesAsync(string userId)
        {
            await _semaphore.WaitAsync();
            try
            {
                return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
                {
                    var preferences = await _unitOfWork.SystemPreferences.Query()
                        .Where(p => p.UserId == userId)
                        .ToListAsync();
                    return _mapper.Map<IEnumerable<SystemPreferenceDTO>>(preferences);
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<string> GetPreferenceValueAsync(string userId, string key, string defaultValue = "")
        {
            await _semaphore.WaitAsync();
            try
            {
                return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
                {
                    var preference = await _unitOfWork.SystemPreferences.Query()
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == key);
                    return preference?.PreferenceValue ?? defaultValue;
                });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SavePreferenceAsync(string userId, string key, string value)
        {
            await _semaphore.WaitAsync();
            try
            {
                // Use a new transaction scope for this operation
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var preference = await _unitOfWork.SystemPreferences.Query()
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == key);

                    if (preference == null)
                    {
                        preference = new SystemPreference
                        {
                            UserId = userId,
                            PreferenceKey = key,
                            PreferenceValue = value,
                            LastModified = DateTime.Now
                        };
                        await _unitOfWork.SystemPreferences.AddAsync(preference);
                    }
                    else
                    {
                        preference.PreferenceValue = value;
                        preference.LastModified = DateTime.Now;
                        await _unitOfWork.SystemPreferences.UpdateAsync(preference);
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();
                    Debug.WriteLine($"Successfully saved preference: {key}={value}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving preference {key}: {ex.Message}");
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task SavePreferencesAsync(string userId, Dictionary<string, string> preferences)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    foreach (var (key, value) in preferences)
                    {
                        var preference = await _unitOfWork.SystemPreferences.Query()
                            .FirstOrDefaultAsync(p => p.UserId == userId && p.PreferenceKey == key);

                        if (preference == null)
                        {
                            preference = new SystemPreference
                            {
                                UserId = userId,
                                PreferenceKey = key,
                                PreferenceValue = value,
                                LastModified = DateTime.Now
                            };
                            await _unitOfWork.SystemPreferences.AddAsync(preference);
                        }
                        else
                        {
                            preference.PreferenceValue = value;
                            preference.LastModified = DateTime.Now;
                            await _unitOfWork.SystemPreferences.UpdateAsync(preference);
                        }
                    }
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();
                    Debug.WriteLine($"Successfully saved {preferences.Count} preferences");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving preferences batch: {ex.Message}");
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task InitializeUserPreferencesAsync(string userId)
        {
            var defaultPreferences = new Dictionary<string, string>
            {
                { "Theme", "Light" },
                { "Language", "en-US" },
                { "TimeZone", "UTC" },
                { "DateFormat", "MM/dd/yyyy" },
                { "TimeFormat", "HH:mm:ss" },
                { "ReceiptPrinter", "Default" },
                { "BarcodeScannerEnabled", "true" },
                { "ShowGridLines", "true" },
                { "ItemsPerPage", "20" },
                { "AutoLogoutMinutes", "30" },
                { "EnableNotifications", "true" },
                { "SoundEffects", "true" },
                { "RestaurantMode", "false" } // Ensure RestaurantMode is included in defaults
            };

            await SavePreferencesAsync(userId, defaultPreferences);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _semaphore.Dispose();
                }
                _disposed = true;
            }
        }

        ~SystemPreferencesService()
        {
            Dispose(false);
        }
    }
}