using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using QuickTechSystems.WPF.Views;
using QuickTechSystems.WPF.Services;
using System.Collections.Generic;

namespace QuickTechSystems.WPF.ViewModels
{
    public class DrawerViewModel : ViewModelBase
    {
        private readonly IDrawerService _drawerService;
        private readonly IWindowService _windowService;
        private DrawerDTO? _currentDrawer;
        private ObservableCollection<DrawerTransactionDTO> _drawerHistory;
        private string _statusMessage = string.Empty, _cashDescription = string.Empty;
        private bool _isProcessing, _includeTransactionDetails = true, _includeFinancialSummary = true, _printCashierCopy = false, _isViewingHistoricalSession;
        private decimal _initialCashAmount, _cashAmount, _finalCashAmount, _totalSales, _totalExpenses, _totalSupplierPayments, _netEarnings, _totalReturns, _netSales, _netCashflow, _dailySales, _dailyReturns, _dailyExpenses, _supplierPayments;
        private ObservableCollection<DrawerSessionItem> _drawerSessions;
        private DrawerSessionItem? _selectedDrawerSession;
        private DateTime _sessionStartDate = DateTime.Today.AddDays(-30), _sessionEndDate = DateTime.Today, _startDate = DateTime.Today, _endDate = DateTime.Today, _minimumDate = DateTime.Today.AddYears(-1), _maximumDate = DateTime.Today;

        public bool IncludeTransactionDetails { get => _includeTransactionDetails; set => SetProperty(ref _includeTransactionDetails, value); }
        public bool IncludeFinancialSummary { get => _includeFinancialSummary; set => SetProperty(ref _includeFinancialSummary, value); }
        public bool PrintCashierCopy { get => _printCashierCopy; set => SetProperty(ref _printCashierCopy, value); }
        public decimal InitialCashAmount { get => _initialCashAmount; set => SetProperty(ref _initialCashAmount, value); }
        public decimal CashAmount { get => _cashAmount; set => SetProperty(ref _cashAmount, value); }
        public string CashDescription { get => _cashDescription; set => SetProperty(ref _cashDescription, value); }
        public decimal FinalCashAmount { get => _finalCashAmount; set { if (SetProperty(ref _finalCashAmount, value)) OnPropertyChanged(nameof(DrawerClosingDifference)); } }
        public ObservableCollection<DrawerSessionItem> DrawerSessions { get => _drawerSessions; set => SetProperty(ref _drawerSessions, value); }
        public DrawerSessionItem? SelectedDrawerSession { get => _selectedDrawerSession; set { if (SetProperty(ref _selectedDrawerSession, value)) _ = LoadSelectedSessionAsync(); } }
        public DateTime SessionStartDate { get => _sessionStartDate; set => SetProperty(ref _sessionStartDate, value); }
        public DateTime SessionEndDate { get => _sessionEndDate; set => SetProperty(ref _sessionEndDate, value); }
        public bool IsViewingHistoricalSession { get => _isViewingHistoricalSession; set => SetProperty(ref _isViewingHistoricalSession, value); }
        public decimal DrawerClosingDifference => CurrentDrawer?.CurrentBalance - FinalCashAmount ?? 0;
        public decimal CurrentBalance => CurrentDrawer?.CurrentBalance ?? 0;
        public decimal ExpectedBalance => CurrentDrawer?.ExpectedBalance ?? 0;
        public decimal Difference => CurrentDrawer?.Difference ?? 0;
        public DateTime StartDate { get => _startDate; set { if (SetProperty(ref _startDate, value) && EndDate < value) EndDate = value; } }
        public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }
        public DateTime MinimumDate { get => _minimumDate; set => SetProperty(ref _minimumDate, value); }
        public DateTime MaximumDate { get => _maximumDate; set => SetProperty(ref _maximumDate, value); }
        public decimal TotalSales { get => _totalSales; set => SetProperty(ref _totalSales, value); }
        public decimal TotalExpenses { get => _totalExpenses; set => SetProperty(ref _totalExpenses, value); }
        public decimal TotalSupplierPayments { get => _totalSupplierPayments; set => SetProperty(ref _totalSupplierPayments, value); }
        public decimal NetEarnings { get => _netEarnings; set => SetProperty(ref _netEarnings, value); }
        public decimal TotalReturns { get => _totalReturns; set => SetProperty(ref _totalReturns, value); }
        public decimal NetSales { get => _netSales; set => SetProperty(ref _netSales, value); }
        public decimal NetCashflow { get => _netCashflow; set => SetProperty(ref _netCashflow, value); }
        public decimal DailySales { get => _dailySales; set => SetProperty(ref _dailySales, value); }
        public decimal DailyReturns { get => _dailyReturns; set => SetProperty(ref _dailyReturns, value); }
        public decimal DailyExpenses { get => _dailyExpenses; set => SetProperty(ref _dailyExpenses, value); }
        public decimal SupplierPayments { get => _supplierPayments; set => SetProperty(ref _supplierPayments, value); }

