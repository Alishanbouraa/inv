namespace QuickTechSystems.Application.DTOs
{
    public class DrawerTransactionDTO
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
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal ResultingBalance
        {
            get => Balance;
            set => Balance = value;
        }
    }
}