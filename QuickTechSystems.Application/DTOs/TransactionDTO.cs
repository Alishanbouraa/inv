using System.Collections.ObjectModel;
using QuickTechSystems.Domain.Enums;

namespace QuickTechSystems.Application.DTOs
{
    public class TransactionDTO : BaseDTO
    {
        public int TransactionId { get; set; }
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public DateTime TransactionDate { get; set; }
        public TransactionType TransactionType { get; set; }
        public TransactionStatus Status { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string CashierId { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public ObservableCollection<TransactionDetailDTO> Details { get; set; } = new ObservableCollection<TransactionDetailDTO>();
        public string CashierRole { get; set; } = string.Empty;
    }
}