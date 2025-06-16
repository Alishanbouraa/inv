using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;

namespace QuickTechSystems.Application.Services
{
    public class CustomerService : BaseService<Customer, CustomerDTO>, ICustomerService
    {
        private readonly IDrawerService _drawerService;

        public CustomerService(IUnitOfWork unitOfWork, IMapper mapper, IEventAggregator eventAggregator, IDbContextScopeService dbContextScopeService, IDrawerService drawerService = null) : base(unitOfWork, mapper, eventAggregator, dbContextScopeService) => _drawerService = drawerService;

        public async Task<IEnumerable<CustomerDTO>> GetByNameAsync(string name) => await _dbContextScopeService.ExecuteInScopeAsync(async context => _mapper.Map<IEnumerable<CustomerDTO>>(await _repository.Query().Where(c => c.Name.Contains(name)).ToListAsync()));

        public override async Task<bool> UpdateAsync(CustomerDTO entity) => await _dbContextScopeService.ExecuteInScopeAsync(async context => await UpdateCustomerAndPublish(entity));

        public async Task<bool> UpdatePaymentTransactionAsync(int transactionId, decimal newAmount, string reason) => await _dbContextScopeService.ExecuteInScopeAsync(async context =>
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var paymentTransaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
                if (paymentTransaction?.CustomerId == null) return false;
                var customer = await _repository.GetByIdAsync(paymentTransaction.CustomerId.Value);
                if (customer == null) return false;
                var originalAmount = paymentTransaction.PaidAmount;
                customer.Balance += originalAmount - newAmount;
                customer.UpdatedAt = DateTime.Now;
                await _repository.UpdateAsync(customer);
                paymentTransaction.PaidAmount = paymentTransaction.TotalAmount = newAmount;
                var updateInfo = $"Updated: {DateTime.Now:MM/dd/yyyy} - Original: {originalAmount:C2}, New: {newAmount:C2}";
                if (paymentTransaction.CashierName.Length + updateInfo.Length <= 100) paymentTransaction.CashierName = $"{paymentTransaction.CashierName} | {updateInfo}";
                else paymentTransaction.CashierRole = $"Reason: {reason} | {updateInfo}";
                await _unitOfWork.Transactions.UpdateAsync(paymentTransaction);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();
                PublishCustomerEvent("Update", customer);
                return true;
            }
            catch { await transaction.RollbackAsync(); throw; }
        });

        public new async Task<CustomerDTO?> GetByIdAsync(int id) => await _dbContextScopeService.ExecuteInScopeAsync(async context => _mapper.Map<CustomerDTO>(await _repository.Query().AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == id)));



        public async Task<bool> UpdateBalanceAsync(int customerId, decimal amount) => await _dbContextScopeService.ExecuteInScopeAsync(async context =>
        {
            var customer = await _repository.GetByIdAsync(customerId);
            if (customer == null) return false;
            customer.Balance += amount;
            customer.UpdatedAt = DateTime.Now;
            await _repository.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();
            PublishCustomerEvent("Update", customer);
            return true;
        });

        public async Task<bool> ProcessPaymentAsync(int customerId, decimal amount, string reference)
        {
            for (int retryCount = 0; retryCount < 3; retryCount++)
            {
                try
                {
                    return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
                    {
                        using var transaction = await _unitOfWork.BeginTransactionAsync();
                        try
                        {
                            var customer = await _repository.GetByIdAsync(customerId);
                            if (customer == null || amount <= 0 || amount > customer.Balance) return amount <= 0 ? throw new InvalidOperationException("Payment amount must be greater than zero") : amount > customer.Balance ? throw new InvalidOperationException("Payment amount cannot exceed customer balance") : false;
                            customer.Balance -= amount;
                            customer.UpdatedAt = DateTime.Now;
                            await _repository.UpdateAsync(customer);
                            await _unitOfWork.SaveChangesAsync();
                            await _unitOfWork.Transactions.AddAsync(new Transaction { CustomerId = customerId, CustomerName = customer.Name, TotalAmount = amount, PaidAmount = amount, TransactionDate = DateTime.Now, TransactionType = Domain.Enums.TransactionType.Adjustment, Status = Domain.Enums.TransactionStatus.Completed, PaymentMethod = "Cash", CashierId = "System", CashierName = "Debt Payment" });
                            await _unitOfWork.SaveChangesAsync();
                            if (_drawerService != null) await _drawerService.ProcessCashReceiptAsync(amount, $"Debt payment from: {customer.Name}, Ref: {reference}");
                            await transaction.CommitAsync();
                            PublishCustomerEvent("Update", customer);
                            return true;
                        }
                        catch { await transaction.RollbackAsync(); throw; }
                    });
                }
                catch { if (retryCount >= 2) throw; await Task.Delay(500 * (retryCount + 1)); }
            }
            return false;
        }

        public async Task<decimal> GetBalanceAsync(int customerId) => await _dbContextScopeService.ExecuteInScopeAsync(async context => (await _repository.GetByIdAsync(customerId))?.Balance ?? 0);

        private async Task<bool> UpdateCustomerAndPublish(CustomerDTO entity)
        {
            var customer = await _repository.GetByIdAsync(entity.CustomerId);
            if (customer == null) return false;
            customer.Name = entity.Name;
            customer.Phone = entity.Phone;
            customer.Email = entity.Email;
            customer.Address = entity.Address;
            customer.IsActive = entity.IsActive;
            customer.UpdatedAt = DateTime.Now;
            customer.Balance = entity.Balance;
            await _repository.UpdateAsync(customer);
            await _unitOfWork.SaveChangesAsync();
            PublishCustomerEvent("Update", customer);
            return true;
        }

        private void PublishCustomerEvent(string action, Customer customer) => _eventAggregator.Publish(new EntityChangedEvent<CustomerDTO>(action, _mapper.Map<CustomerDTO>(customer)));
    }
}