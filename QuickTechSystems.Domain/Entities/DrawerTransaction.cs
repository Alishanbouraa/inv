namespace QuickTechSystems.Domain.Entities
{
    public class DrawerTransaction
    {
        public int TransactionId { get; set; }
        public int DrawerId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
        public string? Notes { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TransactionReference { get; set; } = string.Empty;
        public bool IsVoided { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public virtual Drawer? Drawer { get; set; }

        public decimal GetNewBalance(decimal currentBalance, string transactionType, decimal amount)
        {
            switch (transactionType.ToLower())
            {
                case "expense":
                case "supplier payment":
                case "cash out":
                    return currentBalance - amount;
                case "open":
                    return amount;
                case "cash in":
                case "cash sale":
                    return currentBalance + amount;
                default:
                    return currentBalance;
            }
        }
    }
}