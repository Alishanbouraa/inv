using System;

namespace QuickTechSystems.Domain.Entities
{
    public class ActivityLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public string? SessionId { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string? ErrorMessage { get; set; }
        public string? ModuleName { get; set; }
        public string? EntityId { get; set; }
        public string? EntityType { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
    }
}