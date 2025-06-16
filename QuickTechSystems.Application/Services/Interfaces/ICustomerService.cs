using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    public interface ICustomerService : IBaseService<CustomerDTO>
    {
        Task<IEnumerable<CustomerDTO>> GetByNameAsync(string name);
        Task<bool> UpdateBalanceAsync(int customerId, decimal amount);
        Task<bool> ProcessPaymentAsync(int customerId, decimal amount, string reference);
        Task<decimal> GetBalanceAsync(int customerId);
        Task<bool> UpdatePaymentTransactionAsync(int transactionId, decimal newAmount, string reason);
    }
}