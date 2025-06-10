// QuickTechSystems.Infrastructure/Services/BackupService.cs
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32.TaskScheduler;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Infrastructure.Data;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace QuickTechSystems.Infrastructure.Services
{
    public class BackupService : IBackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;
        private const string TaskName = "QuickTechSystemsBackup";

        public BackupService(ApplicationDbContext context)
        {
            _context = context;
            _connectionString = context.Database.GetConnectionString();
        }

        public async Task<bool> CreateBackupAsync(string destinationPath)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var databaseName = builder.InitialCatalog;
                var backupFile = Path.Combine(destinationPath, $"{databaseName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

                var backupQuery = $@"
            BACKUP DATABASE [{databaseName}] 
            TO DISK = '{backupFile}' 
            WITH FORMAT, MEDIANAME = 'QuickTechBackup', 
            NAME = 'Full Backup of QuickTechSystem'";

                await _context.Database.ExecuteSqlRawAsync(backupQuery);
                await StoreBackupInfoAsync(backupFile);

                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> RestoreBackupAsync(string backupPath)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var databaseName = builder.InitialCatalog;

                // Close all active connections to the database
                var closeConnectionsQuery = $@"
            USE [master];
            ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;";
                await _context.Database.ExecuteSqlRawAsync(closeConnectionsQuery);

                // Perform the restore operation
                var restoreQuery = $@"
            USE [master];
            RESTORE DATABASE [{databaseName}] 
            FROM DISK = '{backupPath}' 
            WITH REPLACE;";
                await _context.Database.ExecuteSqlRawAsync(restoreQuery);

                // Set the database back to multi-user mode
                var setMultiUserQuery = $"ALTER DATABASE [{databaseName}] SET MULTI_USER;";
                await _context.Database.ExecuteSqlRawAsync(setMultiUserQuery);

                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> ScheduleAutomaticBackupAsync(string destinationPath, DayOfWeek day, TimeSpan time)
        {
            try
            {
                using (Microsoft.Win32.TaskScheduler.TaskService ts = new Microsoft.Win32.TaskScheduler.TaskService())
                {
                    ts.RootFolder.DeleteTask(TaskName, false);

                    var td = ts.NewTask();
                    td.RegistrationInfo.Description = "QuickTech Systems Automatic Backup";

                    // Create weekly trigger
                    var trigger = new WeeklyTrigger
                    {
                        DaysOfWeek = GetDayOfWeekFlag(day),
                        StartBoundary = DateTime.Today + time
                    };
                    td.Triggers.Add(trigger);

                    // Create the action
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    td.Actions.Add(new ExecAction(exePath, $"--backup \"{destinationPath}\""));

                    // Set additional settings
                    td.Settings.ExecutionTimeLimit = TimeSpan.FromHours(2);
                    td.Settings.AllowHardTerminate = true;
                    td.Settings.StartWhenAvailable = true;
                    td.Settings.RunOnlyIfNetworkAvailable = true;

                    ts.RootFolder.RegisterTaskDefinition(TaskName, td);
                    await StoreScheduleInfoAsync(true, day, time);

                    return true;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<bool> DisableAutomaticBackupAsync()
        {
            try
            {
                using (var ts = new Microsoft.Win32.TaskScheduler.TaskService())
                {
                    ts.RootFolder.DeleteTask(TaskName, false);
                }

                await StoreScheduleInfoAsync(false, DayOfWeek.Sunday, TimeSpan.Zero);
                return true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<DateTime?> GetLastBackupTimeAsync()
        {
            try
            {
                var setting = await _context.SystemPreferences
                    .FirstOrDefaultAsync(p => p.PreferenceKey == "LastBackupTime");

                if (setting != null && DateTime.TryParse(setting.PreferenceValue, out DateTime lastBackup))
                {
                    return lastBackup;
                }

                return null;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<(bool IsEnabled, DayOfWeek Day, TimeSpan Time)?> GetBackupScheduleAsync()
        {
            try
            {
                var enabled = await _context.SystemPreferences
                    .FirstOrDefaultAsync(p => p.PreferenceKey == "BackupEnabled");
                var day = await _context.SystemPreferences
                    .FirstOrDefaultAsync(p => p.PreferenceKey == "BackupDay");
                var time = await _context.SystemPreferences
                    .FirstOrDefaultAsync(p => p.PreferenceKey == "BackupTime");

                if (enabled != null && day != null && time != null)
                {
                    return (
                        bool.Parse(enabled.PreferenceValue),
                        Enum.Parse<DayOfWeek>(day.PreferenceValue),
                        TimeSpan.Parse(time.PreferenceValue)
                    );
                }

                return null;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private async Task StoreBackupInfoAsync(string backupPath)
        {
            var preference = await _context.SystemPreferences
                .FirstOrDefaultAsync(p => p.PreferenceKey == "LastBackupTime")
                ?? new Domain.Entities.SystemPreference { PreferenceKey = "LastBackupTime" };

            preference.PreferenceValue = DateTime.Now.ToString("O");
            preference.LastModified = DateTime.Now;

            if (preference.Id == 0)
                _context.SystemPreferences.Add(preference);
            else
                _context.SystemPreferences.Update(preference);

            await _context.SaveChangesAsync();
        }

        private async Task StoreScheduleInfoAsync(bool enabled, DayOfWeek day, TimeSpan time)
        {
            await UpdatePreference("BackupEnabled", enabled.ToString());
            await UpdatePreference("BackupDay", day.ToString());
            await UpdatePreference("BackupTime", time.ToString());
        }

        private async Task UpdatePreference(string key, string value)
        {
            var preference = await _context.SystemPreferences
                .FirstOrDefaultAsync(p => p.PreferenceKey == key)
                ?? new Domain.Entities.SystemPreference { PreferenceKey = key };

            preference.PreferenceValue = value;
            preference.LastModified = DateTime.Now;

            if (preference.Id == 0)
                _context.SystemPreferences.Add(preference);
            else
                _context.SystemPreferences.Update(preference);

            await _context.SaveChangesAsync();
        }

        private DaysOfTheWeek GetDayOfWeekFlag(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Sunday => DaysOfTheWeek.Sunday,
                DayOfWeek.Monday => DaysOfTheWeek.Monday,
                DayOfWeek.Tuesday => DaysOfTheWeek.Tuesday,
                DayOfWeek.Wednesday => DaysOfTheWeek.Wednesday,
                DayOfWeek.Thursday => DaysOfTheWeek.Thursday,
                DayOfWeek.Friday => DaysOfTheWeek.Friday,
                DayOfWeek.Saturday => DaysOfTheWeek.Saturday,
                _ => DaysOfTheWeek.Sunday
            };
        }
    }
}