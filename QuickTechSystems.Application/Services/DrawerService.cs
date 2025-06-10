using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;
using QuickTechSystems.Application.Interfaces;

namespace QuickTechSystems.Infrastructure.Services
{
    public partial class DrawerService : BaseService<Drawer, DrawerDTO>, IDrawerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IGenericRepository<Drawer> _repository;
        private readonly IEventAggregator _eventAggregator;

        public DrawerService(
       IUnitOfWork unitOfWork,
       IMapper mapper,
       IEventAggregator eventAggregator,
       IDbContextScopeService dbContextScopeService)
       : base(unitOfWork, mapper, eventAggregator, dbContextScopeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _repository = unitOfWork.Drawers;
            _eventAggregator = eventAggregator;
        }
        public async Task LogDrawerActionAsync(int drawerId, string actionType, string description)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            var transaction = new DrawerTransaction
            {
                DrawerId = drawerId,
                Type = actionType,
                ActionType = actionType,
                Description = description,
                Timestamp = DateTime.Now,
                Balance = drawer.CurrentBalance,
                Amount = 0 // The financial impact was already recorded by AddCashTransactionAsync
            };

            await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();

            // Publish an event for UI updates
            _eventAggregator.Publish(new DrawerUpdateEvent(
                actionType,
                0,
                description
            ));
        }
        // Path: QuickTechSystems.Application.Services/DrawerService.cs
        // Add this method implementation:


        public async Task<IEnumerable<DrawerDTO>> GetAllDrawerSessionsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _repository.Query().AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(d => d.OpenedAt.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(d => d.OpenedAt.Date <= endDate.Value.Date);
                }

                var sessions = await query
                    .OrderByDescending(d => d.OpenedAt)
                    .ToListAsync();

                return _mapper.Map<IEnumerable<DrawerDTO>>(sessions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting drawer sessions: {ex.Message}");
                throw;
            }
        }

        public async Task<DrawerDTO?> GetDrawerSessionByIdAsync(int drawerId)
        {
            try
            {
                var drawer = await _repository.GetByIdAsync(drawerId);
                return drawer != null ? _mapper.Map<DrawerDTO>(drawer) : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting drawer session by ID: {ex.Message}");
                return null;
            }
        }
        public async Task<bool> ProcessSupplierInvoiceAsync(decimal amount, string supplierName, string reference)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                try
                {
                    var drawer = await GetCurrentDrawerAsync();
                    if (drawer == null)
                    {
                        throw new InvalidOperationException("No active drawer found");
                    }

                    if (drawer.CurrentBalance < amount)
                    {
                        throw new InvalidOperationException("Insufficient funds in drawer");
                    }

                    // Create a drawer transaction
                    var transaction = new DrawerTransaction
                    {
                        DrawerId = drawer.DrawerId,
                        Type = "Expense",
                        ActionType = "SupplierInvoice",
                        Amount = amount,
                        Balance = drawer.CurrentBalance - amount,
                        Description = $"Supplier Invoice Payment: {supplierName}",
                        Notes = $"Reference: {reference}",
                        PaymentMethod = "Cash",
                        TransactionReference = reference,
                        Timestamp = DateTime.Now
                    };

                    // Add to the transactions
                    await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(transaction);

                    // Update drawer stats
                    drawer.CurrentBalance -= amount;
                    drawer.TotalSupplierPayments += amount;
                    drawer.LastUpdated = DateTime.Now;

                    // Save all changes
                    await _unitOfWork.SaveChangesAsync();

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing supplier invoice payment: {ex.Message}");
                    throw;
                }
            });
        }

        #region Transaction Processing Methods

        // Path: QuickTechSystems.Infrastructure/Services/DrawerService.cs
        public async Task<DrawerDTO> ProcessTransactionAsync(decimal amount, string transactionType, string description, string reference = "")
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var drawer = await _repository.Query()
                    .FirstOrDefaultAsync(d => d.Status == "Open");

                if (drawer == null)
                    throw new InvalidOperationException("No open drawer");

                // Validate sufficient funds for outgoing transactions
                if (IsOutgoingTransaction(transactionType) && amount > drawer.CurrentBalance)
                    throw new InvalidOperationException("Insufficient funds in drawer");

                var actualAmount = GetAdjustedAmount(transactionType, amount);
                var newBalance = CalculateBalance(transactionType, drawer.CurrentBalance, actualAmount);

                // Include transaction number in description if reference is provided
                string updatedDescription = description;
                if (!string.IsNullOrEmpty(reference) && !updatedDescription.Contains(reference))
                {
                    // Extract transaction number if reference is in format "Transaction #X"
                    if (reference.Contains("#"))
                    {
                        string txNumber = reference.Substring(reference.IndexOf("#"));
                        updatedDescription = $"{description} {txNumber}";
                    }
                    else
                    {
                        updatedDescription = $"{description} ({reference})";
                    }
                }

                var drawerTransaction = new DrawerTransaction
                {
                    DrawerId = drawer.DrawerId,
                    Timestamp = DateTime.Now,
                    Type = transactionType,
                    Amount = actualAmount,
                    Description = updatedDescription,
                    Balance = newBalance,
                    ActionType = transactionType,
                    TransactionReference = reference,
                    PaymentMethod = "Cash",
                    Drawer = drawer
                };

                // Update drawer totals
                await UpdateDrawerTotalsAsync(drawer, transactionType, actualAmount);
                drawer.CurrentBalance = newBalance;

                await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(drawerTransaction);
                await _repository.UpdateAsync(drawer);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                _eventAggregator.Publish(new DrawerUpdateEvent(
                    transactionType,
                    actualAmount,
                    updatedDescription
                ));

                return _mapper.Map<DrawerDTO>(drawer);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"Error processing transaction: {ex.Message}", ex);
            }
        }

        public async Task<bool> ProcessCashReceiptAsync(decimal amount, string description)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var currentDrawer = await GetCurrentDrawerAsync();
                if (currentDrawer == null)
                    throw new InvalidOperationException("No open drawer found. Please open a drawer first.");

                // Create a drawer transaction
                var drawerTransaction = new DrawerTransaction
                {
                    DrawerId = currentDrawer.DrawerId,
                    Type = "Cash Receipt",
                    Amount = amount,
                    Timestamp = DateTime.Now,
                    Description = description,
                    ActionType = "Increase",
                    PaymentMethod = "Cash"
                };

                // Update drawer balance
                var drawer = await _unitOfWork.Drawers.GetByIdAsync(currentDrawer.DrawerId);
                if (drawer != null)
                {
                    // Update financial fields
                    drawer.CurrentBalance += amount;
                    drawer.CashIn += amount;
                    drawer.TotalSales += amount; // THIS LINE WAS MISSING - Update TotalSales
                    drawer.DailySales += amount; // Also update DailySales
                    drawer.LastUpdated = DateTime.Now;

                    // Add drawer transaction
                    drawerTransaction.Balance = drawer.CurrentBalance;
                    await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(drawerTransaction);

                    // Update drawer
                    await _unitOfWork.Drawers.UpdateAsync(drawer);
                    await _unitOfWork.SaveChangesAsync();

                    // Publish drawer update event
                    _eventAggregator.Publish(new DrawerUpdateEvent("Cash Receipt", amount, description));

                    return true;
                }

                return false;
            });
        }
        public async Task<bool> UpdateDrawerTransactionForModifiedSaleAsync(int transactionId, decimal oldAmount, decimal newAmount, string description)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Find related drawer transactions
                var drawerTransactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                    .Where(dt => dt.TransactionReference == transactionId.ToString() ||
                           dt.TransactionReference == $"Transaction #{transactionId}")
                    .ToListAsync();

                if (!drawerTransactions.Any())
                {
                    Debug.WriteLine($"No drawer transactions found for transaction ID {transactionId}");
                    return false;
                }

                var drawer = await _repository.Query()
                    .FirstOrDefaultAsync(d => d.Status == "Open");

                if (drawer == null)
                {
                    Debug.WriteLine("No open drawer found");
                    return false;
                }

                // Calculate difference between old and new amount
                decimal amountDifference = newAmount - oldAmount;

                // Skip if no change in amount
                if (Math.Abs(amountDifference) < 0.01m)
                {
                    await transaction.CommitAsync();
                    return true;
                }

                // Update drawer balance
                drawer.CurrentBalance += amountDifference;

                // Update appropriate totals based on transaction type
                if (drawerTransactions.First().Type.ToLower() == "cash sale")
                {
                    drawer.TotalSales += amountDifference;
                    drawer.CashIn += amountDifference;
                }
                else if (drawerTransactions.First().Type.ToLower() == "expense" ||
                        drawerTransactions.First().Type.ToLower() == "supplier payment")
                {
                    drawer.TotalExpenses += amountDifference;
                    drawer.CashOut += amountDifference;
                }

                drawer.LastUpdated = DateTime.Now;

                // Updated description to include transaction number
                string modificationDescription = $"Modified Transaction #{transactionId}";
                if (!string.IsNullOrEmpty(description))
                {
                    // Keep custom description if provided, but ensure transaction number is included
                    if (!description.Contains($"#{transactionId}"))
                    {
                        modificationDescription = $"{description} (Transaction #{transactionId})";
                    }
                    else
                    {
                        modificationDescription = description;
                    }
                }

                // Add a modification entry to drawer transactions
                var modificationEntry = new DrawerTransaction
                {
                    DrawerId = drawer.DrawerId,
                    Timestamp = DateTime.Now,
                    Type = drawerTransactions.First().Type,
                    Amount = amountDifference,
                    Balance = drawer.CurrentBalance,
                    Description = modificationDescription,
                    ActionType = "Transaction Modification",
                    TransactionReference = $"Transaction #{transactionId} (Modified)",
                    PaymentMethod = "Cash",
                    Drawer = drawer
                };

                await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(modificationEntry);
                await _repository.UpdateAsync(drawer);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                // Publish event for UI refresh
                _eventAggregator.Publish(new DrawerUpdateEvent(
                    "Transaction Modification",
                    amountDifference,
                    modificationDescription
                ));

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating drawer transaction: {ex.Message}");
                await transaction.RollbackAsync();
                return false;
            }
        }
        private bool IsOutgoingTransaction(string transactionType)
        {
            return transactionType.ToLower() switch
            {
                "expense" or "internet expenses" or "supplier payment" or "cash out" => true,
                _ => false
            };
        }

        public async Task<DrawerDTO> ProcessExpenseAsync(decimal amount, string expenseType, string description)
        {
            if (amount <= 0)
                throw new InvalidOperationException("Amount must be greater than zero");

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var drawerEntity = await _repository.Query()
                    .FirstOrDefaultAsync(d => d.Status == "Open");

                if (drawerEntity == null)
                    throw new InvalidOperationException("No active drawer found");

                if (amount > drawerEntity.CurrentBalance)
                    throw new InvalidOperationException("Insufficient funds in drawer");

                var drawerTransaction = new DrawerTransaction
                {
                    DrawerId = drawerEntity.DrawerId,
                    Timestamp = DateTime.Now,
                    Type = "Expense",
                    Amount = -Math.Abs(amount),
                    Description = description,
                    ActionType = expenseType,
                    Balance = drawerEntity.CurrentBalance - amount
                };

                drawerEntity.CurrentBalance -= amount;
                drawerEntity.TotalExpenses += amount;
                drawerEntity.CashOut += amount;
                drawerEntity.LastUpdated = DateTime.Now;

                await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(drawerTransaction);
                await _repository.UpdateAsync(drawerEntity);
                await _unitOfWork.SaveChangesAsync();

                await transaction.CommitAsync();

                _eventAggregator.Publish(new DrawerUpdateEvent(
                    "Expense",
                    -amount,
                    description
                ));

                return _mapper.Map<DrawerDTO>(drawerEntity);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Debug.WriteLine($"Error processing expense: {ex.Message}");
                throw;
            }
        }

        public async Task<DrawerDTO> ProcessSupplierPaymentAsync(decimal amount, string supplierName, string reference)
        {
            return await ProcessTransactionAsync(amount, "Supplier Payment", $"Payment to supplier: {supplierName}", reference);
        }

        // Path: QuickTechSystems.Infrastructure/Services/DrawerService.cs
        public async Task<DrawerDTO> ProcessCashSaleAsync(decimal amount, string reference)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var drawerEntity = await _repository.Query()
                    .FirstOrDefaultAsync(d => d.Status == "Open");

                if (drawerEntity == null)
                    throw new InvalidOperationException("No open drawer found");

                // Update drawer totals
                drawerEntity.TotalSales += amount;
                drawerEntity.CashIn += amount;
                drawerEntity.CurrentBalance += amount;
                drawerEntity.LastUpdated = DateTime.Now;

                // Extract transaction number from reference
                string transactionDetails = "Cash sale transaction";
                if (!string.IsNullOrEmpty(reference))
                {
                    // If reference contains 'Transaction #' format, use it directly
                    if (reference.Contains("#"))
                    {
                        transactionDetails = $"Cash sale {reference}";
                    }
                    // Otherwise try to extract just the number
                    else
                    {
                        string transactionNumber = string.Empty;
                        if (int.TryParse(reference, out int txNumber))
                        {
                            transactionDetails = $"Cash sale transaction #{txNumber}";
                        }
                        else
                        {
                            transactionDetails = $"Cash sale transaction ({reference})";
                        }
                    }
                }

                // Create drawer transaction
                var drawerTransaction = new DrawerTransaction
                {
                    DrawerId = drawerEntity.DrawerId,
                    Timestamp = DateTime.Now,
                    Type = "Cash Sale",
                    Amount = amount,
                    Balance = drawerEntity.CurrentBalance,
                    Description = transactionDetails,
                    ActionType = "Cash Sale",
                    TransactionReference = reference,
                    PaymentMethod = "Cash",
                    Drawer = drawerEntity
                };

                // Add transaction and update drawer
                await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(drawerTransaction);
                await _repository.UpdateAsync(drawerEntity);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return _mapper.Map<DrawerDTO>(drawerEntity);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"Error processing cash sale: {ex.Message}", ex);
            }
        }
        public async Task<DrawerDTO> ProcessQuotePaymentAsync(decimal amount, string customerName, string quoteNumber)
        {
            return await ProcessTransactionAsync(amount, "Quote Payment", $"Quote payment from {customerName}", quoteNumber);
        }

        #endregion

        #region Calculation and Update Methods

        private decimal CalculateBalance(string transactionType, decimal currentBalance, decimal amount, string actionType = null)
        {
            // Special handling for transaction modifications - use signed amount directly
            if (actionType == "Transaction Modification")
            {
                return currentBalance + amount; // Keep the sign intact for modifications
            }

            // For regular transactions, use absolute values with appropriate signs
            var absAmount = Math.Abs(amount);

            return transactionType.ToLower().Trim() switch
            {
                "open" => absAmount,
                "cash sale" or "cash in" => currentBalance + absAmount,
                "expense" or "internet expenses" or "supplier payment" or "cash out" => currentBalance - absAmount,
                _ => currentBalance
            };
        }

        private async Task UpdateDrawerTotalsAsync(Drawer drawer, string transactionType, decimal amount)
        {
            var absAmount = Math.Abs(amount);

            switch (transactionType.ToLower())
            {
                case "expense":
                case "supplier payment":
                case "salary withdrawal":
                    drawer.TotalExpenses += absAmount;
                    drawer.CashOut += absAmount;
                    drawer.CurrentBalance -= absAmount;
                    break;

                case "cash sale":
                    drawer.TotalSales += absAmount;
                    drawer.CashIn += absAmount;
                    drawer.CurrentBalance += absAmount;
                    break;

                case "cash out":
                    drawer.CashOut += absAmount;
                    drawer.CurrentBalance -= absAmount;
                    break;

                case "cash in":
                    drawer.CashIn += absAmount;
                    drawer.CurrentBalance += absAmount;
                    break;
            }

            drawer.LastUpdated = DateTime.Now;
            await RecalculateNetAmountsAsync(drawer);
        }

        private async Task RecalculateNetAmountsAsync(Drawer drawer)
        {
            // Calculate Net Sales
            drawer.NetSales = drawer.TotalSales;

            // Calculate Net Cash Flow
            drawer.NetCashFlow = drawer.TotalSales - drawer.TotalExpenses;

            await _repository.UpdateAsync(drawer);
        }

        public async Task VerifyAndUpdateBalancesAsync(int drawerId)
        {
            var drawer = await _repository.Query()
                .Include(d => d.Transactions)
                .FirstOrDefaultAsync(d => d.DrawerId == drawerId);

            if (drawer == null) return;

            decimal calculatedBalance = drawer.OpeningBalance;

            // Reset all totals
            drawer.TotalSales = 0;
            drawer.TotalExpenses = 0;
            drawer.TotalSupplierPayments = 0;
            drawer.CashIn = 0;
            drawer.CashOut = 0;

            foreach (var transaction in drawer.Transactions.OrderBy(t => t.Timestamp))
            {
                calculatedBalance = CalculateBalance(transaction.Type, calculatedBalance, transaction.Amount);
                await UpdateDrawerTotalsAsync(drawer, transaction.Type, transaction.Amount);

                // Update transaction balance
                transaction.Balance = calculatedBalance;
            }

            drawer.CurrentBalance = calculatedBalance;
            await _repository.UpdateAsync(drawer);
            await _unitOfWork.SaveChangesAsync();

            _eventAggregator.Publish(new DrawerUpdateEvent(
                "Balance Verification",
                0,
                "Drawer balances verified and updated"
            ));
        }

        private async Task UpdateRunningBalancesAsync(Drawer drawer)
        {
            decimal runningBalance = drawer.OpeningBalance;
            var transactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                .Where(t => t.DrawerId == drawer.DrawerId)
                .OrderBy(t => t.Timestamp)
                .ToListAsync();

            foreach (var transaction in transactions)
            {
                runningBalance = CalculateBalance(transaction.Type, runningBalance, transaction.Amount);
                transaction.Balance = runningBalance;
            }

            drawer.CurrentBalance = runningBalance;
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task RecalculateAllTotalsAsync(int drawerId)
        {
            var drawer = await _repository.Query()
                .Include(d => d.Transactions)
                .FirstOrDefaultAsync(d => d.DrawerId == drawerId);

            if (drawer == null) return;

            // Reset all totals
            drawer.TotalSales = 0;
            drawer.TotalExpenses = 0;
            drawer.TotalSupplierPayments = 0;
            drawer.CashIn = 0;
            drawer.CashOut = 0;

            foreach (var transaction in drawer.Transactions.OrderBy(t => t.Timestamp))
            {
                var absAmount = Math.Abs(transaction.Amount);
                switch (transaction.Type.ToLower())
                {
                    case "cash sale":
                        drawer.TotalSales += absAmount;
                        drawer.CashIn += absAmount;
                        break;
                    case "expense":
                    case "internet expenses":
                        drawer.TotalExpenses += absAmount;
                        drawer.CashOut += absAmount;
                        break;
                    case "supplier payment":
                        drawer.TotalSupplierPayments += absAmount;
                        drawer.CashOut += absAmount;
                        break;
                    case "cash out":
                        drawer.CashOut += absAmount;
                        break;
                    case "cash in":
                        drawer.CashIn += absAmount;
                        break;
                }
            }

            drawer.NetSales = drawer.TotalSales;
            drawer.NetCashFlow = await CalculateNetCashflowAsync(drawer);

            await UpdateRunningBalancesAsync(drawer);
            await _repository.UpdateAsync(drawer);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<decimal> CalculateNetCashflowAsync(Drawer drawer)
        {
            var sales = Math.Abs(drawer.TotalSales);
            var expenses = Math.Abs(drawer.TotalExpenses);
            var supplierPayments = Math.Abs(drawer.TotalSupplierPayments);

            return sales - (expenses + supplierPayments);
        }

        private decimal CalculateRunningBalance(DrawerTransaction transaction)
        {
            switch (transaction.Type.ToLower())
            {
                case "open":
                    return transaction.Amount;
                case "cash sale":
                case "cash in":
                    return transaction.Amount;
                case "expense":
                case "supplier payment":
                case "cash out":
                    return -Math.Abs(transaction.Amount);
                default:
                    return 0;
            }
        }

        private void UpdateDrawerTotals(Drawer drawer, string transactionType, decimal amount)
        {
            var absAmount = Math.Abs(amount);

            switch (transactionType.ToLower())
            {
                case "expense":
                case "internet expenses":
                case "supplier payment":
                    drawer.TotalExpenses += absAmount;
                    drawer.CashOut += absAmount;
                    drawer.CurrentBalance -= absAmount;
                    break;

                case "cash sale":
                    drawer.TotalSales += absAmount;
                    drawer.CashIn += absAmount;
                    drawer.CurrentBalance += absAmount;
                    break;

                case "cash out":
                    drawer.CashOut += absAmount;
                    drawer.CurrentBalance -= absAmount;
                    break;
            }

            drawer.LastUpdated = DateTime.Now;
            RecalculateNetAmounts(drawer);
        }

        private void RecalculateNetAmounts(Drawer drawer)
        {
            drawer.NetSales = drawer.TotalSales;
            drawer.NetCashFlow = drawer.TotalSales - (drawer.TotalExpenses + drawer.TotalSupplierPayments);
        }

        public async Task RecalculateDrawerTotalsAsync(int drawerId)
        {
            var drawer = await _repository.Query()
                .Include(d => d.Transactions)
                .FirstOrDefaultAsync(d => d.DrawerId == drawerId);

            if (drawer == null) return;

            // Reset all totals
            drawer.TotalSales = 0;
            drawer.TotalExpenses = 0;
            drawer.TotalSupplierPayments = 0;
            drawer.CashIn = 0;
            drawer.CashOut = 0;

            decimal runningBalance = drawer.OpeningBalance;

            foreach (var transaction in drawer.Transactions.OrderBy(t => t.Timestamp))
            {
                var absAmount = Math.Abs(transaction.Amount);

                switch (transaction.Type.ToLower())
                {
                    case "expense":
                    case "internet expenses":
                    case "supplier payment":
                        drawer.TotalExpenses += absAmount;
                        drawer.CashOut += absAmount;
                        break;

                    case "cash sale":
                        drawer.TotalSales += absAmount;
                        drawer.CashIn += absAmount;
                        break;

                    case "cash out":
                        drawer.CashOut += absAmount;
                        break;
                }

                runningBalance = CalculateBalance(transaction.Type, runningBalance, transaction.Amount);
            }

            drawer.CurrentBalance = runningBalance;
            RecalculateNetAmounts(drawer);

            await _repository.UpdateAsync(drawer);
            await _unitOfWork.SaveChangesAsync();

            _eventAggregator.Publish(new DrawerUpdateEvent(
                "Recalculation",
                0,
                "Drawer totals recalculated"
            ));
        }

        private void UpdateTransactionTotals(Drawer drawer, string transactionType, decimal amount)
        {
            switch (transactionType.ToLower())
            {
                case "expense":
                    drawer.TotalExpenses += Math.Abs(amount);
                    break;
                case "cash sale":
                    drawer.TotalSales += Math.Abs(amount);
                    break;
            }
        }

        #endregion

        #region Drawer Lifecycle Methods

        public async Task<DrawerDTO?> GetCurrentDrawerAsync()
        {
            try
            {
                var drawerEntity = await _repository.Query()
                    .Where(d => d.Status == "Open")
                    .OrderByDescending(d => d.OpenedAt)
                    .FirstOrDefaultAsync();

                return drawerEntity != null ? _mapper.Map<DrawerDTO>(drawerEntity) : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting current drawer: {ex.Message}");
                return null;
            }
        }

        public async Task<DrawerDTO> OpenDrawerAsync(decimal openingBalance, string cashierId, string cashierName)
        {
            if (string.IsNullOrEmpty(cashierId) || string.IsNullOrEmpty(cashierName))
            {
                throw new InvalidOperationException("Cashier information is required");
            }

            var currentDrawer = await GetCurrentDrawerAsync();
            if (currentDrawer != null)
            {
                throw new InvalidOperationException("There is already an open drawer");
            }

            var drawer = new Drawer
            {
                OpeningBalance = openingBalance,
                CurrentBalance = openingBalance,
                OpenedAt = DateTime.Now,
                Status = "Open",
                CashierId = cashierId,
                CashierName = cashierName,
                CashIn = 0,
                CashOut = 0
            };

            var result = await _repository.AddAsync(drawer);
            await _unitOfWork.SaveChangesAsync();

            var openingTransaction = new DrawerTransaction
            {
                DrawerId = result.DrawerId,
                Timestamp = DateTime.Now,
                Type = "Open",
                Amount = openingBalance,
                Balance = openingBalance,
                Description = $"Drawer opened by {cashierName}",
                ActionType = "Open",
                PaymentMethod = "Cash",
                Drawer = result
            };

            await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(openingTransaction);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<DrawerDTO>(result);
        }

        public async Task<DrawerDTO> CloseDrawerAsync(decimal finalBalance, string? notes)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var drawer = await _repository.Query()
                    .Include(d => d.Transactions)
                    .FirstOrDefaultAsync(d => d.Status == "Open");

                if (drawer == null)
                {
                    throw new InvalidOperationException("No open drawer found");
                }

                // Check if there's already a closing transaction to prevent duplicates
                var hasClosingTransaction = drawer.Transactions.Any(t => t.Type == "Close");
                if (!hasClosingTransaction)
                {
                    var closingTransaction = new DrawerTransaction
                    {
                        DrawerId = drawer.DrawerId,
                        Timestamp = DateTime.Now,
                        Type = "Close",
                        Amount = finalBalance,
                        Balance = finalBalance,
                        Description = $"Drawer closed by {drawer.CashierName} with final balance of {finalBalance:C2}",
                        ActionType = "Close",
                        PaymentMethod = "Cash"
                    };

                    drawer.Transactions.Add(closingTransaction);
                }

                drawer.CurrentBalance = finalBalance;
                drawer.ClosedAt = DateTime.Now;
                drawer.Status = "Closed";
                drawer.Notes = notes;

                await _repository.UpdateAsync(drawer);
                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                var difference = finalBalance - (drawer.OpeningBalance + drawer.CashIn - drawer.CashOut);
                var description = $"Drawer closed by {drawer.CashierName} with {(difference >= 0 ? "surplus" : "shortage")} of {Math.Abs(difference):C2}";

                _eventAggregator.Publish(new DrawerUpdateEvent("Close", difference, description));

                return _mapper.Map<DrawerDTO>(drawer);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"Error closing drawer: {ex.Message}", ex);
            }
        }

        public async Task<DrawerDTO> AddCashTransactionAsync(decimal amount, bool isIn, string description)
        {
            var drawer = await _repository.Query()
                .FirstOrDefaultAsync(d => d.Status == "Open");

            if (drawer == null)
            {
                throw new InvalidOperationException("No open drawer found");
            }

            if (!isIn && amount > drawer.CurrentBalance)
            {
                throw new InvalidOperationException("Cannot remove more cash than current balance");
            }

            if (isIn)
            {
                drawer.CashIn += amount;
                drawer.CurrentBalance += amount;
            }
            else
            {
                drawer.CashOut += amount;
                drawer.CurrentBalance -= amount;
            }

            await _repository.UpdateAsync(drawer);

            var actionType = isIn ? "Cash In" : "Cash Out";
            // Use the provided description instead of a generic message
            await LogDrawerActionAsync(actionType, description, isIn ? amount : -amount, drawer.CurrentBalance);

            await _unitOfWork.SaveChangesAsync();

            // Publish an event to notify subscribers
            _eventAggregator.Publish(new DrawerUpdateEvent(
                actionType,
                isIn ? amount : -amount,
                description
            ));

            return _mapper.Map<DrawerDTO>(drawer);
        }
        public async Task<DrawerDTO> AddCashTransactionAsync(decimal amount, bool isIn)
        {
            // Call the new method with a default description
            string defaultDescription = isIn ? "Cash added to drawer" : "Cash removed from drawer";
            return await AddCashTransactionAsync(amount, isIn, defaultDescription);
        }

        #endregion

        #region Verification and Balance Methods

        public async Task VerifyCalculationsAsync(int drawerId)
        {
            var drawer = await _repository.Query()
                .Include(d => d.Transactions)
                .FirstOrDefaultAsync(d => d.DrawerId == drawerId);

            if (drawer == null) return;

            var expectedBalance = drawer.OpeningBalance + drawer.CashIn - drawer.CashOut;
            if (Math.Abs(expectedBalance - drawer.CurrentBalance) > 0.01m)
            {
                await RecalculateAllTotalsAsync(drawerId);
            }
        }

        public async Task<decimal> GetCurrentBalanceAsync()
        {
            var drawer = await GetCurrentDrawerAsync();
            return drawer?.CurrentBalance ?? 0;
        }

        public async Task<DrawerDTO> AdjustBalanceAsync(int drawerId, decimal newBalance, string reason)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null)
            {
                throw new InvalidOperationException("Drawer not found");
            }

            var adjustment = newBalance - drawer.CurrentBalance;
            drawer.CurrentBalance = newBalance;
            await _repository.UpdateAsync(drawer);
            await LogDrawerActionAsync("Balance Adjustment", reason, adjustment, newBalance);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<DrawerDTO>(drawer);
        }

        #endregion

        #region Daily Operations

        public async Task ResetDailyTotalsAsync(int drawerId)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            drawer.DailySales = 0;
            drawer.DailyExpenses = 0;
            drawer.DailySupplierPayments = 0;

            await _repository.UpdateAsync(drawer);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<(decimal Sales, decimal Expenses)> GetDailyTotalsAsync(int drawerId)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            var today = DateTime.Today;
            var transactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                .Where(t => t.DrawerId == drawerId && t.Timestamp.Date == today)
                .ToListAsync();

            var sales = transactions.Where(t => t.Type == "Cash Sale").Sum(t => Math.Abs(t.Amount));
            var expenses = transactions.Where(t => t.Type == "Expense" || t.Type == "Supplier Payment")
                .Sum(t => Math.Abs(t.Amount));

            return (sales, expenses);
        }

        public async Task UpdateDailyCalculationsAsync(int drawerId)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            var (sales, expenses) = await GetDailyTotalsAsync(drawerId);

            drawer.DailySales = sales;
            drawer.DailyExpenses = expenses;

            await _repository.UpdateAsync(drawer);
            await _unitOfWork.SaveChangesAsync();
        }

        #endregion

        #region Balance Verification

        public async Task<decimal> GetExpectedBalanceAsync(int drawerId)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            return drawer.OpeningBalance + drawer.CashIn - drawer.CashOut;
        }

        public async Task<decimal> GetActualBalanceAsync(int drawerId)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            return drawer?.CurrentBalance ?? 0;
        }

        public async Task<decimal> GetBalanceDifferenceAsync(int drawerId)
        {
            var expected = await GetExpectedBalanceAsync(drawerId);
            var actual = await GetActualBalanceAsync(drawerId);
            return actual - expected;
        }

        #endregion

        #region Audit and Security

        public async Task<IEnumerable<DrawerTransactionDTO>> GetDiscrepancyTransactionsAsync(int drawerId)
        {
            var transactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                .Where(t => t.DrawerId == drawerId)
                .OrderByDescending(t => t.Timestamp)
                .ToListAsync();

            var discrepancies = new List<DrawerTransaction>();
            decimal runningBalance = 0;

            foreach (var transaction in transactions.OrderBy(t => t.Timestamp))
            {
                runningBalance = CalculateBalance(transaction.Type, runningBalance, transaction.Amount);
                if (Math.Abs(runningBalance - transaction.Balance) > 0.01m)
                {
                    discrepancies.Add(transaction);
                }
            }

            return _mapper.Map<IEnumerable<DrawerTransactionDTO>>(discrepancies);
        }

        public async Task LogDrawerAuditAsync(int drawerId, string action, string description)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            var auditEntry = new DrawerTransaction
            {
                DrawerId = drawerId,
                Type = "Audit",
                ActionType = action,
                Description = description,
                Timestamp = DateTime.Now,
                Balance = drawer.CurrentBalance,
                Amount = 0
            };

            await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(auditEntry);
            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<bool> ValidateDrawerAccessAsync(string cashierId, int drawerId)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) return false;

            // Basic validation - check if the cashier is assigned to this drawer
            if (drawer.CashierId == cashierId) return true;

            // Log unauthorized access attempt
            await LogDrawerAuditAsync(drawerId, "Access Validation",
                $"Unauthorized access attempt by cashier {cashierId}");

            return false;
        }

        public async Task<bool> VerifyDrawerBalanceAsync(int drawerId)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) return false;

            var expectedBalance = await GetExpectedBalanceAsync(drawerId);
            var difference = drawer.CurrentBalance - expectedBalance;

            if (Math.Abs(difference) > 0.01m)
            {
                await LogDrawerAuditAsync(drawerId, "Balance Verification",
                    $"Balance discrepancy detected: {difference:C2}");
                return false;
            }

            return true;
        }

        #endregion

        #region Validation and Logging

        private decimal GetAdjustedAmount(string transactionType, decimal amount)
        {
            return transactionType.ToLower() switch
            {
                "expense" or "internet expenses" or "supplier payment" or "cash out" => -Math.Abs(amount),
                _ => Math.Abs(amount)
            };
        }

        public async Task<bool> ValidateDrawerBalanceAsync(int drawerId)
        {
            var drawer = await _repository.Query()
                .Include(d => d.Transactions)
                .FirstOrDefaultAsync(d => d.DrawerId == drawerId);

            if (drawer == null) return false;

            decimal calculatedBalance = drawer.OpeningBalance;
            foreach (var transaction in drawer.Transactions.OrderBy(t => t.Timestamp))
            {
                calculatedBalance = CalculateBalance(transaction.Type, calculatedBalance, transaction.Amount);
            }

            return Math.Abs(calculatedBalance - drawer.CurrentBalance) < 0.01m;
        }

        public async Task<bool> ValidateTransactionAsync(decimal amount, bool isCashOut = false)
        {
            if (amount <= 0) return false;

            var drawer = await GetCurrentDrawerAsync();
            if (drawer == null) return false;

            if (isCashOut)
                return drawer.CurrentBalance >= amount;

            return true;
        }

        private async Task LogDrawerActionAsync(
            string actionType,
            string description,
            decimal amount,
            decimal resultingBalance,
            string reference = "")
        {
            var currentDrawer = await GetCurrentDrawerAsync();
            if (currentDrawer == null) return;

            var transaction = new DrawerTransaction
            {
                DrawerId = currentDrawer.DrawerId,
                Timestamp = DateTime.Now,
                Type = actionType,
                Amount = amount,
                Balance = resultingBalance,
                Description = description,
                ActionType = actionType,
                TransactionReference = reference,
                PaymentMethod = "Cash"
            };

            await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();
        }

        #endregion

        #region History and Summary

        public async Task<IEnumerable<DrawerTransactionDTO>> GetDrawerHistoryAsync(int drawerId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                    .Where(dt => dt.DrawerId == drawerId)
                    .OrderBy(dt => dt.Timestamp)
                    .ToListAsync();

                var result = _mapper.Map<IEnumerable<DrawerTransactionDTO>>(transactions);

                // Ensure resulting balance is correctly set for all transactions
                decimal runningBalance = 0;
                foreach (var tx in result.OrderBy(t => t.Timestamp))
                {
                    // For "Open" transaction, set the balance directly
                    if (tx.Type.Equals("Open", StringComparison.OrdinalIgnoreCase))
                    {
                        runningBalance = tx.Amount;
                        tx.ResultingBalance = runningBalance;
                        continue;
                    }

                    // Handle all other transaction types
                    if (tx.Type.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) ||
                        tx.Type.Equals("Cash In", StringComparison.OrdinalIgnoreCase) ||
                        tx.Type.Equals("Cash Receipt", StringComparison.OrdinalIgnoreCase) ||
                        (tx.ActionType?.Equals("Increase", StringComparison.OrdinalIgnoreCase) == true))
                    {
                        runningBalance += Math.Abs(tx.Amount);
                    }
                    else
                    {
                        runningBalance -= Math.Abs(tx.Amount);
                    }

                    tx.ResultingBalance = runningBalance;
                }

                return result;
            });
        }

        public async Task<(decimal Sales, decimal SupplierPayments, decimal Expenses)>
       GetFinancialSummaryAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var transactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                    .Where(t => t.Timestamp.Date >= startDate.Date &&
                               t.Timestamp.Date <= endDate.Date &&
                               t.Drawer.Status == "Open")
                    .AsNoTracking()
                    .ToListAsync();

                var result = (
                    Sales: transactions
                        .Where(t => t.Type.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase))
                        .Sum(t => Math.Abs(t.Amount)),

                    SupplierPayments: transactions
                        .Where(t => t.Type.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase))
                        .Sum(t => Math.Abs(t.Amount)),

                    Expenses: transactions
                        .Where(t => t.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase) ||
                                   t.Type.Equals("Internet Expenses", StringComparison.OrdinalIgnoreCase) ||
                                   t.Type.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase))
                        .Sum(t => Math.Abs(t.Amount))
                );

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetFinancialSummaryAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<DrawerTransactionDTO>> GetTransactionsByTypeAsync(
            string transactionType, DateTime startDate, DateTime endDate)
        {
            var transactions = await _unitOfWork.Context.Set<DrawerHistoryEntry>()
                .Where(h => h.ActionType == transactionType &&
                           h.Timestamp >= startDate &&
                           h.Timestamp <= endDate)
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync();

            return _mapper.Map<IEnumerable<DrawerTransactionDTO>>(transactions);
        }

        public async Task<decimal> GetTotalByTransactionTypeAsync(
            string transactionType, DateTime startDate, DateTime endDate)
        {
            return await _unitOfWork.Context.Set<DrawerHistoryEntry>()
                .Where(h => h.ActionType == transactionType &&
                           h.Timestamp >= startDate &&
                           h.Timestamp <= endDate)
                .SumAsync(h => h.Amount);
        }

        #endregion
    }
}