        public DrawerDTO? CurrentDrawer
        {
            get => _currentDrawer;
            set { if (SetProperty(ref _currentDrawer, value)) new[] { nameof(IsDrawerOpen), nameof(CanOpenDrawer), nameof(CurrentBalance), nameof(ExpectedBalance), nameof(Difference) }.ToList().ForEach(OnPropertyChanged); }
        }

        public ObservableCollection<DrawerTransactionDTO> DrawerHistory { get => _drawerHistory; set => SetProperty(ref _drawerHistory, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsProcessing { get => _isProcessing; set { if (SetProperty(ref _isProcessing, value)) CommandManager.InvalidateRequerySuggested(); } }
        public bool IsDrawerOpen => CurrentDrawer?.Status == "Open";
        public bool CanOpenDrawer => CurrentDrawer?.Status != "Open";

        public ICommand OpenDrawerCommand { get; private set; }
        public ICommand AddCashCommand { get; private set; }
        public ICommand RemoveCashCommand { get; private set; }
        public ICommand CloseDrawerCommand { get; private set; }
        public ICommand PrintReportCommand { get; private set; }
        public ICommand LoadFinancialDataCommand { get; private set; }
        public ICommand ApplyDateFilterCommand { get; private set; }
        public ICommand LoadDrawerSessionsCommand { get; private set; }
        public ICommand ApplySessionFilterCommand { get; private set; }
        public ICommand ViewCurrentSessionCommand { get; private set; }

        public class DrawerSessionItem
        {
            public int DrawerId { get; set; }
            public DateTime OpenedAt { get; set; }
            public DateTime? ClosedAt { get; set; }
            public string CashierName { get; set; } = string.Empty;
            public string CashierId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string DisplayText => $"Drawer {DrawerId}. {OpenedAt:MM/dd/yyyy}, Opened By: {CashierId}, {OpenedAt:MM/dd/yyyy}, {CashierName}";
        }

        public DrawerViewModel(IDrawerService drawerService, IWindowService windowService, IEventAggregator eventAggregator) : base(eventAggregator)
        {
            (_drawerService, _windowService) = (drawerService, windowService);
            (_drawerSessions, _drawerHistory) = (new ObservableCollection<DrawerSessionItem>(), new ObservableCollection<DrawerTransactionDTO>());
            InitializeCommands();
            SubscribeToEvents();
            _ = LoadDataAsync();
        }

        private void InitializeCommands() =>
            (OpenDrawerCommand, AddCashCommand, RemoveCashCommand, CloseDrawerCommand, PrintReportCommand, LoadFinancialDataCommand, ApplyDateFilterCommand, LoadDrawerSessionsCommand, ApplySessionFilterCommand, ViewCurrentSessionCommand) =
            (new AsyncRelayCommand(async _ => await OpenDrawerAsync(), _ => CanOpenDrawer && !IsProcessing),
             new AsyncRelayCommand(async _ => await AddCashAsync(), _ => IsDrawerOpen && !IsProcessing),
             new AsyncRelayCommand(async _ => await RemoveCashAsync(), _ => IsDrawerOpen && !IsProcessing),
             new AsyncRelayCommand(async _ => await CloseDrawerAsync(), _ => IsDrawerOpen && !IsProcessing),
             new AsyncRelayCommand(async _ => await PrintReportAsync(), _ => IsDrawerOpen && !IsProcessing),
             new AsyncRelayCommand(async _ => await LoadFinancialOverviewAsync()),
             new AsyncRelayCommand(async _ => await ApplyDateFilterAsync()),
             new AsyncRelayCommand(async _ => await LoadDrawerSessionsAsync()),
             new AsyncRelayCommand(async _ => await ApplySessionFilterAsync()),
             new AsyncRelayCommand(async _ => await ViewCurrentSessionAsync()));

        protected override void SubscribeToEvents()
        {
            base.SubscribeToEvents();
            _eventAggregator.Subscribe<DrawerUpdateEvent>(async evt => await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => { try { if (evt.Type.Contains("Transaction")) { await Task.Delay(200); await RefreshDrawerDataAsync(); await LoadFinancialOverviewAsync(); UpdateStatus(); UpdateTotals(); OnPropertyChanged(nameof(DrawerHistory)); } else await RefreshDrawerDataAsync(); } catch { await ShowErrorMessageAsync("Error updating drawer display"); } }));
            _eventAggregator.Subscribe<EntityChangedEvent<TransactionDTO>>(async evt => { if (evt.Entity != null) await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => { await RefreshDrawerDataAsync(); await LoadFinancialOverviewAsync(); UpdateTotals(); }); });
        }

