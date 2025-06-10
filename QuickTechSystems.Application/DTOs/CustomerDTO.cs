// QuickTechSystems.Application.DTOs/CustomerDTO.cs
namespace QuickTechSystems.Application.DTOs
{
    public class CustomerDTO : BaseDTO
    {
        public int CustomerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public int TransactionCount { get; set; }
        public decimal Balance { get; set; } = 0;
    }
}