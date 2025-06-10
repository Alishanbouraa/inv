// Path: QuickTechSystems.Domain/Entities/SystemPreference.cs
namespace QuickTechSystems.Domain.Entities
{
    public class SystemPreference
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string PreferenceKey { get; set; } = string.Empty;
        public string PreferenceValue { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}