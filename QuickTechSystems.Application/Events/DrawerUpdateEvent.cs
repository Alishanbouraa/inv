namespace QuickTechSystems.Application.Events
{
    public class DrawerUpdateEvent
    {
        public string Type { get; }
        public decimal Amount { get; }
        public string Description { get; }

        public DrawerUpdateEvent(string type, decimal amount, string description)
        {
            Type = type;
            Amount = amount;
            Description = description;
        }
    }
}