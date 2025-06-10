namespace QuickTechSystems.Application.DTOs
{
    public abstract class BaseDTO
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}