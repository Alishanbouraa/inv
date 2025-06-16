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

        private static readonly Dictionary<string, (bool IsIncoming, bool UpdatesSales, bool UpdatesExpenses)> TransactionTypeConfig = new()
        {
            ["Open"] = (true, false, false),
            ["Cash Sale"] = (true, true, false),
            ["Cash In"] = (true, false, false),
            ["Cash Receipt"] = (true, true, false),
            ["Expense"] = (false, false, true),
            ["Internet Expenses"] = (false, false, true),
            ["Supplier Payment"] = (false, false, true),
            ["Cash Out"] = (false, false, false),
            ["Salary Withdrawal"] = (false, false, true),
            ["Return"] = (false, false, false),
            ["Quote Payment"] = (true, true, false)
        };

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

        public async Task<DrawerDTO> ProcessTransactionAsync(decimal amount, string transactionType, string description, string reference = "")
        {
            return await ExecuteWithTransactionAsync(async () =>
            {
                var drawer = await GetActiveDrawerEntityAsync();
                ValidateTransactionInternal(amount, transactionType, drawer);

                var config = GetTransactionConfig(transactionType);
                var adjustedAmount = GetAdjustedAmount(transactionType, amount);
                var newBalance = CalculateNewBalance(transactionType, drawer.CurrentBalance, adjustedAmount);

                var transaction = CreateDrawerTransaction(drawer.DrawerId, transactionType, adjustedAmount, newBalance,
                    EnhanceDescription(description, reference), reference);

                await UpdateDrawerTotalsAsync(drawer, transactionType, adjustedAmount, config);
                drawer.CurrentBalance = newBalance;

                await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(transaction);
                await _repository.UpdateAsync(drawer);
                await _unitOfWork.SaveChangesAsync();

                PublishTransactionEvent(transactionType, adjustedAmount, EnhanceDescription(description, reference));

                return _mapper.Map<DrawerDTO>(drawer);
            });
        }

        public Task<DrawerDTO> ProcessCashSaleAsync(decimal amount, string reference) =>
            ProcessTransactionAsync(amount, "Cash Sale", "Cash sale transaction", reference);

        public Task<DrawerDTO> ProcessExpenseAsync(decimal amount, string expenseType, string description) =>
            ProcessTransactionAsync(amount, "Expense", description, expenseType);

        public Task<DrawerDTO> ProcessSupplierPaymentAsync(decimal amount, string supplierName, string reference) =>
            ProcessTransactionAsync(amount, "Supplier Payment", $"Payment to supplier: {supplierName}", reference);

        public Task<DrawerDTO> ProcessQuotePaymentAsync(decimal amount, string customerName, string quoteNumber) =>
            ProcessTransactionAsync(amount, "Quote Payment", $"Quote payment from {customerName}", quoteNumber);

        public async Task<bool> ProcessCashReceiptAsync(decimal amount, string description)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var currentDrawer = await GetCurrentDrawerAsync();
                if (currentDrawer == null)
                    throw new InvalidOperationException("No open drawer found. Please open a drawer first.");

                var drawer = await _unitOfWork.Drawers.GetByIdAsync(currentDrawer.DrawerId);
                if (drawer != null)
                {
                    var transaction = CreateDrawerTransaction(drawer.DrawerId, "Cash Receipt", amount,
                        drawer.CurrentBalance + amount, description);

                    UpdateDrawerFinancials(drawer, amount, true, true, false);
                    transaction.Balance = drawer.CurrentBalance;

                    await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(transaction);
                    await _unitOfWork.Drawers.UpdateAsync(drawer);
                    await _unitOfWork.SaveChangesAsync();

                    PublishTransactionEvent("Cash Receipt", amount, description);
                    return true;
                }
                return false;
            });
        }

        public async Task<bool> ProcessSupplierInvoiceAsync(decimal amount, string supplierName, string reference)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var drawer = await GetCurrentDrawerAsync();
                if (drawer == null || drawer.CurrentBalance < amount)
                    throw new InvalidOperationException(drawer == null ? "No active drawer found" : "Insufficient funds in drawer");

                var transaction = CreateDrawerTransaction(drawer.DrawerId, "Expense", amount,
                    drawer.CurrentBalance - amount, $"Supplier Invoice Payment: {supplierName}", reference);

                var drawerEntity = await _unitOfWork.Drawers.GetByIdAsync(drawer.DrawerId);
                UpdateDrawerFinancials(drawerEntity, amount, false, false, true);

                await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(transaction);
                await _unitOfWork.SaveChangesAsync();
                return true;
            });
        }

        public async Task<bool> UpdateDrawerTransactionForModifiedSaleAsync(int transactionId, decimal oldAmount, decimal newAmount, string description)
        {
            return await ExecuteWithTransactionAsync(async () =>
            {
                var amountDifference = newAmount - oldAmount;
                if (Math.Abs(amountDifference) < 0.01m) return true;

                var drawer = await GetActiveDrawerEntityAsync();
                var drawerTransactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                    .Where(dt => dt.TransactionReference == transactionId.ToString() ||
                                dt.TransactionReference == $"Transaction #{transactionId}")
                    .ToListAsync();

                if (!drawerTransactions.Any()) return false;

                UpdateDrawerForModification(drawer, drawerTransactions.First().Type, amountDifference);

                var modificationEntry = CreateDrawerTransaction(drawer.DrawerId, drawerTransactions.First().Type,
                    amountDifference, drawer.CurrentBalance,
                    FormatModificationDescription(description, transactionId), $"Transaction #{transactionId} (Modified)");
                modificationEntry.ActionType = "Transaction Modification";

                await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(modificationEntry);
                await _repository.UpdateAsync(drawer);
                await _unitOfWork.SaveChangesAsync();

                PublishTransactionEvent("Transaction Modification", amountDifference, modificationEntry.Description);
                return true;
            });
        }

        public async Task<DrawerDTO> OpenDrawerAsync(decimal openingBalance, string cashierId, string cashierName)
        {
            if (string.IsNullOrEmpty(cashierId) || string.IsNullOrEmpty(cashierName))
                throw new InvalidOperationException("Cashier information is required");

            var currentDrawer = await GetCurrentDrawerAsync();
            if (currentDrawer != null)
                throw new InvalidOperationException("There is already an open drawer");

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

            var openingTransaction = CreateDrawerTransaction(result.DrawerId, "Open", openingBalance,
                openingBalance, $"Drawer opened by {cashierName}");
            openingTransaction.ActionType = "Open";

            await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(openingTransaction);
            await _unitOfWork.SaveChangesAsync();

            return _mapper.Map<DrawerDTO>(result);
        }

        public async Task<DrawerDTO> CloseDrawerAsync(decimal finalBalance, string? notes)
        {
            return await ExecuteWithTransactionAsync(async () =>
            {
                var drawer = await _repository.Query()
                    .Include(d => d.Transactions)
                    .FirstOrDefaultAsync(d => d.Status == "Open");

                if (drawer == null)
                    throw new InvalidOperationException("No open drawer found");

                if (!drawer.Transactions.Any(t => t.Type == "Close"))
                {
                    var closingTransaction = CreateDrawerTransaction(drawer.DrawerId, "Close", finalBalance,
                        finalBalance, $"Drawer closed by {drawer.CashierName} with final balance of {finalBalance:C2}");
                    closingTransaction.ActionType = "Close";
                    drawer.Transactions.Add(closingTransaction);
                }

                drawer.CurrentBalance = finalBalance;
                drawer.ClosedAt = DateTime.Now;
                drawer.Status = "Closed";
                drawer.Notes = notes;

                await _repository.UpdateAsync(drawer);
                await _unitOfWork.SaveChangesAsync();

                var difference = finalBalance - (drawer.OpeningBalance + drawer.CashIn - drawer.CashOut);
                PublishTransactionEvent("Close", difference,
                    $"Drawer closed by {drawer.CashierName} with {(difference >= 0 ? "surplus" : "shortage")} of {Math.Abs(difference):C2}");

                return _mapper.Map<DrawerDTO>(drawer);
            });
        }

        public async Task<DrawerDTO> AddCashTransactionAsync(decimal amount, bool isIn, string description = null)
        {
            var drawer = await GetActiveDrawerEntityAsync();
            if (!isIn && amount > drawer.CurrentBalance)
                throw new InvalidOperationException("Cannot remove more cash than current balance");

            var actionType = isIn ? "Cash In" : "Cash Out";
            var finalDescription = description ?? (isIn ? "Cash added to drawer" : "Cash removed from drawer");

            UpdateDrawerFinancials(drawer, amount, isIn, false, false);
            await _repository.UpdateAsync(drawer);
            await LogDrawerActionInternalAsync(actionType, finalDescription, isIn ? amount : -amount, drawer.CurrentBalance);
            await _unitOfWork.SaveChangesAsync();

            PublishTransactionEvent(actionType, isIn ? amount : -amount, finalDescription);
            return _mapper.Map<DrawerDTO>(drawer);
        }

        public Task<DrawerDTO> AddCashTransactionAsync(decimal amount, bool isIn) =>
            AddCashTransactionAsync(amount, isIn, null);

        public async Task<IEnumerable<DrawerTransactionDTO>> GetDrawerHistoryAsync(int drawerId)
        {
            return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
            {
                var transactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                    .Where(dt => dt.DrawerId == drawerId)
                    .OrderBy(dt => dt.Timestamp)
                    .ToListAsync();

                var result = _mapper.Map<IEnumerable<DrawerTransactionDTO>>(transactions);
                CalculateRunningBalances(result);
                return result;
            });
        }

        public async Task<(decimal Sales, decimal SupplierPayments, decimal Expenses)> GetFinancialSummaryAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                var transactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                    .Where(t => t.Timestamp.Date >= startDate.Date &&
                               t.Timestamp.Date <= endDate.Date &&
                               t.Drawer.Status == "Open")
                    .AsNoTracking()
                    .ToListAsync();

                var summary = transactions.GroupBy(t => GetSummaryCategory(t.Type))
                    .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

                return (
                    Sales: summary.GetValueOrDefault("Sales", 0),
                    SupplierPayments: summary.GetValueOrDefault("SupplierPayments", 0),
                    Expenses: summary.GetValueOrDefault("Expenses", 0)
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GetFinancialSummaryAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<DrawerDTO>> GetAllDrawerSessionsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _repository.Query();
                if (startDate.HasValue) query = query.Where(d => d.OpenedAt.Date >= startDate.Value.Date);
                if (endDate.HasValue) query = query.Where(d => d.OpenedAt.Date <= endDate.Value.Date);

                var sessions = await query.OrderByDescending(d => d.OpenedAt).ToListAsync();
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

        public async Task LogDrawerActionAsync(int drawerId, string actionType, string description)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            await LogDrawerActionInternalAsync(actionType, description, 0, drawer.CurrentBalance);
            await _unitOfWork.SaveChangesAsync();

            PublishTransactionEvent(actionType, 0, description);
        }

        public Task<decimal> GetCurrentBalanceAsync() =>
            GetCurrentDrawerAsync().ContinueWith(t => t.Result?.CurrentBalance ?? 0);

        public async Task<DrawerDTO> AdjustBalanceAsync(int drawerId, decimal newBalance, string reason)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            var adjustment = newBalance - drawer.CurrentBalance;
            drawer.CurrentBalance = newBalance;
            await _repository.UpdateAsync(drawer);
            await LogDrawerActionInternalAsync("Balance Adjustment", reason, adjustment, newBalance);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<DrawerDTO>(drawer);
        }

        public async Task RecalculateDrawerTotalsAsync(int drawerId)
        {
            var drawer = await _repository.Query()
                .Include(d => d.Transactions)
                .FirstOrDefaultAsync(d => d.DrawerId == drawerId);

            if (drawer == null) return;

            ResetDrawerTotals(drawer);
            decimal runningBalance = drawer.OpeningBalance;

            foreach (var transaction in drawer.Transactions.OrderBy(t => t.Timestamp))
            {
                var config = GetTransactionConfig(transaction.Type);
                var absAmount = Math.Abs(transaction.Amount);

                UpdateDrawerTotalsFromTransaction(drawer, transaction.Type, absAmount, config);
                runningBalance = CalculateNewBalance(transaction.Type, runningBalance, transaction.Amount);
            }

            drawer.CurrentBalance = runningBalance;
            CalculateNetAmounts(drawer);

            await _repository.UpdateAsync(drawer);
            await _unitOfWork.SaveChangesAsync();

            PublishTransactionEvent("Recalculation", 0, "Drawer totals recalculated");
        }

        public Task<IEnumerable<DrawerTransactionDTO>> GetTransactionsByTypeAsync(string transactionType, DateTime startDate, DateTime endDate) =>
            _unitOfWork.Context.Set<DrawerHistoryEntry>()
                .Where(h => h.ActionType == transactionType && h.Timestamp >= startDate && h.Timestamp <= endDate)
                .OrderByDescending(h => h.Timestamp)
                .ToListAsync()
                .ContinueWith(t => _mapper.Map<IEnumerable<DrawerTransactionDTO>>(t.Result));

        public Task<decimal> GetTotalByTransactionTypeAsync(string transactionType, DateTime startDate, DateTime endDate) =>
            _unitOfWork.Context.Set<DrawerHistoryEntry>()
                .Where(h => h.ActionType == transactionType && h.Timestamp >= startDate && h.Timestamp <= endDate)
                .SumAsync(h => h.Amount);

        public Task<bool> ValidateTransactionAsync(decimal amount, bool isCashOut = false) =>
            GetCurrentDrawerAsync().ContinueWith(t =>
                amount > 0 && t.Result != null && (!isCashOut || t.Result.CurrentBalance >= amount));

        public Task ResetDailyTotalsAsync(int drawerId) =>
            ModifyDrawerAsync(drawerId, d => { d.DailySales = d.DailyExpenses = d.DailySupplierPayments = 0; });

        public async Task<(decimal Sales, decimal Expenses)> GetDailyTotalsAsync(int drawerId)
        {
            var today = DateTime.Today;
            var transactions = await _unitOfWork.Context.Set<DrawerTransaction>()
                .Where(t => t.DrawerId == drawerId && t.Timestamp.Date == today)
                .ToListAsync();

            return (
                Sales: transactions.Where(t => t.Type == "Cash Sale").Sum(t => Math.Abs(t.Amount)),
                Expenses: transactions.Where(t => t.Type == "Expense" || t.Type == "Supplier Payment").Sum(t => Math.Abs(t.Amount))
            );
        }

        public async Task UpdateDailyCalculationsAsync(int drawerId)
        {
            var (sales, expenses) = await GetDailyTotalsAsync(drawerId);
            await ModifyDrawerAsync(drawerId, d => { d.DailySales = sales; d.DailyExpenses = expenses; });
        }

        public Task<bool> VerifyDrawerBalanceAsync(int drawerId) =>
            _repository.Query().Include(d => d.Transactions)
                .FirstOrDefaultAsync(d => d.DrawerId == drawerId)
                .ContinueWith(t => ValidateCalculatedBalance(t.Result));

        public Task<decimal> GetExpectedBalanceAsync(int drawerId) =>
            _repository.GetByIdAsync(drawerId).ContinueWith(t =>
                t.Result?.OpeningBalance + t.Result?.CashIn - t.Result?.CashOut ?? 0);

        public Task<decimal> GetActualBalanceAsync(int drawerId) =>
            _repository.GetByIdAsync(drawerId).ContinueWith(t => t.Result?.CurrentBalance ?? 0);

        public async Task<decimal> GetBalanceDifferenceAsync(int drawerId)
        {
            var expected = await GetExpectedBalanceAsync(drawerId);
            var actual = await GetActualBalanceAsync(drawerId);
            return actual - expected;
        }

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
                runningBalance = CalculateNewBalance(transaction.Type, runningBalance, transaction.Amount);
                if (Math.Abs(runningBalance - transaction.Balance) > 0.01m)
                    discrepancies.Add(transaction);
            }

            return _mapper.Map<IEnumerable<DrawerTransactionDTO>>(discrepancies);
        }

        public Task LogDrawerAuditAsync(int drawerId, string action, string description) =>
            LogDrawerActionAsync(drawerId, "Audit", $"{action}: {description}");

        public async Task<bool> ValidateDrawerAccessAsync(string cashierId, int drawerId)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer?.CashierId == cashierId) return true;

            if (drawer != null)
                await LogDrawerAuditAsync(drawerId, "Access Validation", $"Unauthorized access attempt by cashier {cashierId}");

            return false;
        }

        private async Task<T> ExecuteWithTransactionAsync<T>(Func<Task<T>> operation)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var result = await operation();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<Drawer> GetActiveDrawerEntityAsync()
        {
            var drawer = await _repository.Query().FirstOrDefaultAsync(d => d.Status == "Open");
            return drawer ?? throw new InvalidOperationException("No open drawer");
        }

        private void ValidateTransactionInternal(decimal amount, string transactionType, Drawer drawer)
        {
            if (amount <= 0) throw new InvalidOperationException("Amount must be greater than zero");

            var config = GetTransactionConfig(transactionType);
            if (!config.IsIncoming && amount > drawer.CurrentBalance)
                throw new InvalidOperationException("Insufficient funds in drawer");
        }

        private (bool IsIncoming, bool UpdatesSales, bool UpdatesExpenses) GetTransactionConfig(string transactionType) =>
            TransactionTypeConfig.TryGetValue(transactionType, out var config) ? config : (false, false, false);

        private decimal GetAdjustedAmount(string transactionType, decimal amount)
        {
            var config = GetTransactionConfig(transactionType);
            return config.IsIncoming ? Math.Abs(amount) : -Math.Abs(amount);
        }

        private decimal CalculateNewBalance(string transactionType, decimal currentBalance, decimal amount) =>
            transactionType.ToLower() switch
            {
                "open" => Math.Abs(amount),
                _ when GetTransactionConfig(transactionType).IsIncoming => currentBalance + Math.Abs(amount),
                _ => currentBalance - Math.Abs(amount)
            };

        private DrawerTransaction CreateDrawerTransaction(int drawerId, string type, decimal amount, decimal balance, string description, string reference = "")
        {
            return new DrawerTransaction
            {
                DrawerId = drawerId,
                Timestamp = DateTime.Now,
                Type = type,
                Amount = amount,
                Balance = balance,
                Description = description,
                ActionType = type,
                TransactionReference = reference,
                PaymentMethod = "Cash"
            };
        }

        private async Task UpdateDrawerTotalsAsync(Drawer drawer, string transactionType, decimal amount, (bool IsIncoming, bool UpdatesSales, bool UpdatesExpenses) config)
        {
            var absAmount = Math.Abs(amount);

            if (config.UpdatesSales)
            {
                drawer.TotalSales += absAmount;
                drawer.CashIn += absAmount;
            }
            else if (config.UpdatesExpenses)
            {
                drawer.TotalExpenses += absAmount;
                drawer.CashOut += absAmount;
            }
            else if (transactionType.ToLower() == "cash out")
            {
                drawer.CashOut += absAmount;
            }
            else if (transactionType.ToLower() == "cash in")
            {
                drawer.CashIn += absAmount;
            }

            drawer.LastUpdated = DateTime.Now;
            await CalculateNetAmountsAsync(drawer);
        }

        private void UpdateDrawerFinancials(Drawer drawer, decimal amount, bool isIncoming, bool updatesSales, bool updatesExpenses)
        {
            var absAmount = Math.Abs(amount);

            if (isIncoming)
            {
                drawer.CurrentBalance += absAmount;
                drawer.CashIn += absAmount;
                if (updatesSales) drawer.TotalSales += absAmount;
            }
            else
            {
                drawer.CurrentBalance -= absAmount;
                drawer.CashOut += absAmount;
                if (updatesExpenses) drawer.TotalExpenses += absAmount;
            }

            drawer.LastUpdated = DateTime.Now;
        }

        private void UpdateDrawerForModification(Drawer drawer, string transactionType, decimal amountDifference)
        {
            drawer.CurrentBalance += amountDifference;

            switch (transactionType.ToLower())
            {
                case "cash sale":
                    drawer.TotalSales += amountDifference;
                    drawer.CashIn += amountDifference;
                    break;
                case "expense":
                case "supplier payment":
                    drawer.TotalExpenses += amountDifference;
                    drawer.CashOut += amountDifference;
                    break;
            }

            drawer.LastUpdated = DateTime.Now;
        }

        private async Task CalculateNetAmountsAsync(Drawer drawer)
        {
            drawer.NetSales = drawer.TotalSales;
            drawer.NetCashFlow = drawer.TotalSales - drawer.TotalExpenses;
            await _repository.UpdateAsync(drawer);
        }

        private void CalculateNetAmounts(Drawer drawer)
        {
            drawer.NetSales = drawer.TotalSales;
            drawer.NetCashFlow = drawer.TotalSales - drawer.TotalExpenses;
        }

        private string EnhanceDescription(string description, string reference)
        {
            if (string.IsNullOrEmpty(reference) || description.Contains(reference)) return description;

            return reference.Contains("#")
                ? $"{description} {reference.Substring(reference.IndexOf("#"))}"
                : $"{description} ({reference})";
        }

        private string FormatModificationDescription(string description, int transactionId)
        {
            if (!string.IsNullOrEmpty(description))
                return description.Contains($"#{transactionId}") ? description : $"{description} (Transaction #{transactionId})";

            return $"Modified Transaction #{transactionId}";
        }

        private void PublishTransactionEvent(string type, decimal amount, string description) =>
            _eventAggregator.Publish(new DrawerUpdateEvent(type, amount, description));

        private async Task LogDrawerActionInternalAsync(string actionType, string description, decimal amount, decimal resultingBalance)
        {
            var currentDrawer = await GetCurrentDrawerAsync();
            if (currentDrawer == null) return;

            var transaction = CreateDrawerTransaction(currentDrawer.DrawerId, actionType, amount, resultingBalance, description);
            await _unitOfWork.Context.Set<DrawerTransaction>().AddAsync(transaction);
        }

        private void CalculateRunningBalances(IEnumerable<DrawerTransactionDTO> transactions)
        {
            decimal runningBalance = 0;
            foreach (var tx in transactions.OrderBy(t => t.Timestamp))
            {
                if (tx.Type.Equals("Open", StringComparison.OrdinalIgnoreCase))
                {
                    runningBalance = tx.Amount;
                }
                else if (GetTransactionConfig(tx.Type).IsIncoming ||
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
        }

        private string GetSummaryCategory(string transactionType) =>
            transactionType.ToLower() switch
            {
                "cash sale" => "Sales",
                "supplier payment" => "SupplierPayments",
                "expense" or "internet expenses" => "Expenses",
                _ => "Other"
            };

        private void ResetDrawerTotals(Drawer drawer)
        {
            drawer.TotalSales = drawer.TotalExpenses = drawer.TotalSupplierPayments =
            drawer.CashIn = drawer.CashOut = 0;
        }

        private void UpdateDrawerTotalsFromTransaction(Drawer drawer, string transactionType, decimal absAmount, (bool IsIncoming, bool UpdatesSales, bool UpdatesExpenses) config)
        {
            if (config.UpdatesSales) drawer.TotalSales += absAmount;
            if (config.UpdatesExpenses) drawer.TotalExpenses += absAmount;
            if (transactionType.ToLower() == "supplier payment") drawer.TotalSupplierPayments += absAmount;
        }

        private bool ValidateCalculatedBalance(Drawer drawer)
        {
            if (drawer == null) return false;

            decimal calculatedBalance = drawer.OpeningBalance;
            foreach (var transaction in drawer.Transactions.OrderBy(t => t.Timestamp))
                calculatedBalance = CalculateNewBalance(transaction.Type, calculatedBalance, transaction.Amount);

            return Math.Abs(calculatedBalance - drawer.CurrentBalance) < 0.01m;
        }

        private async Task ModifyDrawerAsync(int drawerId, Action<Drawer> modification)
        {
            var drawer = await _repository.GetByIdAsync(drawerId);
            if (drawer == null) throw new InvalidOperationException("Drawer not found");

            modification(drawer);
            await _repository.UpdateAsync(drawer);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}