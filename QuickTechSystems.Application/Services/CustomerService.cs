// QuickTechSystems.Application.Services/CustomerService.cs
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

        public CustomerService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService,
            IDrawerService drawerService = null) // Optional to allow for DI resolution
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _drawerService = drawerService;
        }

        public async Task<IEnumerable<CustomerDTO>> GetByNameAsync(string name)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var customers = await _repository.Query()
                    .Where(c => c.Name.Contains(name))
                    .ToListAsync();
                return _mapper.Map<IEnumerable<CustomerDTO>>(customers);
            });
        }
        public override async Task<bool> UpdateAsync(CustomerDTO entity)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var customer = await _repository.GetByIdAsync(entity.CustomerId);
                    if (customer == null)
                        return false;

                    // Explicitly map all properties including Balance
                    customer.Name = entity.Name;
                    customer.Phone = entity.Phone;
                    customer.Email = entity.Email;
                    customer.Address = entity.Address;
                    customer.IsActive = entity.IsActive;
                    customer.UpdatedAt = DateTime.Now;
                    customer.Balance = entity.Balance; // Explicitly set Balance

                    await _repository.UpdateAsync(customer);
                    await _unitOfWork.SaveChangesAsync();

                    // Publish customer updated event
                    var customerDto = _mapper.Map<CustomerDTO>(customer);
                    _eventAggregator.Publish(new EntityChangedEvent<CustomerDTO>("Update", customerDto));

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating customer: {ex.Message}");
                    throw;
                }
            });
        }


        public async Task<bool> UpdatePaymentTransactionAsync(int transactionId, decimal newAmount, string reason)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Get the transaction
                    var paymentTransaction = await _unitOfWork.Transactions.GetByIdAsync(transactionId);
                    if (paymentTransaction == null)
                        return false;

                    // Get the original amount
                    decimal originalAmount = paymentTransaction.PaidAmount;

                    // Calculate difference (positive if payment decreased, negative if increased)
                    decimal amountDifference = originalAmount - newAmount;

                    // Handle nullable CustomerId
                    if (!paymentTransaction.CustomerId.HasValue)
                        return false;

                    // Get the customer using the non-nullable value
                    var customer = await _repository.GetByIdAsync(paymentTransaction.CustomerId.Value);
                    if (customer == null)
                        return false;

                    // Update customer balance (add the difference back to balance)
                    // If payment decreased (e.g. $10 → $5), balance increases by $5
                    // If payment increased (e.g. $5 → $10), balance decreases by $5
                    customer.Balance += amountDifference;
                    customer.UpdatedAt = DateTime.Now;

                    Debug.WriteLine($"Updating customer {customer.CustomerId} balance from {customer.Balance - amountDifference} to {customer.Balance}");
                    await _repository.UpdateAsync(customer);

                    // Update transaction amount
                    paymentTransaction.PaidAmount = newAmount;
                    paymentTransaction.TotalAmount = newAmount;

                    // Since there's no Notes property, store update info in CashierName
                    // Format: "Original Cashier Name | Updated: [date] - Original: $X, New: $Y, Reason: [reason]"
                    string updateInfo = $"Updated: {DateTime.Now:MM/dd/yyyy} - Original: {originalAmount:C2}, New: {newAmount:C2}";

                    // Append to CashierName if there's space, or use CashierRole for overflow
                    if (paymentTransaction.CashierName.Length + updateInfo.Length <= 100) // Assuming reasonable limit
                    {
                        paymentTransaction.CashierName = $"{paymentTransaction.CashierName} | {updateInfo}";
                    }
                    else
                    {
                        // Use CashierRole as overflow for update history
                        paymentTransaction.CashierRole = $"Reason: {reason} | {updateInfo}";
                    }

                    await _unitOfWork.Transactions.UpdateAsync(paymentTransaction);
                    await _unitOfWork.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Publish customer updated event
                    var customerDto = _mapper.Map<CustomerDTO>(customer);
                    _eventAggregator.Publish(new EntityChangedEvent<CustomerDTO>("Update", customerDto));

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating payment transaction: {ex.Message}");
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public new async Task<CustomerDTO?> GetByIdAsync(int id)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var customer = await _repository.Query()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.CustomerId == id);
                    return _mapper.Map<CustomerDTO>(customer);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting customer: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<IEnumerable<CustomerProductPriceDTO>> GetCustomProductPricesAsync(int customerId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var customerPrices = await _unitOfWork.Context.Set<CustomerProductPrice>()
                    .Include(cpp => cpp.Product)
                    .Where(cpp => cpp.CustomerId == customerId)
                    .Select(cpp => new CustomerProductPriceDTO
                    {
                        CustomerProductPriceId = cpp.CustomerProductPriceId,
                        CustomerId = cpp.CustomerId,
                        ProductId = cpp.ProductId,
                        Price = cpp.Price,
                        ProductName = cpp.Product.Name,
                        DefaultPrice = cpp.Product.SalePrice
                    })
                    .ToListAsync();

                return customerPrices;
            });
        }

        public async Task SetCustomProductPricesAsync(int customerId, IEnumerable<CustomerProductPriceDTO> prices)
        {
            await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var customerPricesSet = _unitOfWork.Context.Set<CustomerProductPrice>();

                // Remove existing prices for this customer
                var existingPrices = await customerPricesSet
                    .Where(cpp => cpp.CustomerId == customerId)
                    .ToListAsync();
                customerPricesSet.RemoveRange(existingPrices);

                // Add new prices
                var newPrices = prices.Select(p => new CustomerProductPrice
                {
                    CustomerId = customerId,
                    ProductId = p.ProductId,
                    Price = p.Price
                });

                await customerPricesSet.AddRangeAsync(newPrices);
                await _unitOfWork.SaveChangesAsync();

                // Publish event to notify of customer update
                var customer = await _repository.GetByIdAsync(customerId);
                if (customer != null)
                {
                    var customerDto = _mapper.Map<CustomerDTO>(customer);
                    _eventAggregator.Publish(new EntityChangedEvent<CustomerDTO>("Update", customerDto));
                }
            });
        }

        // New methods for debt management
        public async Task<bool> UpdateBalanceAsync(int customerId, decimal amount)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var customer = await _repository.GetByIdAsync(customerId);
                    if (customer == null)
                        return false;

                    // Update customer balance
                    customer.Balance += amount;
                    customer.UpdatedAt = DateTime.Now;

                    await _repository.UpdateAsync(customer);
                    await _unitOfWork.SaveChangesAsync();

                    // Publish customer updated event
                    var customerDto = _mapper.Map<CustomerDTO>(customer);
                    _eventAggregator.Publish(new EntityChangedEvent<CustomerDTO>("Update", customerDto));

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating customer balance: {ex.Message}");
                    return false;
                }
            });
        }
        public async Task<bool> ProcessPaymentAsync(int customerId, decimal amount, string reference)
        {
            // Add a retry mechanism for improved reliability
            const int maxRetries = 3;
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
                    {
                        // Start a transaction with a timeout
                        using var transaction = await _unitOfWork.BeginTransactionAsync();
                        try
                        {
                            // Get fresh customer data
                            var customer = await _repository.GetByIdAsync(customerId);
                            if (customer == null)
                            {
                                Debug.WriteLine($"Customer {customerId} not found");
                                return false;
                            }

                            if (amount <= 0)
                            {
                                Debug.WriteLine("Payment amount must be greater than zero");
                                throw new InvalidOperationException("Payment amount must be greater than zero");
                            }

                            if (amount > customer.Balance)
                            {
                                Debug.WriteLine($"Payment amount {amount} exceeds balance {customer.Balance}");
                                throw new InvalidOperationException("Payment amount cannot exceed customer balance");
                            }

                            // Update customer balance (reduce debt)
                            customer.Balance -= amount;
                            customer.UpdatedAt = DateTime.Now;

                            Debug.WriteLine($"Updating customer {customerId} balance to {customer.Balance}");
                            await _repository.UpdateAsync(customer);
                            await _unitOfWork.SaveChangesAsync();

                            // Create a payment transaction record
                            var paymentTransaction = new Transaction
                            {
                                CustomerId = customerId,
                                CustomerName = customer.Name,
                                TotalAmount = amount,
                                PaidAmount = amount, // Set PaidAmount explicitly for payment transactions
                                TransactionDate = DateTime.Now,
                                TransactionType = Domain.Enums.TransactionType.Adjustment,
                                Status = Domain.Enums.TransactionStatus.Completed,
                                PaymentMethod = "Cash",
                                CashierId = "System",
                                CashierName = "Debt Payment",
                            };

                            Debug.WriteLine("Adding transaction record");
                            await _unitOfWork.Transactions.AddAsync(paymentTransaction);
                            await _unitOfWork.SaveChangesAsync();

                            // Update drawer (increase cash) if the drawer service is available
                            if (_drawerService != null)
                            {
                                Debug.WriteLine("Processing cash receipt");
                                await _drawerService.ProcessCashReceiptAsync(
                                    amount,
                                    $"Debt payment from: {customer.Name}, Ref: {reference}"
                                );
                            }

                            Debug.WriteLine("Committing transaction");
                            await transaction.CommitAsync();

                            // Publish customer updated event
                            var customerDto = _mapper.Map<CustomerDTO>(customer);
                            _eventAggregator.Publish(new EntityChangedEvent<CustomerDTO>("Update", customerDto));

                            Debug.WriteLine("Payment processing completed successfully");
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing customer payment: {ex.Message}");
                            Debug.WriteLine($"Rolling back transaction");
                            await transaction.RollbackAsync();
                            throw;
                        }
                    });
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Debug.WriteLine($"Payment processing attempt {retryCount} failed: {ex.Message}");

                    if (retryCount >= maxRetries)
                    {
                        Debug.WriteLine("Max retries reached, giving up");
                        throw;
                    }

                    // Wait before retrying (exponential backoff)
                    await Task.Delay(500 * retryCount);
                }
            }

            // Should never reach here, but just in case
            return false;
        }

        public async Task<decimal> GetBalanceAsync(int customerId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var customer = await _repository.GetByIdAsync(customerId);
                return customer?.Balance ?? 0;
            });
        }
    }
}