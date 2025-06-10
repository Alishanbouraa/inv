// QuickTechSystems.Domain.Entities/Customer.cs
namespace QuickTechSystems.Domain.Entities
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public required string Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public decimal Balance { get; set; } = 0;
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}