namespace QuickTechSystems.Application.DTOs
{
    public class ActivityLogDTO
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public string? SessionId { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ModuleName { get; set; }
        public string? EntityId { get; set; }
        public string? EntityType { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }

        // Additional properties for display purposes
        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string StatusText => IsSuccess ? "Success" : "Failed";
        public string ChangeDescription => string.IsNullOrEmpty(OldValue) && string.IsNullOrEmpty(NewValue)
            ? string.Empty
            : $"Changed from '{OldValue}' to '{NewValue}'";
    }
}