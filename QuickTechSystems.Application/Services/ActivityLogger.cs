using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;
using System.Net;

namespace QuickTechSystems.Application.Services
{
    public class ActivityLogger : IActivityLogger
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ActivityLogger(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task LogActivityAsync(string userId, string action, string details, bool isSuccess = true, string? errorMessage = null)
        {
            var activity = new ActivityLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                SessionId = GetCurrentSessionId(),
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage,
                ModuleName = GetCurrentModule()
            };

            await _unitOfWork.Context.Set<ActivityLog>().AddAsync(activity);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task LogEntityChangeAsync(string userId, string entityType, string entityId, string action, string? oldValue = null, string? newValue = null)
        {
            var activity = new ActivityLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValue = oldValue,
                NewValue = newValue,
                Timestamp = DateTime.UtcNow,
                IpAddress = GetClientIpAddress(),
                UserAgent = GetUserAgent(),
                SessionId = GetCurrentSessionId(),
                ModuleName = GetCurrentModule()
            };

            await _unitOfWork.Context.Set<ActivityLog>().AddAsync(activity);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<IEnumerable<ActivityLogDTO>> GetUserActivitiesAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _unitOfWork.Context.Set<ActivityLog>().AsQueryable();

            query = query.Where(a => a.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            var activities = await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            return _mapper.Map<IEnumerable<ActivityLogDTO>>(activities);
        }

        public async Task<IEnumerable<ActivityLogDTO>> GetModuleActivitiesAsync(string moduleName, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _unitOfWork.Context.Set<ActivityLog>()
                .Where(a => a.ModuleName == moduleName);

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            var activities = await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            return _mapper.Map<IEnumerable<ActivityLogDTO>>(activities);
        }

        public async Task<IEnumerable<ActivityLogDTO>> GetEntityChangesAsync(string entityType, string entityId)
        {
            var activities = await _unitOfWork.Context.Set<ActivityLog>()
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            return _mapper.Map<IEnumerable<ActivityLogDTO>>(activities);
        }

        public async Task<IEnumerable<ActivityLogDTO>> SearchActivitiesAsync(string searchTerm)
        {
            var activities = await _unitOfWork.Context.Set<ActivityLog>()
                .Where(a => a.Action.Contains(searchTerm) ||
                           a.Details.Contains(searchTerm) ||
                           a.UserId.Contains(searchTerm) ||
                           a.ModuleName.Contains(searchTerm))
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            return _mapper.Map<IEnumerable<ActivityLogDTO>>(activities);
        }

        public async Task<int> GetUserActivityCountAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _unitOfWork.Context.Set<ActivityLog>()
                .Where(a => a.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            return await query.CountAsync();
        }

        public async Task ClearOldLogsAsync(int daysToKeep)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var oldLogs = await _unitOfWork.Context.Set<ActivityLog>()
                .Where(a => a.Timestamp < cutoffDate)
                .ToListAsync();

            _unitOfWork.Context.Set<ActivityLog>().RemoveRange(oldLogs);
            await _unitOfWork.SaveChangesAsync();
        }

        private string GetClientIpAddress()
        {
            // In a WPF application, this would typically be the local machine's IP
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                ?.ToString() ?? "127.0.0.1";
        }

        private string GetUserAgent()
        {
            return $"QuickTechSystems WPF Client v{GetApplicationVersion()}";
        }

        private string GetApplicationVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }

        private string GetCurrentSessionId()
        {
            // In a WPF application, this could be a unique identifier generated at startup
            return System.Threading.Thread.CurrentThread.ManagedThreadId.ToString();
        }

        private string GetCurrentModule()
        {
            // This would typically be set by the calling code
            return "WPF Client";
        }
    }
}