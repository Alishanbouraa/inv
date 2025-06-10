// Application/DTOs/CustomerProductPriceDTO.cs
namespace QuickTechSystems.Application.DTOs
{
    public class CustomerProductPriceDTO
    {
        public int CustomerProductPriceId { get; set; }
        public int CustomerId { get; set; }
        public int ProductId { get; set; }
        public decimal Price { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal DefaultPrice { get; set; }
    }
}