        protected override void UnsubscribeFromEvents() => base.UnsubscribeFromEvents();

        protected override async Task LoadDataAsync() => await ExecuteWithProcessing(async () => { await ExecuteOperationsWithRetry(new Queue<Func<Task>>(new[] { async () => CurrentDrawer = await _drawerService.GetCurrentDrawerAsync(), LoadDrawerHistoryAsync, () => { UpdateStatus(); return Task.CompletedTask; } }), 3); if (CurrentDrawer == null) DrawerHistory.Clear(); });

        public async Task RefreshDrawerDataAsync() => await ExecuteWithProcessing(async () => { CurrentDrawer = await _drawerService.GetCurrentDrawerAsync(); if (CurrentDrawer == null) { DrawerHistory.Clear(); UpdateStatus(); return; } await LoadDrawerHistoryAsync(); await LoadFinancialOverviewAsync(); new[] { nameof(CurrentBalance), nameof(ExpectedBalance), nameof(Difference) }.ToList().ForEach(OnPropertyChanged); UpdateStatus(); UpdateTotals(); });

        private async Task ExecuteOperationsWithRetry(Queue<Func<Task>> operations, int maxRetries)
        {
            for (int retry = 0; retry < maxRetries && operations.Count > 0; retry++)
            {
                try { IsProcessing = true; foreach (var operation in operations.ToList()) await operation(); operations.Clear(); return; }
                catch (Exception ex) { Debug.WriteLine($"Attempt {retry + 1} failed: {ex.Message}"); if (retry == maxRetries - 1) { await ShowErrorMessageAsync("Unable to load drawer data. Please try again."); throw; } await Task.Delay(1000 * (retry + 1)); }
                finally { IsProcessing = false; }
            }
        }

        private async Task ExecuteWithProcessing(Func<Task> operation) { try { IsProcessing = true; await operation(); } catch (Exception ex) { await ShowErrorMessageAsync($"Error: {ex.Message}"); } finally { IsProcessing = false; } }

