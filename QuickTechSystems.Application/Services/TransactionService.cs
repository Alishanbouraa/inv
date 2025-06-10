using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Enums;
using QuickTechSystems.Domain.Interfaces.Repositories;

namespace QuickTechSystems.Application.Services
{
    public class TransactionService : BaseService<Transaction, TransactionDTO>, ITransactionService
    {
        private readonly IProductService _productService;
        private readonly IDrawerService _drawerService;
        private readonly ICustomerService _customerService;

        public TransactionService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IProductService productService,
            IDrawerService drawerService,
            ICustomerService customerService,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _productService = productService;
            _drawerService = drawerService;
            _customerService = customerService;
        }

        public async Task<TransactionDTO> UpdateAsync(TransactionDTO transactionDto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Get the existing transaction
                    var existingTransaction = await _repository.GetByIdAsync(transactionDto.TransactionId);
                    if (existingTransaction == null)
                    {
                        throw new InvalidOperationException($"Transaction #{transactionDto.TransactionId} not found");
                    }

                    // Store the original total amount for drawer sync
                    decimal originalAmount = existingTransaction.TotalAmount;
                    string originalPaymentMethod = existingTransaction.PaymentMethod;

                    // Update the existing transaction properties
                    existingTransaction.CustomerId = transactionDto.CustomerId;
                    existingTransaction.CustomerName = transactionDto.CustomerName;
                    existingTransaction.TotalAmount = transactionDto.TotalAmount;
                    existingTransaction.PaidAmount = transactionDto.PaidAmount;
                    existingTransaction.Status = transactionDto.Status;
                    existingTransaction.PaymentMethod = transactionDto.PaymentMethod; // Important: preserve payment method

                    // Get current transaction details from database
                    var existingDetails = await _unitOfWork.Context.Set<TransactionDetail>()
                        .Where(td => td.TransactionId == transactionDto.TransactionId)
                        .ToListAsync();

                    // Remove details that are no longer in the updated transaction
                    var detailsToRemove = existingDetails
                        .Where(ed => !transactionDto.Details.Any(d => d.TransactionDetailId == ed.TransactionDetailId))
                        .ToList();

                    foreach (var detail in detailsToRemove)
                    {
                        // Revert stock changes
                        await _productService.UpdateStockAsync(detail.ProductId, detail.Quantity);
                        _unitOfWork.Context.Set<TransactionDetail>().Remove(detail);
                    }

                    // Update existing details and add new ones
                    foreach (var detailDto in transactionDto.Details)
                    {
                        if (detailDto.TransactionDetailId > 0)
                        {
                            // Update existing detail
                            var existingDetail = existingDetails
                                .FirstOrDefault(ed => ed.TransactionDetailId == detailDto.TransactionDetailId);

                            if (existingDetail != null)
                            {
                                // Calculate stock difference - with explicit cast to resolve the decimal-to-int conversion
                                int quantityDifference = (int)(detailDto.Quantity - existingDetail.Quantity);
                                if (quantityDifference != 0)
                                {
                                    // Update stock
                                    await _productService.UpdateStockAsync(detailDto.ProductId, -quantityDifference);
                                }

                                // Update the detail
                                existingDetail.Quantity = detailDto.Quantity;
                                existingDetail.UnitPrice = detailDto.UnitPrice;
                                existingDetail.Discount = detailDto.Discount;
                                existingDetail.Total = detailDto.Total;
                            }
                        }
                        else
                        {
                            // Add new detail
                            var newDetail = _mapper.Map<TransactionDetail>(detailDto);
                            newDetail.TransactionId = transactionDto.TransactionId;
                            await _unitOfWork.Context.Set<TransactionDetail>().AddAsync(newDetail);

                            // Update stock
                            await _productService.UpdateStockAsync(detailDto.ProductId, -detailDto.Quantity);
                        }
                    }

                    // Update the transaction
                    await _repository.UpdateAsync(existingTransaction);
                    await _unitOfWork.SaveChangesAsync();

                    // Check if total amount has changed
                    decimal amountDifference = transactionDto.TotalAmount - originalAmount;

                    // Finish transaction operations - cash transactions are handled by the caller
                    await transaction.CommitAsync();

                    // Get the updated transaction with all its details
                    var updatedTransaction = await _repository.Query()
                        .Include(t => t.Customer)
                        .Include(t => t.TransactionDetails)
                            .ThenInclude(td => td.Product)
                        .FirstOrDefaultAsync(t => t.TransactionId == transactionDto.TransactionId);

                    var result = _mapper.Map<TransactionDTO>(updatedTransaction);

                    // Publish event to notify other components of the update
                    _eventAggregator.Publish(new EntityChangedEvent<TransactionDTO>("Update", result));

                    return result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating transaction: {ex.Message}");
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<IEnumerable<TransactionDTO>> GetByEmployeeAsync(int employeeId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _repository.Query()
                    .Include(t => t.Customer)
                    .Include(t => t.TransactionDetails)
                        .ThenInclude(td => td.Product)
                    .Where(t => t.CashierId == employeeId.ToString())
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<TransactionDTO>>(transactions);
            });
        }

        public async Task<IEnumerable<TransactionDTO>> GetByEmployeeAndDateRangeAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Normalize dates to start and end of day
                    var normalizedStartDate = startDate.Date;
                    var normalizedEndDate = endDate.Date.AddDays(1).AddTicks(-1);

                    var transactions = await _repository.Query()
                        .Include(t => t.Customer)
                        .Include(t => t.TransactionDetails)
                            .ThenInclude(td => td.Product)
                                .ThenInclude(p => p.Category)
                        .Where(t => t.CashierId == employeeId.ToString() &&
                                   t.TransactionDate >= normalizedStartDate &&
                                   t.TransactionDate <= normalizedEndDate &&
                                   t.Status == TransactionStatus.Completed)
                        .OrderByDescending(t => t.TransactionDate)
                        .AsNoTracking()
                        .ToListAsync();

                    if (transactions == null || !transactions.Any())
                    {
                        Debug.WriteLine($"No transactions found for employee {employeeId} between {normalizedStartDate:d} and {normalizedEndDate:d}");
                        return new List<TransactionDTO>();
                    }

                    var dtos = _mapper.Map<IEnumerable<TransactionDTO>>(transactions);

                    // Calculate detailed information for each transaction
                    foreach (var dto in dtos)
                    {
                        if (dto.Details != null)
                        {
                            foreach (var detail in dto.Details)
                            {
                                // Ensure proper calculations
                                detail.UpdateTotal();
                            }
                        }
                    }

                    return dtos;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetByEmployeeAndDateRangeAsync: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw new InvalidOperationException($"Error retrieving transactions for employee {employeeId} by date range", ex);
                }
            });
        }
        public async Task<decimal> GetTransactionProfitByDateRangeAsync(
        DateTime startDate, DateTime endDate, int? categoryId = null, int? employeeId = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Normalize dates to start and end of day
                    var normalizedStartDate = startDate.Date;
                    var normalizedEndDate = endDate.Date.AddDays(1).AddTicks(-1);

                    // Get all relevant transactions
                    var transactionsQuery = _repository.Query()
                        .Include(t => t.TransactionDetails)
                            .ThenInclude(td => td.Product)
                        .Where(t => t.TransactionDate >= normalizedStartDate &&
                                   t.TransactionDate <= normalizedEndDate &&
                                   t.Status == TransactionStatus.Completed &&
                                   t.TransactionType == TransactionType.Sale);

                    // Apply category filter if specified
                    if (categoryId.HasValue && categoryId.Value > 0)
                    {
                        transactionsQuery = transactionsQuery.Where(t =>
                            t.TransactionDetails.Any(d => d.Product.CategoryId == categoryId.Value));
                    }

                    // Apply employee filter if specified
                    if (employeeId.HasValue && employeeId.Value > 0)
                    {
                        transactionsQuery = transactionsQuery.Where(t => t.CashierId == employeeId.Value.ToString());
                    }

                    var transactions = await transactionsQuery.ToListAsync();

                    // Calculate profit
                    decimal totalProfit = 0;
                    foreach (var transaction in transactions)
                    {
                        if (transaction.TransactionDetails == null || !transaction.TransactionDetails.Any())
                            continue;

                        if (transaction.TotalAmount == 0)
                            continue;

                        // Calculate total purchase cost
                        decimal purchaseCost = transaction.TransactionDetails.Sum(d => d.PurchasePrice * d.Quantity);

                        // Calculate profit
                        totalProfit += (transaction.TotalAmount - purchaseCost);
                    }

                    return totalProfit;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting transaction profit: {ex.Message}");
                    throw new InvalidOperationException("Error retrieving transaction profit", ex);
                }
            });
        }
        public async Task<bool> DeleteAsync(int transactionId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Get the transaction with details
                    var existingTransaction = await _repository.Query()
                        .Include(t => t.TransactionDetails)
                        .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

                    if (existingTransaction == null)
                        return false;

                    // First, delete related drawer transactions
                    var drawerTransactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                        .Where(dt => dt.TransactionReference == transactionId.ToString() ||
                               dt.TransactionReference == $"Transaction #{transactionId}")
                        .ToListAsync();

                    foreach (var drawerTransaction in drawerTransactions)
                    {
                        _unitOfWork.Context.Set<DrawerTransaction>().Remove(drawerTransaction);
                    }

                    // Check if this is a completed sale and restore stock if needed
                    if (existingTransaction.TransactionType == TransactionType.Sale &&
                        existingTransaction.Status == TransactionStatus.Completed)
                    {
                        // Restore stock for each item in the transaction
                        foreach (var detail in existingTransaction.TransactionDetails)
                        {
                            await _productService.UpdateStockAsync(
                                detail.ProductId,
                                detail.Quantity); // Add stock back (positive value)

                            // Add inventory history entry
                            await _unitOfWork.Context.Set<InventoryHistory>().AddAsync(new InventoryHistory
                            {
                                ProductId = detail.ProductId,
                                QuantityChange = detail.Quantity,
                                Type = TransactionType.Adjustment.ToString(),
                                Timestamp = DateTime.Now,
                                Notes = $"Stock restored due to transaction deletion | Ref: DeleteTx-{transactionId}"
                            });
                        }
                    }

                    // Delete transaction details first (cascade doesn't always work reliably)
                    foreach (var detail in existingTransaction.TransactionDetails.ToList())
                    {
                        _unitOfWork.Context.Set<TransactionDetail>().Remove(detail);
                    }

                    // Delete the transaction
                    await _repository.DeleteAsync(existingTransaction);
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Create a DTO for the event
                    var dto = _mapper.Map<TransactionDTO>(existingTransaction);
                    _eventAggregator.Publish(new EntityChangedEvent<TransactionDTO>("Delete", dto));

                    // Also publish a drawer update event to refresh drawer views
                    _eventAggregator.Publish(new DrawerUpdateEvent(
                        "Transaction Deletion",
                        0,
                        $"Transaction #{transactionId} deleted"
                    ));

                    // Publish product update events for affected products
                    foreach (var detail in existingTransaction.TransactionDetails)
                    {
                        var updatedProduct = await _productService.GetByIdAsync(detail.ProductId);
                        if (updatedProduct != null)
                        {
                            _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", updatedProduct));
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deleting transaction: {ex.Message}");
                    await transaction.RollbackAsync();
                    return false;
                }
            });
        }

        public async Task<TransactionDTO> ProcessSaleAsync(TransactionDTO transactionDto, int cashierId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    // Retrieve cashier information
                    var cashier = await _unitOfWork.Employees.GetByIdAsync(cashierId);
                    if (cashier == null)
                        throw new ArgumentException("Invalid cashier ID");

                    // Set cashier information
                    transactionDto.CashierId = cashier.EmployeeId.ToString();
                    transactionDto.CashierName = $"{cashier.FirstName} {cashier.LastName}";
                    transactionDto.CashierRole = cashier.Role;

                    // For each detail, get the product's purchase price and update stock
                    foreach (var detail in transactionDto.Details)
                    {
                        var product = await _productService.GetByIdAsync(detail.ProductId);
                        if (product != null)
                        {
                            detail.PurchasePrice = product.PurchasePrice;

                            // Decrement stock - negative quantity because it's a sale
                            bool stockUpdated = await _productService.UpdateStockAsync(
                                detail.ProductId,
                                -detail.Quantity);

                            if (!stockUpdated)
                            {
                                Debug.WriteLine($"Warning: Could not update stock for product {detail.ProductId} - product may not exist");
                                // Continue with the transaction - don't throw an exception
                            }
                            else
                            {
                                Debug.WriteLine($"Successfully updated stock for product {detail.ProductId}");
                            }

                            // Add inventory history entry
                            await _unitOfWork.Context.Set<InventoryHistory>().AddAsync(new InventoryHistory
                            {
                                ProductId = detail.ProductId,
                                QuantityChange = -detail.Quantity,
                                Type = TransactionType.Sale.ToString(),
                                Timestamp = DateTime.Now,
                                Notes = $"Sold in transaction by {cashier.FirstName} {cashier.LastName} | Ref: Sale-{DateTime.Now:yyyyMMddHHmmss}"
                            });
                        }
                    }

                    // Create the transaction record
                    var entity = _mapper.Map<Transaction>(transactionDto);
                    await _repository.AddAsync(entity);
                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Map the entity back to DTO for publishing event - AFTER transaction commit
                    var resultDto = _mapper.Map<TransactionDTO>(entity);

                    // Publish events AFTER transaction is committed
                    foreach (var detail in transactionDto.Details)
                    {
                        var updatedProduct = await _productService.GetByIdAsync(detail.ProductId);
                        if (updatedProduct != null)
                        {
                            _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", updatedProduct));
                        }
                    }

                    // Publish transaction created event
                    _eventAggregator.Publish(new EntityChangedEvent<TransactionDTO>(
                        "Create",
                        resultDto
                    ));

                    Debug.WriteLine($"Transaction {resultDto.TransactionId} processed successfully with cashier {cashierId}");
                    return resultDto;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing sale with cashier: {ex.Message}");
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<IEnumerable<TransactionDTO>> GetByCustomerAsync(int customerId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _repository.Query()
                    .Include(t => t.Customer)
                    .Include(t => t.TransactionDetails)
                    .ThenInclude(td => td.Product)
                    .Where(t => t.CustomerId == customerId)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<TransactionDTO>>(transactions);
            });
        }

        public async Task<TransactionDTO> ProcessPaymentTransactionAsync(TransactionDTO transaction)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                if (transaction == null)
                    throw new ArgumentNullException(nameof(transaction));

                using var dbTransaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var entity = _mapper.Map<Transaction>(transaction);
                    await _repository.AddAsync(entity);
                    await _unitOfWork.SaveChangesAsync();

                    // Process cash drawer transaction
                    if (transaction.PaymentMethod == "Cash")
                    {
                        await _drawerService.ProcessCashSaleAsync(
                            transaction.PaidAmount,
                            $"Transaction #{entity.TransactionId}"
                        );
                    }

                    await dbTransaction.CommitAsync();
                    return _mapper.Map<TransactionDTO>(entity);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing payment transaction: {ex.Message}");
                    await dbTransaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<IEnumerable<TransactionDTO>> GetByCustomerAndDateRangeAsync(int customerId, DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Normalize dates to start and end of day
                    var normalizedStartDate = startDate.Date;
                    var normalizedEndDate = endDate.Date.AddDays(1).AddTicks(-1);

                    // Get transactions for the specific customer within date range
                    var transactions = await _repository.Query()
                        .Include(t => t.Customer)
                        .Include(t => t.TransactionDetails)
                            .ThenInclude(td => td.Product)
                                .ThenInclude(p => p.Category)
                        .Where(t => t.CustomerId == customerId &&
                                   t.TransactionDate >= normalizedStartDate &&
                                   t.TransactionDate <= normalizedEndDate &&
                                   t.Status == TransactionStatus.Completed)
                        .OrderByDescending(t => t.TransactionDate)
                        .AsNoTracking()
                        .ToListAsync();

                    if (transactions == null || !transactions.Any())
                    {
                        Debug.WriteLine($"No transactions found for customer {customerId} between {normalizedStartDate:d} and {normalizedEndDate:d}");
                        return new List<TransactionDTO>();
                    }

                    var dtos = _mapper.Map<IEnumerable<TransactionDTO>>(transactions);

                    // Calculate detailed information for each transaction
                    foreach (var dto in dtos)
                    {
                        if (dto.Details != null)
                        {
                            foreach (var detail in dto.Details)
                            {
                                // Ensure proper calculations
                                detail.UpdateTotal();
                            }
                        }
                    }

                    return dtos;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetByCustomerAndDateRangeAsync: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw new InvalidOperationException($"Error retrieving transactions for customer {customerId} by date range", ex);
                }
            });
        }

        public async Task<int> GetLatestTransactionIdAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var latestTransaction = await _repository.Query()
                    .OrderByDescending(t => t.TransactionId)
                    .FirstOrDefaultAsync();

                return latestTransaction?.TransactionId ?? 0;
            });
        }

        public async Task<TransactionDTO?> GetLastTransactionAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var lastTransaction = await _repository.Query()
                        .Include(t => t.Customer)
                        .Include(t => t.TransactionDetails)
                            .ThenInclude(td => td.Product)
                        .Where(t => t.Status == TransactionStatus.Completed)
                        .OrderByDescending(t => t.TransactionDate)
                        .FirstOrDefaultAsync();

                    return _mapper.Map<TransactionDTO>(lastTransaction);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error retrieving last transaction: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task<IEnumerable<TransactionDTO>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Normalize dates to start and end of day
                    var normalizedStartDate = startDate.Date;
                    var normalizedEndDate = endDate.Date.AddDays(1).AddTicks(-1);

                    // Validate date range
                    if (normalizedStartDate > normalizedEndDate)
                    {
                        throw new ArgumentException("Start date must be before or equal to end date");
                    }

                    var transactions = await _repository.Query()
                        .Include(t => t.Customer)
                        .Include(t => t.TransactionDetails)
                            .ThenInclude(td => td.Product)
                                .ThenInclude(p => p.Category)
                        .Where(t => t.TransactionDate >= normalizedStartDate &&
                                   t.TransactionDate <= normalizedEndDate &&
                                   t.Status == TransactionStatus.Completed)
                        .OrderByDescending(t => t.TransactionDate)
                        .AsNoTracking()  // Add this for better performance
                        .ToListAsync();

                    if (transactions == null || !transactions.Any())
                    {
                        Debug.WriteLine($"No transactions found between {normalizedStartDate:d} and {normalizedEndDate:d}");
                        return new List<TransactionDTO>();
                    }

                    var dtos = _mapper.Map<IEnumerable<TransactionDTO>>(transactions);

                    // Calculate detailed information for each transaction
                    foreach (var dto in dtos)
                    {
                        if (dto.Details != null)
                        {
                            foreach (var detail in dto.Details)
                            {
                                // Ensure proper calculations
                                detail.UpdateTotal();
                            }
                        }
                    }

                    return dtos;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetByDateRangeAsync: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw new InvalidOperationException("Error retrieving transactions by date range", ex);
                }
            });
        }
        public async Task<(IEnumerable<TransactionDTO> Transactions, int TotalCount)> GetByDateRangePagedAsync(
            DateTime startDate, DateTime endDate, int page, int pageSize, int? categoryId = null, int? employeeId = null)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    // Normalize dates to start and end of day
                    var normalizedStartDate = startDate.Date;
                    var normalizedEndDate = endDate.Date.AddDays(1).AddTicks(-1);

                    // Validate date range
                    if (normalizedStartDate > normalizedEndDate)
                    {
                        throw new ArgumentException("Start date must be before or equal to end date");
                    }

                    // Get base query for transactions matching the criteria
                    var query = _repository.Query()
                        .Include(t => t.Customer)
                        .Include(t => t.TransactionDetails)
                            .ThenInclude(td => td.Product)
                                .ThenInclude(p => p.Category)
                        .Where(t => t.TransactionDate >= normalizedStartDate &&
                                   t.TransactionDate <= normalizedEndDate &&
                                   t.Status == TransactionStatus.Completed)
                        .OrderByDescending(t => t.TransactionDate)
                        .AsNoTracking();  // For better performance

                    // Apply category filter if specified
                    if (categoryId.HasValue && categoryId.Value > 0)
                    {
                        query = query.Where(t => t.TransactionDetails
                            .Any(d => d.Product.CategoryId == categoryId.Value));
                    }

                    // Apply employee filter if specified
                    if (employeeId.HasValue && employeeId.Value > 0)
                    {
                        query = query.Where(t => t.CashierId == employeeId.Value.ToString());
                    }

                    // Get total count for pagination
                    int totalCount = await query.CountAsync();

                    // Apply pagination
                    var transactions = await query
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    if (transactions == null || !transactions.Any())
                    {
                        Debug.WriteLine($"No transactions found between {normalizedStartDate:d} and {normalizedEndDate:d} for page {page}");
                        return (new List<TransactionDTO>(), totalCount);
                    }

                    var dtos = _mapper.Map<IEnumerable<TransactionDTO>>(transactions);

                    // Calculate detailed information for each transaction
                    foreach (var dto in dtos)
                    {
                        if (dto.Details != null)
                        {
                            foreach (var detail in dto.Details)
                            {
                                // Ensure proper calculations
                                detail.UpdateTotal();
                            }
                        }
                    }

                    return (dtos, totalCount);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in GetByDateRangePagedAsync: {ex.Message}");
                    Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    throw new InvalidOperationException("Error retrieving paginated transactions by date range", ex);
                }
            });
        }
        public async Task<int> GetTransactionCountByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    return await _repository.Query()
                        .Where(t => t.TransactionDate.Date >= startDate.Date &&
                                   t.TransactionDate.Date <= endDate.Date &&
                                   t.Status == TransactionStatus.Completed)
                        .CountAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting transaction count: {ex.Message}");
                    throw new InvalidOperationException("Error retrieving transaction count", ex);
                }
            });
        }

        public async Task<decimal> GetTransactionSummaryByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var totalSales = await _repository.Query()
                        .Where(t => t.TransactionDate.Date >= startDate.Date &&
                                   t.TransactionDate.Date <= endDate.Date &&
                                   t.Status == TransactionStatus.Completed &&
                                   t.TransactionType == TransactionType.Sale)
                        .SumAsync(t => t.TotalAmount);

                    return totalSales;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting transaction summary: {ex.Message}");
                    throw new InvalidOperationException("Error retrieving transaction summary", ex);
                }
            });
        }

        public async Task<Dictionary<string, decimal>> GetCategorySalesByDateRangeAsync(
            DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var transactions = await _repository.Query()
                        .Include(t => t.TransactionDetails)
                            .ThenInclude(td => td.Product)
                                .ThenInclude(p => p.Category)
                        .Where(t => t.TransactionDate.Date >= startDate.Date &&
                                   t.TransactionDate.Date <= endDate.Date &&
                                   t.Status == TransactionStatus.Completed &&
                                   t.TransactionType == TransactionType.Sale)
                        .ToListAsync();

                    return transactions
                        .SelectMany(t => t.TransactionDetails)
                        .Where(td => td.Product?.Category != null)
                        .GroupBy(td => td.Product.Category.Name)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Sum(td => td.Total)
                        );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting category sales: {ex.Message}");
                    throw new InvalidOperationException("Error retrieving category sales", ex);
                }
            });
        }

        public async Task<IEnumerable<TransactionDTO>> GetByTypeAsync(TransactionType type)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _repository.Query()
                    .Include(t => t.Customer)
                    .Include(t => t.TransactionDetails)
                    .ThenInclude(td => td.Product)
                    .Where(t => t.TransactionType == type)
                    .ToListAsync();
                return _mapper.Map<IEnumerable<TransactionDTO>>(transactions);
            });
        }

        public async Task<bool> UpdateStatusAsync(int id, TransactionStatus status)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transaction = await _repository.GetByIdAsync(id);
                if (transaction == null) return false;

                transaction.Status = status;
                await _repository.UpdateAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                return true;
            });
        }

        public async Task<decimal> GetTotalSalesAsync(DateTime startDate, DateTime endDate)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                return await _repository.Query()
                    .Where(t => t.TransactionType == TransactionType.Sale
                           && t.Status == TransactionStatus.Completed
                           && t.TransactionDate >= startDate
                           && t.TransactionDate <= endDate)
                    .SumAsync(t => t.TotalAmount);
            });
        }

        public async Task<TransactionDTO> ProcessSaleAsync(TransactionDTO transactionDto)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                using var transaction = await _unitOfWork.BeginTransactionAsync();
                try
                {
                    Debug.WriteLine($"Processing sale with {transactionDto.Details.Count} products");

                    // First collect product data for all details
                    Dictionary<int, ProductDTO> productDict = new Dictionary<int, ProductDTO>();
                    foreach (var detail in transactionDto.Details)
                    {
                        var product = await _productService.GetByIdAsync(detail.ProductId);
                        if (product != null)
                        {
                            detail.PurchasePrice = product.PurchasePrice;
                            productDict[detail.ProductId] = product;
                        }
                    }

                    // Create the transaction record first
                    var entity = _mapper.Map<Transaction>(transactionDto);
                    await _repository.AddAsync(entity);

                    // Now perform stock updates
                    foreach (var detail in transactionDto.Details)
                    {
                        if (productDict.ContainsKey(detail.ProductId))
                        {
                            // Decrement stock - negative quantity because it's a sale
                            bool stockUpdated = await _productService.UpdateStockAsync(
                                detail.ProductId,
                                -detail.Quantity);

                            if (!stockUpdated)
                            {
                                Debug.WriteLine($"Warning: Could not update stock for product {detail.ProductId} - product may not exist");
                                // Continue with the transaction - don't throw an exception
                            }
                            else
                            {
                                Debug.WriteLine($"Successfully updated stock for product {detail.ProductId}");
                            }

                            // Add inventory history entry
                            await _unitOfWork.Context.Set<InventoryHistory>().AddAsync(new InventoryHistory
                            {
                                ProductId = detail.ProductId,
                                QuantityChange = -detail.Quantity,
                                Type = TransactionType.Sale.ToString(),
                                Timestamp = DateTime.Now,
                                Notes = $"Sold in transaction | Ref: Sale-{DateTime.Now:yyyyMMddHHmmss}"
                            });
                        }
                    }

                    await _unitOfWork.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Create result DTO to return
                    var resultDto = _mapper.Map<TransactionDTO>(entity);

                    // Publish events AFTER transaction is committed
                    foreach (var detail in transactionDto.Details)
                    {
                        var updatedProduct = await _productService.GetByIdAsync(detail.ProductId);
                        if (updatedProduct != null)
                        {
                            _eventAggregator.Publish(new EntityChangedEvent<ProductDTO>("Update", updatedProduct));
                        }
                    }

                    // Publish transaction created event
                    _eventAggregator.Publish(new EntityChangedEvent<TransactionDTO>(
                        "Create",
                        resultDto
                    ));

                    Debug.WriteLine($"Transaction {resultDto.TransactionId} processed successfully");
                    return resultDto;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing sale: {ex.Message}");
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public override async Task<IEnumerable<TransactionDTO>> GetAllAsync()
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _repository.Query()
                    .Include(t => t.Customer)
                    .Include(t => t.TransactionDetails)
                        .ThenInclude(td => td.Product)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<TransactionDTO>>(transactions);
            });
        }

        public override async Task<TransactionDTO?> GetByIdAsync(int id)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transaction = await _repository.Query()
                    .Include(t => t.Customer)
                    .Include(t => t.TransactionDetails)
                        .ThenInclude(td => td.Product)
                    .FirstOrDefaultAsync(t => t.TransactionId == id);

                return _mapper.Map<TransactionDTO>(transaction);
            });
        }
    }
}