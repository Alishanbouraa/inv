// Path: QuickTechSystems.Application/DTOs/BusinessSettingDTO.cs
namespace QuickTechSystems.Application.DTOs
{
    public class BusinessSettingDTO
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string DataType { get; set; } = "string";
        public bool IsSystem { get; set; }
        public DateTime LastModified { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;
    }
}