        private async Task ExecuteDrawerOperation(string title, string prompt, Func<decimal, Task> operation, bool validateBalance = false)
        {
            try
            {
                IsProcessing = true;
                var dialog = new InputDialog(title, prompt) { Owner = _windowService.GetCurrentWindow() };
                if (dialog.ShowDialog() == true && decimal.TryParse(dialog.Input, out decimal amount))
                {
                    if (amount > 0 && (!validateBalance || amount <= (CurrentDrawer?.CurrentBalance ?? 0)))
                    {
                        await operation(amount); await LoadDrawerHistoryAsync(); UpdateStatus();
                        MessageBox.Show($"Operation completed successfully with {amount:C2}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (amount <= 0) await ShowErrorMessageAsync("Amount must be greater than zero");
                    else if (validateBalance) MessageBox.Show("Amount exceeds current balance.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex) { await ShowErrorMessageAsync($"Error in {title.ToLower()}: {ex.Message}"); }
            finally { IsProcessing = false; }
        }

        private async Task OpenDrawerAsync()
        {
            var currentUser = System.Windows.Application.Current.Properties["CurrentUser"] as EmployeeDTO;
            if (currentUser == null) { await ShowErrorMessageAsync("No user is currently logged in"); return; }
            await ExecuteDrawerOperation("Open Drawer", "Enter opening balance:", async amount => CurrentDrawer = await _drawerService.OpenDrawerAsync(amount, currentUser.EmployeeId.ToString(), $"{currentUser.FirstName} {currentUser.LastName}"));
        }

        private async Task AddCashAsync() => await ExecuteDrawerOperation("Add Cash", "Enter amount to add:", async amount => { var desc = GetUserInput("Add Cash", "Enter a description:", "Cash added to drawer"); CurrentDrawer = await _drawerService.ProcessTransactionAsync(amount, "Cash In", desc); });
        private async Task RemoveCashAsync() => await ExecuteDrawerOperation("Remove Cash", "Enter amount to remove:", async amount => { var desc = GetUserInput("Remove Cash", "Enter a reason:", "Cash removed from drawer"); CurrentDrawer = await _drawerService.ProcessTransactionAsync(amount, "Cash Out", desc); }, true);

        private async Task CloseDrawerAsync() => await ExecuteDrawerOperation("Close Drawer", "Enter final cash amount:", async finalAmount => {
            var notes = GetUserInput("Closing Notes", "Enter any notes for closing:", string.Empty);
            decimal currentBalance = CurrentDrawer?.CurrentBalance ?? 0;
            CurrentDrawer = await _drawerService.CloseDrawerAsync(finalAmount, notes);
            decimal difference = finalAmount - currentBalance;
            MessageBox.Show(difference == 0 ? "Drawer closed successfully with no discrepancy." : $"Drawer closed with a {(difference > 0 ? "surplus" : "shortage")} of {Math.Abs(difference):C2}", "Drawer Closed", MessageBoxButton.OK, difference == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });

        private string GetUserInput(string title, string prompt, string defaultValue) { var dialog = new InputDialog(title, prompt) { Owner = _windowService.GetCurrentWindow() }; return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Input) ? dialog.Input : defaultValue; }

        private async Task LoadDrawerSessionsAsync() => await ExecuteWithProcessing(async () => { var sessions = await _drawerService.GetAllDrawerSessionsAsync(SessionStartDate, SessionEndDate); var sessionItems = sessions.Select(s => new DrawerSessionItem { DrawerId = s.DrawerId, OpenedAt = s.OpenedAt, ClosedAt = s.ClosedAt, CashierName = s.CashierName, CashierId = s.CashierId, Status = s.Status }).ToList(); await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { DrawerSessions = new ObservableCollection<DrawerSessionItem>(sessionItems); var currentSession = sessionItems.FirstOrDefault(s => s.Status == "Open"); if (currentSession != null) { SelectedDrawerSession = currentSession; IsViewingHistoricalSession = false; } }); });

        private async Task ApplySessionFilterAsync() { if (SessionStartDate > SessionEndDate) { await ShowErrorMessageAsync("Start date cannot be after end date"); return; } await LoadDrawerSessionsAsync(); }

        private async Task LoadSelectedSessionAsync() { if (SelectedDrawerSession == null) return; await ExecuteWithProcessing(async () => { var drawer = await _drawerService.GetDrawerSessionByIdAsync(SelectedDrawerSession.DrawerId); if (drawer != null) { CurrentDrawer = drawer; IsViewingHistoricalSession = drawer.Status != "Open"; await LoadDrawerHistoryAsync(); await LoadFinancialOverviewAsync(); UpdateStatus(); } }); }

        private async Task ViewCurrentSessionAsync() => await ExecuteWithProcessing(async () => { CurrentDrawer = await _drawerService.GetCurrentDrawerAsync(); IsViewingHistoricalSession = false; if (CurrentDrawer != null) { var currentSessionItem = DrawerSessions?.FirstOrDefault(s => s.DrawerId == CurrentDrawer.DrawerId); if (currentSessionItem != null) SelectedDrawerSession = currentSessionItem; } await LoadDrawerHistoryAsync(); await LoadFinancialOverviewAsync(); UpdateStatus(); });

        public async Task CloseDrawerWithCurrentBalance() { if (CurrentDrawer == null) { await ShowErrorMessageAsync("No active drawer found"); return; } await ExecuteWithProcessing(async () => { CurrentDrawer = await _drawerService.CloseDrawerAsync(CurrentDrawer.CurrentBalance, string.Empty); await LoadDrawerHistoryAsync(); UpdateStatus(); }); }

        public async Task OpenDrawerWithAmount(decimal amount) { var currentUser = System.Windows.Application.Current.Properties["CurrentUser"] as EmployeeDTO; if (currentUser == null || amount < 0) { await ShowErrorMessageAsync(currentUser == null ? "No user is currently logged in" : "Amount cannot be negative"); return; } CurrentDrawer = await _drawerService.OpenDrawerAsync(amount, currentUser.EmployeeId.ToString(), $"{currentUser.FirstName} {currentUser.LastName}"); await LoadDrawerHistoryAsync(); UpdateStatus(); MessageBox.Show("Drawer opened successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information); }

        public async Task AddCashWithDetails(decimal amount, string description) => await ExecuteCashOperation(amount, "Cash In", description, "Cash added to drawer", "added");
        public async Task RemoveCashWithDetails(decimal amount, string description) => await ExecuteCashOperation(amount, "Cash Out", description, "Cash removed from drawer", "removed", true);

        private async Task ExecuteCashOperation(decimal amount, string transactionType, string description, string defaultDescription, string action, bool validateBalance = false) => await ExecuteWithProcessing(async () => { if (amount <= 0 || (validateBalance && amount > (CurrentDrawer?.CurrentBalance ?? 0))) { if (amount <= 0) await ShowErrorMessageAsync("Amount must be greater than zero"); else MessageBox.Show("Amount exceeds current balance.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning); return; } CurrentDrawer = await _drawerService.ProcessTransactionAsync(amount, transactionType, string.IsNullOrWhiteSpace(description) ? defaultDescription : description); await LoadDrawerHistoryAsync(); UpdateStatus(); MessageBox.Show($"Successfully {action} {amount:C2}", "Success", MessageBoxButton.OK, MessageBoxImage.Information); });

        public async Task CloseDrawerWithAmount(decimal finalAmount) { if (finalAmount < 0) { await ShowErrorMessageAsync("Final amount cannot be negative"); return; } decimal currentBalance = CurrentDrawer?.CurrentBalance ?? 0; await ExecuteWithProcessing(async () => { CurrentDrawer = await _drawerService.CloseDrawerAsync(finalAmount, string.Empty); await LoadDrawerHistoryAsync(); UpdateStatus(); decimal difference = finalAmount - currentBalance; MessageBox.Show(difference == 0 ? "Drawer closed successfully with no discrepancy." : $"Drawer closed with a {(difference > 0 ? "surplus" : "shortage")} of {Math.Abs(difference):C2}", "Drawer Closed", MessageBoxButton.OK, difference == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning); }); }

        public async Task PrintReportWithOptions(bool includeTransactions, bool includeFinancialSummary, bool printCashierCopy) => await ExecuteWithProcessing(async () => { if (CurrentDrawer == null) return; var printDialog = new PrintDialog(); if (printDialog.ShowDialog() == true) printDialog.PrintDocument(((IDocumentPaginatorSource)CreateDrawerReport(includeTransactions, includeFinancialSummary, printCashierCopy)).DocumentPaginator, "Drawer Report"); });

        private async Task PrintReportAsync() => await PrintReportWithOptions(true, true, false);

        private FlowDocument CreateDrawerReport(bool includeTransactions = true, bool includeFinancial = true, bool cashierCopy = false)
        {
            var document = new FlowDocument();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Bold(new Run("Drawer Report\n")) { FontSize = 18 });
            paragraph.Inlines.Add(new Run($"Generated: {DateTime.Now:g}\n\n"));
            if (cashierCopy) paragraph.Inlines.Add(new Bold(new Run("CASHIER COPY\n")) { FontSize = 14 });
            if (CurrentDrawer != null && includeFinancial) new[] { "Summary:\n", $"Opening Balance: {CurrentDrawer.OpeningBalance:C2}\n", $"Current Balance: {CurrentDrawer.CurrentBalance:C2}\n", $"Cash In: {CurrentDrawer.CashIn:C2}\n", $"Cash Out: {CurrentDrawer.CashOut:C2}\n", $"Difference: {CurrentDrawer.Difference:C2}\n\n" }.ToList().ForEach(line => paragraph.Inlines.Add(line.Contains("Summary") ? new Bold(new Run(line)) : new Run(line)));
            document.Blocks.Add(paragraph);
            if (includeTransactions && DrawerHistory.Any()) { var table = new Table(); new[] { 150.0, 100.0, 100.0, 100.0, 100.0 }.ToList().ForEach(width => table.Columns.Add(new TableColumn { Width = new GridLength(width) })); var headerRow = new TableRow(); new[] { "Timestamp", "Type", "Amount", "Balance", "Notes" }.ToList().ForEach(header => headerRow.Cells.Add(new TableCell(new Paragraph(new Run(header))))); table.RowGroups.Add(new TableRowGroup()); table.RowGroups[0].Rows.Add(headerRow); DrawerHistory.ToList().ForEach(transaction => { var row = new TableRow(); new[] { transaction.Timestamp.ToString("g"), transaction.Type, transaction.Amount.ToString("C2"), transaction.Balance.ToString("C2"), transaction.Notes ?? string.Empty }.ToList().ForEach(cell => row.Cells.Add(new TableCell(new Paragraph(new Run(cell))))); table.RowGroups[0].Rows.Add(row); }); document.Blocks.Add(table); }
            return document;
        }

        private async Task LoadFinancialOverviewAsync() { try { if (CurrentDrawer == null) return; var todayTransactions = DrawerHistory.Where(t => t.Timestamp.Date == DateTime.Today).ToList(); var calculations = CalculateFinancialTotals(todayTransactions); ApplyFinancialCalculations(calculations); if (CurrentDrawer != null) { var calculatedBalance = CalculateCurrentBalance(); if (CurrentDrawer.CurrentBalance != calculatedBalance) { CurrentDrawer.CurrentBalance = calculatedBalance; new[] { nameof(CurrentBalance), nameof(ExpectedBalance), nameof(Difference) }.ToList().ForEach(OnPropertyChanged); } } await System.Windows.Application.Current.Dispatcher.InvokeAsync(NotifyTotalChanges); } catch { await ShowErrorMessageAsync("Error updating financial summary"); } }

        private Dictionary<string, decimal> CalculateFinancialTotals(List<DrawerTransactionDTO> transactions) { var debtPayments = transactions.Where(t => (t.Type?.Equals("Cash Receipt", StringComparison.OrdinalIgnoreCase) == true) || (t.ActionType?.Equals("Increase", StringComparison.OrdinalIgnoreCase) == true && t.Description != null && t.Description.Contains("Debt payment", StringComparison.OrdinalIgnoreCase)) || (t.Type?.Equals("Increase", StringComparison.OrdinalIgnoreCase) == true && t.Description != null && t.Description.Contains("Debt payment", StringComparison.OrdinalIgnoreCase))).ToList(); return new Dictionary<string, decimal> { ["RegularSales"] = transactions.Where(t => t.Type?.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) == true && t.ActionType != "Transaction Modification").Sum(t => Math.Abs(t.Amount)), ["SalesModifications"] = transactions.Where(t => t.Type?.Equals("Cash Sale", StringComparison.OrdinalIgnoreCase) == true && t.ActionType == "Transaction Modification").Sum(t => t.Amount), ["DebtPaymentTotal"] = debtPayments.Sum(t => Math.Abs(t.Amount)), ["TotalReturns"] = transactions.Where(t => t.Type?.Equals("Return", StringComparison.OrdinalIgnoreCase) == true).Sum(t => Math.Abs(t.Amount)), ["RegularExpenses"] = transactions.Where(t => t.Type?.Equals("Expense", StringComparison.OrdinalIgnoreCase) == true && t.ActionType != "Transaction Modification").Sum(t => Math.Abs(t.Amount)) + transactions.Where(t => t.Type?.Equals("Expense", StringComparison.OrdinalIgnoreCase) == true && t.ActionType == "Transaction Modification").Sum(t => t.Amount), ["SalaryWithdrawals"] = transactions.Where(t => t.Type?.Equals("Salary Withdrawal", StringComparison.OrdinalIgnoreCase) == true).Sum(t => Math.Abs(t.Amount)), ["SupplierPayments"] = transactions.Where(t => t.Type?.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase) == true && t.ActionType != "Transaction Modification").Sum(t => Math.Abs(t.Amount)) + transactions.Where(t => t.Type?.Equals("Supplier Payment", StringComparison.OrdinalIgnoreCase) == true && t.ActionType == "Transaction Modification").Sum(t => t.Amount) }; }

        private void ApplyFinancialCalculations(Dictionary<string, decimal> calc) => (TotalSales, TotalReturns, TotalExpenses, TotalSupplierPayments, DailySales, DailyReturns, DailyExpenses, SupplierPayments, NetSales, NetCashflow, NetEarnings) = (calc["RegularSales"] + calc["SalesModifications"] + calc["DebtPaymentTotal"], calc["TotalReturns"], calc["RegularExpenses"] + calc["SalaryWithdrawals"] + calc["SupplierPayments"], calc["SupplierPayments"], TotalSales, calc["TotalReturns"], calc["RegularExpenses"] + calc["SalaryWithdrawals"] + calc["SupplierPayments"], calc["SupplierPayments"], TotalSales - calc["TotalReturns"], TotalSales - (calc["RegularExpenses"] + calc["SalaryWithdrawals"] + calc["SupplierPayments"] + calc["TotalReturns"]), TotalSales - calc["TotalReturns"] - (calc["RegularExpenses"] + calc["SalaryWithdrawals"] + calc["SupplierPayments"]));

        public async Task ApplyDateFilterAsync() { if (StartDate > EndDate) { await ShowErrorMessageAsync("Start date cannot be after end date"); return; } await ExecuteWithProcessing(async () => { if (CurrentDrawer != null) { var history = await _drawerService.GetDrawerHistoryAsync(CurrentDrawer.DrawerId); var filteredHistory = history.Where(t => t.Timestamp.Date >= StartDate.Date && t.Timestamp.Date <= EndDate.Date).OrderByDescending(t => t.Timestamp); DrawerHistory = new ObservableCollection<DrawerTransactionDTO>(filteredHistory); var calculations = CalculateFinancialTotals(DrawerHistory.ToList()); ApplyFinancialCalculations(calculations); UpdateStatus(); NotifyTotalChanges(); } }); }

        private decimal CalculateCurrentBalance() { if (DrawerHistory?.Any() != true) return 0; var balanceRules = new Dictionary<string, Func<DrawerTransactionDTO, decimal, decimal>> { ["open"] = (t, _) => t.Amount, ["cash sale"] = (t, b) => b + Math.Abs(t.Amount), ["cash in"] = (t, b) => b + Math.Abs(t.Amount), ["cash receipt"] = (t, b) => b + Math.Abs(t.Amount), ["expense"] = (t, b) => b - Math.Abs(t.Amount), ["supplier payment"] = (t, b) => b - Math.Abs(t.Amount), ["return"] = (t, b) => b - Math.Abs(t.Amount), ["cash out"] = (t, b) => b - Math.Abs(t.Amount), ["salary withdrawal"] = (t, b) => b - Math.Abs(t.Amount) }; return DrawerHistory.OrderBy(t => t.Timestamp).Aggregate(0m, (balance, transaction) => transaction.ActionType == "Transaction Modification" ? balance + transaction.Amount : balanceRules.TryGetValue(transaction.Type.ToLower(), out var rule) ? rule(transaction, balance) : balance); }

        private void UpdateTotals() { if (DrawerHistory == null) return; var todayTransactions = DrawerHistory.Where(t => t.Timestamp.Date == DateTime.Today).ToList(); var calculations = CalculateFinancialTotals(todayTransactions); ApplyFinancialCalculations(calculations); NotifyTotalChanges(); }

        private void NotifyTotalChanges() => new[] { nameof(TotalSales), nameof(TotalReturns), nameof(TotalExpenses), nameof(TotalSupplierPayments), nameof(DailySales), nameof(DailyReturns), nameof(DailyExpenses), nameof(SupplierPayments), nameof(NetSales), nameof(NetCashflow), nameof(NetEarnings), nameof(CurrentBalance), nameof(ExpectedBalance), nameof(Difference) }.ToList().ForEach(OnPropertyChanged);

        private void ResetFinancialTotals() => (TotalSales, TotalReturns, TotalExpenses, TotalSupplierPayments, DailySales, DailyReturns, DailyExpenses, NetSales, NetCashflow, NetEarnings) = (0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        private async Task LoadDrawerHistoryAsync() { if (CurrentDrawer == null) return; await ExecuteOperationsWithRetry(new Queue<Func<Task>>(new[] { async () => { var history = await _drawerService.GetDrawerHistoryAsync(CurrentDrawer.DrawerId); await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => { DrawerHistory = new ObservableCollection<DrawerTransactionDTO>(history.OrderByDescending(t => t.Timestamp)); UpdateTotals(); UpdateStatus(); }); } }), 3); }

        private void UpdateStatus() { if (CurrentDrawer == null) { StatusMessage = "No drawer is currently open"; return; } var timeInfo = CurrentDrawer.Status == "Open" ? $"opened at {CurrentDrawer.OpenedAt:t}" : $"closed at {CurrentDrawer.ClosedAt:t}"; StatusMessage = $"Drawer is {CurrentDrawer.Status.ToLower()} - {CurrentDrawer.CashierName} - {timeInfo}"; OnPropertyChanged(nameof(StatusMessage)); }
    }
}