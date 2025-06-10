using System;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class DrawerViewModel
    {
        private decimal _totalSales;
        private decimal _totalExpenses;
        private decimal _totalSupplierPayments;
        private decimal _netEarnings;
        private decimal _totalReturns;
        private decimal _netSales;
        private decimal _netCashflow;
        private decimal _dailySales;
        private decimal _dailyReturns;
        private decimal _dailyExpenses;
        private decimal _supplierPayments;

        private async Task LoadFinancialOverviewAsync()
        {
            try
            {
                if (CurrentDrawer == null)
                {
                    Debug.WriteLine("LoadFinancialOverviewAsync: CurrentDrawer is null");
                    return;
                }

                Debug.WriteLine($"LoadFinancialOverviewAsync: Starting calculation for Drawer {CurrentDrawer.DrawerId}");

                var todayTransactions = DrawerHistory
                    .Where(t => t.Timestamp.Date == DateTime.Today)
                    .ToList();

                Debug.WriteLine($"Found {todayTransactions.Count} transactions for today");
                foreach (var t in todayTransactions)
                {
                    Debug.WriteLine($"Transaction: Type={t.Type}, ActionType={t.ActionType}, Amount={t.Amount}, Balance={t.Balance}, Description={t.Description}");
                }

                // Track debt payments separately for debugging
                var debtPayments = todayTransactions
                    .Where(t =>
                        // Match on Type "Cash Receipt" 
                        (t.Type?.Equals("Cash Receipt", StringComparison.OrdinalIgnoreCase) == true) ||
                        // Match on ActionType "Increase" with description containing "Debt payment"
                        (t.ActionType?.Equals("Increase", StringComparison.OrdinalIgnoreCase) == true &&
                         t.Description != null && t.Description.Contains("Debt payment", StringComparison.OrdinalIgnoreCase)) ||
                        // Match on Type "Increase" with description containing "Debt payment"
                        (t.Type?.Equals("Increase", StringComparison.OrdinalIgnoreCase) == true &&
                         t.Description != null && t.Description.Contains("Debt payment", StringComparison.OrdinalIgnoreCase))
                    )
                    .ToList();

                // Log all detected debt payments
                Debug.WriteLine($"Found {debtPayments.Count} debt payment transactions");
                foreach (var payment in debtPayments)
                {
                    Debug.WriteLine($"Debt payment: Type={payment.Type}, ActionType={payment.ActionType}, Amount={payment.Amount}, Description={payment.Description}");
                }

                // Calculate regular cash sales
                var regularSales = todayTransactions
                    .Where(t => t.Type?.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) == true &&
                           t.ActionType != "Transaction Modification")
                    .Sum(t => Math.Abs(t.Amount));

                var salesModifications = todayTransactions
                    .Where(t => t.Type?.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) == true &&
                           t.ActionType == "Transaction Modification")
                    .Sum(t => t.Amount);

                // Add all debt payments to total sales
                var debtPaymentTotal = debtPayments.Sum(t => Math.Abs(t.Amount));

                // Calculate total sales
                TotalSales = regularSales + salesModifications + debtPaymentTotal;

                Debug.WriteLine($"Sales breakdown: Regular={regularSales}, Modifications={salesModifications}, DebtPayments={debtPaymentTotal}");
                Debug.WriteLine($"TotalSales calculated: {TotalSales}");

                TotalReturns = todayTransactions
                    .Where(t => t.Type?.Equals("Return", StringComparison.OrdinalIgnoreCase) == true)
                    .Sum(t => Math.Abs(t.Amount));
                Debug.WriteLine($"TotalReturns calculated: {TotalReturns}");

                // Calculate expenses with same approach
                var regularExpenses = todayTransactions
                    .Where(t => t.Type?.Equals("Expense", StringComparison.OrdinalIgnoreCase) == true &&
                           t.ActionType != "Transaction Modification")
                    .Sum(t => Math.Abs(t.Amount)) +
                    todayTransactions
                    .Where(t => t.Type?.Equals("Expense", StringComparison.OrdinalIgnoreCase) == true &&
                           t.ActionType == "Transaction Modification")
                    .Sum(t => t.Amount);
                Debug.WriteLine($"RegularExpenses calculated: {regularExpenses}");

                var salaryWithdrawals = todayTransactions
                    .Where(t => t.Type?.Equals("Salary Withdrawal", StringComparison.OrdinalIgnoreCase) == true)
                    .Sum(t => Math.Abs(t.Amount));
                Debug.WriteLine($"SalaryWithdrawals calculated: {salaryWithdrawals}");

                var supplierPayments = todayTransactions
                    .Where(t => t.Type?.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase) == true &&
                               t.ActionType != "Transaction Modification")
                    .Sum(t => Math.Abs(t.Amount)) +
                    todayTransactions
                    .Where(t => t.Type?.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase) == true &&
                               t.ActionType == "Transaction Modification")
                    .Sum(t => t.Amount);
                Debug.WriteLine($"SupplierPayments calculated: {supplierPayments}");

                // Sum all expenses
                TotalExpenses = regularExpenses + salaryWithdrawals + supplierPayments;
                Debug.WriteLine($"TotalExpenses calculated: {TotalExpenses}");

                TotalSupplierPayments = supplierPayments;
                Debug.WriteLine($"TotalSupplierPayments set: {TotalSupplierPayments}");

                // Update daily totals
                DailySales = TotalSales;
                DailyReturns = TotalReturns;
                DailyExpenses = TotalExpenses;
                SupplierPayments = TotalSupplierPayments;

                // Calculate net values
                NetSales = TotalSales - TotalReturns;
                NetCashflow = TotalSales - (TotalExpenses + TotalReturns);
                NetEarnings = NetSales - TotalExpenses;

                Debug.WriteLine($"Net calculations completed:");
                Debug.WriteLine($"NetSales: {NetSales}");
                Debug.WriteLine($"NetCashflow: {NetCashflow}");
                Debug.WriteLine($"NetEarnings: {NetEarnings}");

                // Update balance calculations
                if (CurrentDrawer != null)
                {
                    var calculatedBalance = CalculateCurrentBalance();
                    Debug.WriteLine($"Calculated balance: {calculatedBalance}");
                    Debug.WriteLine($"Current drawer balance: {CurrentDrawer.CurrentBalance}");

                    if (CurrentDrawer.CurrentBalance != calculatedBalance)
                    {
                        Debug.WriteLine($"Updating drawer balance from {CurrentDrawer.CurrentBalance} to {calculatedBalance}");
                        CurrentDrawer.CurrentBalance = calculatedBalance;
                        OnPropertyChanged(nameof(CurrentBalance));
                        OnPropertyChanged(nameof(ExpectedBalance));
                        OnPropertyChanged(nameof(Difference));
                    }
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    NotifyTotalChanges();
                });

                Debug.WriteLine("LoadFinancialOverviewAsync completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadFinancialOverviewAsync: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                await ShowErrorMessageAsync("Error updating financial summary");
            }
        }
        public async Task ApplyDateFilterAsync()
        {
            try
            {
                IsProcessing = true;

                if (StartDate > EndDate)
                {
                    await ShowErrorMessageAsync("Start date cannot be after end date");
                    return;
                }

                // Load filtered transactions
                if (CurrentDrawer != null)
                {
                    var history = await _drawerService.GetDrawerHistoryAsync(CurrentDrawer.DrawerId);
                    var filteredHistory = history.Where(t =>
                        t.Timestamp.Date >= StartDate.Date &&
                        t.Timestamp.Date <= EndDate.Date)
                        .OrderByDescending(t => t.Timestamp);

                    DrawerHistory = new ObservableCollection<DrawerTransactionDTO>(filteredHistory);

                    // Recalculate financial totals based on filtered data
                    var transactions = DrawerHistory.Where(t =>
                        t.Timestamp.Date >= StartDate.Date &&
                        t.Timestamp.Date <= EndDate.Date);

                    // Calculate totals with modification handling
                    TotalSales = transactions
                        .Where(t => t.Type.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) &&
                               t.ActionType != "Transaction Modification")
                        .Sum(t => Math.Abs(t.Amount)) +
                        transactions
                        .Where(t => t.Type.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) &&
                               t.ActionType == "Transaction Modification")
                        .Sum(t => t.Amount);

                    TotalReturns = transactions
                        .Where(t => t.Type.Equals("Return", StringComparison.OrdinalIgnoreCase))
                        .Sum(t => Math.Abs(t.Amount));

                    var regularExpenses = transactions
                        .Where(t => t.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase) &&
                               t.ActionType != "Transaction Modification")
                        .Sum(t => Math.Abs(t.Amount)) +
                        transactions
                        .Where(t => t.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase) &&
                               t.ActionType == "Transaction Modification")
                        .Sum(t => t.Amount);

                    var supplierPayments = transactions
                        .Where(t => t.Type.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase) &&
                                   t.ActionType != "Transaction Modification")
                        .Sum(t => Math.Abs(t.Amount)) +
                        transactions
                        .Where(t => t.Type.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase) &&
                                   t.ActionType == "Transaction Modification")
                        .Sum(t => t.Amount);

                    TotalExpenses = regularExpenses + supplierPayments;
                    TotalSupplierPayments = supplierPayments;

                    // Update calculated totals
                    NetSales = TotalSales - TotalReturns;
                    NetCashflow = TotalSales - (TotalExpenses + TotalReturns);
                    NetEarnings = NetSales - TotalExpenses;

                    // Update the UI
                    UpdateStatus();
                    NotifyTotalChanges();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying date filter: {ex.Message}");
                await ShowErrorMessageAsync($"Error applying date filter: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private decimal CalculateCurrentBalance()
        {
            if (DrawerHistory == null || !DrawerHistory.Any())
                return 0;

            decimal balance = 0;
            foreach (var transaction in DrawerHistory.OrderBy(t => t.Timestamp))
            {
                if (transaction.Type.Equals("Open", StringComparison.OrdinalIgnoreCase))
                {
                    balance = transaction.Amount;
                    continue;
                }

                if (transaction.ActionType == "Transaction Modification")
                {
                    // For modifications, use the actual amount with its sign
                    balance += transaction.Amount;
                    continue;
                }

                switch (transaction.Type.ToLower())
                {
                    case "cash sale":
                    case "cash in":
                    case "cash receipt":
                        balance += Math.Abs(transaction.Amount);
                        break;

                    case "expense":
                    case "supplier payment":
                    case "return":
                    case "cash out":
                    case "salary withdrawal":
                        balance -= Math.Abs(transaction.Amount);
                        break;
                }
            }

            return balance;
        }

        private void UpdateTotals()
        {
            if (DrawerHistory == null) return;

            var todayTransactions = DrawerHistory
                .Where(t => t.Timestamp.Date == DateTime.Today)
                .ToList();

            // Calculate Cash Sales: regular transactions vs. modifications
            TotalSales = todayTransactions
                .Where(t => t.Type.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) &&
                       t.ActionType != "Transaction Modification")
                .Sum(t => Math.Abs(t.Amount)) +
                todayTransactions
                .Where(t => t.Type.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) &&
                       t.ActionType == "Transaction Modification")
                .Sum(t => t.Amount);

            // Add debt payments to sales
            TotalSales += todayTransactions
                .Where(t => t.ActionType.Equals("Increase", StringComparison.OrdinalIgnoreCase) &&
                      t.Description != null &&
                      t.Description.Contains("Debt payment", StringComparison.OrdinalIgnoreCase))
                .Sum(t => Math.Abs(t.Amount));

            TotalReturns = todayTransactions
                .Where(t => t.Type.Equals("Return", StringComparison.OrdinalIgnoreCase))
                .Sum(t => Math.Abs(t.Amount));

            // Calculate expenses with same approach
            var regularExpenses = todayTransactions
                .Where(t => t.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase) &&
                       t.ActionType != "Transaction Modification")
                .Sum(t => Math.Abs(t.Amount)) +
                todayTransactions
                .Where(t => t.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase) &&
                       t.ActionType == "Transaction Modification")
                .Sum(t => t.Amount);

            var supplierPayments = todayTransactions
                .Where(t => t.Type.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase) &&
                       t.ActionType != "Transaction Modification")
                .Sum(t => Math.Abs(t.Amount)) +
                todayTransactions
                .Where(t => t.Type.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase) &&
                       t.ActionType == "Transaction Modification")
                .Sum(t => t.Amount);

            TotalExpenses = regularExpenses + supplierPayments;
            TotalSupplierPayments = supplierPayments;

            // Update daily totals
            DailySales = TotalSales;
            DailyReturns = TotalReturns;
            DailyExpenses = TotalExpenses;
            SupplierPayments = TotalSupplierPayments;

            // Calculate net values
            NetSales = TotalSales - TotalReturns;
            NetCashflow = TotalSales - (TotalExpenses + TotalReturns);
            NetEarnings = NetSales - TotalExpenses;

            NotifyTotalChanges();
        }

        private void NotifyTotalChanges()
        {
            OnPropertyChanged(nameof(TotalSales));
            OnPropertyChanged(nameof(TotalReturns));
            OnPropertyChanged(nameof(TotalExpenses));
            OnPropertyChanged(nameof(TotalSupplierPayments));
            OnPropertyChanged(nameof(DailySales));
            OnPropertyChanged(nameof(DailyReturns));
            OnPropertyChanged(nameof(DailyExpenses));
            OnPropertyChanged(nameof(SupplierPayments));
            OnPropertyChanged(nameof(NetSales));
            OnPropertyChanged(nameof(NetCashflow));
            OnPropertyChanged(nameof(NetEarnings));
            OnPropertyChanged(nameof(CurrentBalance));
            OnPropertyChanged(nameof(ExpectedBalance));
            OnPropertyChanged(nameof(Difference));
        }

        private void ResetFinancialTotals()
        {
            TotalSales = 0;
            TotalReturns = 0;
            TotalExpenses = 0;
            TotalSupplierPayments = 0;
            DailySales = 0;
            DailyReturns = 0;
            DailyExpenses = 0;
            NetSales = 0;
            NetCashflow = 0;
            NetEarnings = 0;
        }

        private async Task UpdateFinancialSummaryAsync()
        {
            try
            {
                var todayTransactions = DrawerHistory
                    .Where(t => t.Timestamp.Date == DateTime.Today)
                    .ToList();

                // Calculate base totals
                TotalSales = todayTransactions
                    .Where(t => t.Type.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => Math.Abs(t.Amount));

                TotalReturns = todayTransactions
                    .Where(t => t.Type.Equals("Return", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => Math.Abs(t.Amount));

                var regularExpenses = todayTransactions
                    .Where(t => t.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => Math.Abs(t.Amount));

                var supplierPayments = todayTransactions
                    .Where(t => t.Type.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => Math.Abs(t.Amount));

                TotalExpenses = regularExpenses + supplierPayments;
                TotalSupplierPayments = supplierPayments;

                // Update UI properties
                OnPropertyChanged(nameof(TotalSales));
                OnPropertyChanged(nameof(TotalReturns));
                OnPropertyChanged(nameof(TotalExpenses));
                OnPropertyChanged(nameof(TotalSupplierPayments));
                OnPropertyChanged(nameof(NetSales));
                OnPropertyChanged(nameof(NetCashflow));
                OnPropertyChanged(nameof(NetEarnings));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating financial summary: {ex.Message}");
                await ShowErrorMessageAsync("Error updating financial summary");
            }
        }
    }
}