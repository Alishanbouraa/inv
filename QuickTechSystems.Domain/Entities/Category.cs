namespace QuickTechSystems.Domain.Entities
{
    public class Category
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public string Type { get; set; } = "Product"; // New property

        // Navigation property
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}