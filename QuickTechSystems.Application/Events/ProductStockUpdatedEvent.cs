// Path: QuickTechSystems.Application.Events/ProductStockUpdatedEvent.cs
namespace QuickTechSystems.Application.Events
{
    public class ProductStockUpdatedEvent
    {
        public int ProductId { get; private set; }
        public decimal NewStock { get; private set; }

        public ProductStockUpdatedEvent(int productId, decimal newStock)
        {
            ProductId = productId;
            NewStock = newStock;
        }
    }
}