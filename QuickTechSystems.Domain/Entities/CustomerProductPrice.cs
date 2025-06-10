// Domain/Entities/CustomerProductPrice.cs
namespace QuickTechSystems.Domain.Entities
{
    public class CustomerProductPrice
    {
        public int CustomerProductPriceId { get; set; }
        public int CustomerId { get; set; }
        public int ProductId { get; set; }
        public decimal Price { get; set; }
        public virtual Customer Customer { get; set; }
        public virtual Product Product { get; set; }
    }
}