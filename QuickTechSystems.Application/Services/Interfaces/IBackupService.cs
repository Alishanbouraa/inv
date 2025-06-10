using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// QuickTechSystems.Application/Services/Interfaces/IBackupService.cs
namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface IBackupService
    {
        Task<bool> CreateBackupAsync(string destinationPath);
        Task<bool> RestoreBackupAsync(string backupPath);
        Task<bool> ScheduleAutomaticBackupAsync(string destinationPath, DayOfWeek day, TimeSpan time);
        Task<bool> DisableAutomaticBackupAsync();
        Task<DateTime?> GetLastBackupTimeAsync();
        Task<(bool IsEnabled, DayOfWeek Day, TimeSpan Time)?> GetBackupScheduleAsync();
    }
}