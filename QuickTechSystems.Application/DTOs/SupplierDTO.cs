namespace QuickTechSystems.Application.DTOs
{
    public class SupplierDTO : BaseDTO
    {
        public int SupplierId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public decimal Balance { get; set; }
        public string? TaxNumber { get; set; }
        public int ProductCount { get; set; }
        public int TransactionCount { get; set; }
    }
}