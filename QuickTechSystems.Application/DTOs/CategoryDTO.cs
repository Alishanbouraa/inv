namespace QuickTechSystems.Application.DTOs
{
    public class CategoryDTO : BaseDTO
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = "Product"; // "Product" or "Expense"
        public int ProductCount { get; set; }
    }
}