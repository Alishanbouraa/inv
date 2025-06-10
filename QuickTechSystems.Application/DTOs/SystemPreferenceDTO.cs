// Path: QuickTechSystems.Application/DTOs/SystemPreferenceDTO.cs
namespace QuickTechSystems.Application.DTOs
{
    public class SystemPreferenceDTO
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string PreferenceKey { get; set; } = string.Empty;
        public string PreferenceValue { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }
}