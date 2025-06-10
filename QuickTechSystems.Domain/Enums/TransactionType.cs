namespace QuickTechSystems.Domain.Enums
{
    public enum TransactionType
    {
        Sale,
        Purchase,
        Adjustment
    }

    public enum TransactionStatus
    {
        Pending,
        Completed,
        Cancelled
    }
}