using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Infrastructure.Data;
using System.Diagnostics;
using System.Text;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Collections.ObjectModel;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.WPF.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QuickTechSystems.WPF.Commands;

using QuickTechSystems.Application.Helpers;

namespace QuickTechSystems.WPF.ViewModels
{
    public class TransactionHistoryViewModel : ViewModelBase
    {
        private readonly ITransactionService _transactionService;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly ICategoryService _categoryService;
        private readonly IBusinessSettingsService _businessSettingsService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private TransactionDTO _selectedTransaction;
        private bool _isDisposed;
        private CancellationTokenSource _cts;

        private ObservableCollection<TransactionDTO> _transactions;
        private ObservableCollection<CategoryDTO> _categories;
        private ObservableCollection<TransactionDTO> _filteredTransactions;
        private CategoryDTO? _selectedCategory;
        private decimal _totalSales;
        private decimal _totalProfit;
        private string _searchText = string.Empty;
        private bool _isRefreshing;
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;
        private bool _isDateRangeValid = true;
        private int _totalTransactions;

        // Pagination properties
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages;
        private ObservableCollection<int> _pageNumbers;
        private List<int> _visiblePageNumbers = new List<int>();

        private Dictionary<string, decimal> _categorySales = new();

        private static TransactionHistoryViewModel _instance;
        private Action<EntityChangedEvent<TransactionDTO>> _transactionChangedHandler;

        public ObservableCollection<TransactionDTO> Transactions
        {
            get => _transactions;
            private set => SetProperty(ref _transactions, value);
        }

        public ObservableCollection<TransactionDTO> FilteredTransactions
        {
            get => _filteredTransactions;
            private set => SetProperty(ref _filteredTransactions, value);
        }

        public ObservableCollection<CategoryDTO> Categories
        {
            get => _categories;
            private set => SetProperty(ref _categories, value);
        }

        public CategoryDTO? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    _currentPage = 1; // Reset to first page when changing category
                    OnPropertyChanged(nameof(CurrentPage));
                    _ = SafeLoadDataAsync();
                }
            }
        }

        public decimal TotalSales
        {
            get => _totalSales;
            private set => SetProperty(ref _totalSales, value);
        }

        public decimal TotalProfit
        {
            get => _totalProfit;
            private set => SetProperty(ref _totalProfit, value);
        }

        public int TotalTransactions
        {
            get => _totalTransactions;
            private set => SetProperty(ref _totalTransactions, value);
        }

        public Dictionary<string, decimal> CategorySales
        {
            get => _categorySales;
            private set => SetProperty(ref _categorySales, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                Debug.WriteLine($"TransactionHistoryViewModel - StartDate changing from {_startDate:yyyy-MM-dd} to {value:yyyy-MM-dd}");
                if (SetProperty(ref _startDate, value))
                {
                    ValidateDateRange();
                    _currentPage = 1; // Reset to first page when changing date range
                    OnPropertyChanged(nameof(CurrentPage));
                    _ = SafeLoadDataAsync();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                Debug.WriteLine($"TransactionHistoryViewModel - EndDate changing from {_endDate:yyyy-MM-dd} to {value:yyyy-MM-dd}");
                if (SetProperty(ref _endDate, value))
                {
                    ValidateDateRange();
                    _currentPage = 1; // Reset to first page when changing date range
                    OnPropertyChanged(nameof(CurrentPage));
                    _ = SafeLoadDataAsync();
                }
            }
        }
        public TransactionDTO SelectedTransaction
        {
            get => _selectedTransaction;
            set => SetProperty(ref _selectedTransaction, value);
        }
        public bool IsDateRangeValid
        {
            get => _isDateRangeValid;
            private set => SetProperty(ref _isDateRangeValid, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _currentPage = 1; // Reset to first page when searching
                    OnPropertyChanged(nameof(CurrentPage));
                    ApplyFilters();
                }
            }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set => SetProperty(ref _isRefreshing, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        // Pagination properties
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (value < 1 || value > TotalPages) return;
                if (SetProperty(ref _currentPage, value))
                {
                    _ = SafeLoadDataAsync();
                    UpdateVisiblePageNumbers();
                    OnPropertyChanged(nameof(IsFirstPage));
                    OnPropertyChanged(nameof(IsLastPage));
                }
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    _currentPage = 1; // Reset to first page when changing page size
                    OnPropertyChanged(nameof(CurrentPage));
                    _ = SafeLoadDataAsync();
                }
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                if (SetProperty(ref _totalPages, value))
                {
                    UpdateVisiblePageNumbers();
                    OnPropertyChanged(nameof(IsFirstPage));
                    OnPropertyChanged(nameof(IsLastPage));
                }
            }
        }

        public ObservableCollection<int> PageNumbers
        {
            get => _pageNumbers;
            private set => SetProperty(ref _pageNumbers, value);
        }

        public List<int> VisiblePageNumbers
        {
            get => _visiblePageNumbers;
            private set => SetProperty(ref _visiblePageNumbers, value);
        }

        public bool IsFirstPage => CurrentPage <= 1;
        public bool IsLastPage => CurrentPage >= TotalPages;

        public static void ForceRefresh()
        {
            if (_instance != null)
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _instance.RefreshCommand.Execute(null);
                });
            }
        }

        public ICommand ExportCommand { get; }
        public ICommand PrintReportCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ViewTransactionDetailsCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand DeleteTransactionCommand { get; }

        // Pagination commands
        public ICommand NextPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand GoToPageCommand { get; }
        public ICommand ChangePageSizeCommand { get; }

        public ObservableCollection<int> AvailablePageSizes { get; } = new ObservableCollection<int> { 10, 25, 50, 100 };

        public TransactionHistoryViewModel(
     ITransactionService transactionService,
     ICategoryService categoryService,
     IBusinessSettingsService businessSettingsService,
     IDbContextFactory<ApplicationDbContext> dbContextFactory,
     IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _instance = this;
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _businessSettingsService = businessSettingsService ?? throw new ArgumentNullException(nameof(businessSettingsService));
            _transactions = new ObservableCollection<TransactionDTO>();
            _filteredTransactions = new ObservableCollection<TransactionDTO>();
            _categories = new ObservableCollection<CategoryDTO>();
            _transactionChangedHandler = HandleTransactionChanged;
            _cts = new CancellationTokenSource();
            _pageNumbers = new ObservableCollection<int>();

            ExportCommand = new AsyncRelayCommand(async _ => await ExportTransactionsAsync(), CanExecuteCommand);
            PrintReportCommand = new AsyncRelayCommand(async _ => await PrintTransactionReportAsync(), CanExecuteCommand);
            RefreshCommand = new AsyncRelayCommand(async _ => await RefreshDataAsync(), CanExecuteCommand);
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            DeleteTransactionCommand = new AsyncRelayCommand<TransactionDTO>(
                async transaction => await DeleteTransactionAsync(transaction),
                CanDeleteTransaction);


            ViewTransactionDetailsCommand = new AsyncRelayCommand<TransactionDTO>(
    async transaction => await ShowTransactionDetailsAsync(transaction),
    CanShowTransactionDetails);
            // Pagination commands
            NextPageCommand = new RelayCommand(_ => CurrentPage++, _ => !IsLastPage);
            PreviousPageCommand = new RelayCommand(_ => CurrentPage--, _ => !IsFirstPage);
            GoToPageCommand = new RelayCommand<int>(page => CurrentPage = page);
            ChangePageSizeCommand = new RelayCommand<int>(size => PageSize = size);

            _ = InitializeAsync();
        }

        private void UpdateVisiblePageNumbers()
        {
            var visiblePages = new List<int>();
            int startPage = Math.Max(1, CurrentPage - 2);
            int endPage = Math.Min(TotalPages, CurrentPage + 2);

            // Always show first page
            if (startPage > 1)
            {
                visiblePages.Add(1);
                if (startPage > 2) visiblePages.Add(-1); // -1 represents ellipsis
            }

            // Add current range
            for (int i = startPage; i <= endPage; i++)
            {
                visiblePages.Add(i);
            }

            // Always show last page
            if (endPage < TotalPages)
            {
                if (endPage < TotalPages - 1) visiblePages.Add(-1); // -1 represents ellipsis
                visiblePages.Add(TotalPages);
            }

            VisiblePageNumbers = visiblePages;
            OnPropertyChanged(nameof(VisiblePageNumbers));
        }

        private void ValidateDateRange()
        {
            IsDateRangeValid = StartDate <= EndDate;
            ErrorMessage = !IsDateRangeValid ? "Start date must be before or equal to end date" : string.Empty;
        }
        private async Task EnsureExchangeRateLoaded()
        {
            try
            {
                // Try to get exchange rate from business settings
                var rateSetting = await _businessSettingsService.GetByKeyAsync("ExchangeRate");
                if (rateSetting != null && decimal.TryParse(rateSetting.Value, out decimal rate) && rate > 0)
                {
                    CurrencyHelper.UpdateExchangeRate(rate);
                    Debug.WriteLine($"Exchange rate loaded: {rate}");
                }
                else
                {
                    // Use a default value as fallback
                    CurrencyHelper.UpdateExchangeRate(100000m);
                    Debug.WriteLine("Using default exchange rate: 100000");
                }

                // Force a small calculation to verify
                decimal test = CurrencyHelper.ConvertToLBP(1);
                Debug.WriteLine($"Verification: 1 USD = {test} LBP");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading exchange rate: {ex.Message}");
                // Ensure a fallback value is set
                CurrencyHelper.UpdateExchangeRate(100000m);
            }
        }
        private async Task InitializeAsync()
        {
            try
            {
                await LoadCategoriesAsync();
                await SafeLoadDataAsync();
            }
            catch (Exception ex)
            {
                HandleError("Initialization error", ex);
            }
        }

        private async Task ShowTransactionDetailsAsync(TransactionDTO transaction)
        {
            if (transaction == null) return;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Set the selected transaction
                SelectedTransaction = transaction;

                // Load transaction details if they're not already loaded
                if (transaction.Details == null || !transaction.Details.Any())
                {
                    var loadedTransaction = await _transactionService.GetByIdAsync(transaction.TransactionId);
                    if (loadedTransaction != null)
                    {
                        transaction.Details = loadedTransaction.Details;
                        // Update the selected transaction with loaded details
                        SelectedTransaction = transaction;
                    }
                }

                // Show the transaction details popup
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var popup = new QuickTechSystems.Views.TransactionDetailsPopup();
                    popup.DataContext = SelectedTransaction;

                    // Create a container for the popup in fullscreen
                    var overlayWindow = new Window
                    {
                        Title = $"Transaction #{transaction.TransactionId} Details",
                        Content = popup,
                        WindowState = WindowState.Maximized,
                        WindowStyle = WindowStyle.None, // Remove window chrome for fullscreen
                        ResizeMode = ResizeMode.NoResize,
                        ShowInTaskbar = false
                    };

                    // Close the window with Escape key
                    overlayWindow.KeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Escape)
                            overlayWindow.Close();
                    };

                    overlayWindow.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                HandleError("Error showing transaction details", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }
        private bool CanDeleteTransaction(TransactionDTO? transaction)
        {
            return transaction != null && !IsLoading && !IsRefreshing;
        }

        // Add this method to handle refresh requests with proper DbContext scope
        public static async Task SafeRefreshAsync()
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var instance = _instance;
                    if (instance == null) return;

                    // Wait a short delay to allow any ongoing operations to complete
                    await Task.Delay(1000);

                    // Execute refresh on UI thread
                    // Use RefreshCommand instead of directly calling methods to respect
                    // internal locking mechanisms already in the ViewModel
                    if (instance.RefreshCommand.CanExecute(null))
                    {
                        instance.RefreshCommand.Execute(null);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error refreshing transaction history: {ex.Message}");
                }
            });
        }
        private async Task DeleteTransactionAsync(TransactionDTO? transaction)
        {
            if (transaction == null) return;

            if (!await _operationLock.WaitAsync(0))
            {
                await ShowErrorMessageAsync("Another operation is in progress. Please try again in a moment.");
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Confirm deletion
                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show(
                        GetOwnerWindow(),
                        $"Are you sure you want to delete transaction #{transaction.TransactionId}?\nThis action cannot be undone.",
                        "Confirm Deletion",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) == MessageBoxResult.Yes;
                });

                if (!result)
                {
                    return;
                }

                // Delete transaction
                bool success = await _transactionService.DeleteAsync(transaction.TransactionId);

                if (success)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Remove from collections
                        if (Transactions.Contains(transaction))
                            Transactions.Remove(transaction);

                        if (FilteredTransactions.Contains(transaction))
                            FilteredTransactions.Remove(transaction);

                        // Show success message
                        MessageBox.Show(
                            GetOwnerWindow(),
                            $"Transaction #{transaction.TransactionId} has been deleted successfully.",
                            "Transaction Deleted",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });

                    // Refresh data to ensure pagination is correct and update totals for entire date range
                    await SafeLoadDataAsync();
                }
                else
                {
                    await ShowErrorMessageAsync($"Failed to delete transaction #{transaction.TransactionId}");
                }
            }
            catch (Exception ex)
            {
                HandleError("Error deleting transaction", ex);
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }
        private async Task LoadCategoriesAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadCategoriesAsync skipped - operation in progress");
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                try
                {
                    using var context = await _dbContextFactory.CreateDbContextAsync();
                    var categories = await context.Set<Category>()
                        .Where(c => c.IsActive)
                        .Select(c => new CategoryDTO
                        {
                            CategoryId = c.CategoryId,
                            Name = c.Name,
                            Description = c.Description,
                            IsActive = c.IsActive
                        })
                        .ToListAsync();

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Categories = new ObservableCollection<CategoryDTO>(
                            new[] { new CategoryDTO { CategoryId = 0, Name = "All Categories" } }
                            .Concat(categories)
                        );
                        SelectedCategory = Categories.First();
                    });
                }
                catch (Exception ex)
                {
                    HandleError("Error loading categories", ex);
                }
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task SafeLoadDataAsync()
        {
            if (!IsDateRangeValid || !await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("SafeLoadDataAsync skipped - invalid date range or operation in progress");
                return;
            }

            // Create a new CancellationTokenSource for this operation
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                try
                {
                    // Add a timeout for the operation
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                    // Get the category ID filter
                    int? categoryId = SelectedCategory?.CategoryId > 0 ? SelectedCategory.CategoryId : null;

                    // Retrieve paginated transactions
                    var (transactions, totalCount) = await _transactionService.GetByDateRangePagedAsync(
                        StartDate, EndDate, CurrentPage, PageSize, categoryId);

                    if (linkedCts.Token.IsCancellationRequested) return;

                    // Get summary data for the entire date range (not just current page)
                    var summary = await _transactionService.GetTransactionSummaryByDateRangeAsync(StartDate, EndDate);
                    if (linkedCts.Token.IsCancellationRequested) return;

                    var categorySales = await _transactionService.GetCategorySalesByDateRangeAsync(StartDate, EndDate);
                    if (linkedCts.Token.IsCancellationRequested) return;

                    // Calculate profit for the entire date range
                    var totalProfit = await _transactionService.GetTransactionProfitByDateRangeAsync(
                        StartDate, EndDate, categoryId);
                    if (linkedCts.Token.IsCancellationRequested) return;

                    // Calculate total pages
                    int calculatedTotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!linkedCts.Token.IsCancellationRequested)
                        {
                            Transactions = new ObservableCollection<TransactionDTO>(transactions);
                            TotalTransactions = totalCount;
                            TotalPages = calculatedTotalPages;
                            TotalSales = summary;
                            TotalProfit = totalProfit;
                            CategorySales = new Dictionary<string, decimal>(categorySales);

                            ApplyFilters();
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation was canceled");
                }
                catch (Exception ex)
                {
                    HandleError("Error loading transactions", ex);
                }
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private void ApplyFilters()
        {
            try
            {
                var filtered = Transactions.AsEnumerable();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filtered = filtered.Where(t =>
                        (t.CustomerName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        t.TransactionId.ToString().Contains(SearchText) ||
                        (t.Details?.Any(d => d.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                        t.CashierName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                }

                FilteredTransactions = new ObservableCollection<TransactionDTO>(filtered);

                // Don't recalculate totals - we're using the full date range totals
                // CalculateTotals();
            }
            catch (Exception ex)
            {
                HandleError("Error applying filters", ex);
            }
        }

        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedCategory = Categories.First();
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
            CurrentPage = 1;
            ApplyFilters();
        }

        // This method is kept for reference but no longer used for main calculations
        private void CalculateTotals()
        {
            try
            {
                var filteredSales = FilteredTransactions.Sum(t => t.TotalAmount);
                var filteredProfit = FilteredTransactions.Sum(t =>
                {
                    // Skip if transaction has no details
                    if (t.Details == null || !t.Details.Any())
                        return 0;

                    // Skip profit calculation if total amount is zero
                    if (t.TotalAmount == 0)
                        return 0;

                    // Calculate total purchase cost
                    decimal purchaseCost = t.Details.Sum(d => d.PurchasePrice * d.Quantity);

                    // Use transaction total amount (already includes discounts) instead of summing details
                    return t.TotalAmount - purchaseCost;
                });

                // Only update if we're supposed to show filtered values instead of overall totals
                // Uncomment these if you want to show filtered values instead
                // TotalSales = filteredSales;
                // TotalProfit = filteredProfit;
            }
            catch (Exception ex)
            {
                HandleError("Error calculating totals", ex);
            }
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsRefreshing = true;
                await SafeLoadDataAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private bool CanExecuteCommand(object? parameter)
        {
            return !IsLoading && !IsRefreshing &&
                   FilteredTransactions.Any() &&
                   IsDateRangeValid;
        }

        private bool CanShowTransactionDetails(object? parameter)
        {
            return parameter is TransactionDTO;
        }

        private Window GetOwnerWindow()
        {
            // Try to get the active window first
            var activeWindow = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (activeWindow != null)
                return activeWindow;

            // Fall back to the main window
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.IsLoaded)
                return mainWindow;

            // Last resort, get any window that's visible
            return System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible)
                   ?? System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault();
        }

        // Prevent excessive event handling with throttling
        private DateTime _lastTransactionChangedTime = DateTime.MinValue;
        private object _eventLock = new object();

        // From TransactionHistoryViewModel.cs
        private async void HandleTransactionChanged(EntityChangedEvent<TransactionDTO> evt)
        {
            // Skip if loading or refreshing
            if (IsLoading || IsRefreshing) return;

            // Throttle events
            lock (_eventLock)
            {
                var now = DateTime.Now;
                if ((now - _lastTransactionChangedTime).TotalMilliseconds < 500)
                {
                    return; // Ignore events that come too quickly
                }
                _lastTransactionChangedTime = now;
            }

            try
            {
                await Task.Delay(200); // Small delay to group multiple quick changes
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await SafeLoadDataAsync();
                });
            }
            catch (Exception ex)
            {
                HandleError("Error handling transaction change", ex);
            }
        }

        private void HandleError(string message, Exception ex)
        {
            Debug.WriteLine($"{message}: {ex}");

            // Handle specific database errors
            if (ex.Message.Contains("A second operation was started") ||
                (ex.InnerException != null && ex.InnerException.Message.Contains("A second operation was started")))
            {
                ErrorMessage = "The system is processing another request. Please try again in a moment.";
            }
            else if (ex.Message.Contains("The connection was closed") ||
                    (ex.InnerException != null && ex.InnerException.Message.Contains("The connection was closed")))
            {
                ErrorMessage = "Database connection lost. Please check your connection and try again.";
            }
            else
            {
                ErrorMessage = $"{message}: {ex.Message}";
            }

            // Clear error after delay
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ErrorMessage.Contains(ex.Message))
                    {
                        ErrorMessage = string.Empty;
                    }
                });
            });

            // If this is a critical error that shows a dialog, ensure it has a proper owner
            if (message.Contains("critical", StringComparison.OrdinalIgnoreCase))
            {
                var ownerWindow = GetOwnerWindow();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        ownerWindow,
                        $"{message}: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
        }

        protected override void SubscribeToEvents()
        {
            _eventAggregator.Subscribe<EntityChangedEvent<TransactionDTO>>(_transactionChangedHandler);
        }

        protected override void UnsubscribeFromEvents()
        {
            if (!_isDisposed)
            {
                _eventAggregator.Unsubscribe<EntityChangedEvent<TransactionDTO>>(_transactionChangedHandler);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _cts?.Cancel();
                    _cts?.Dispose();
                    _operationLock.Dispose();
                    UnsubscribeFromEvents();
                }
                _isDisposed = true;
            }
        }

        private async Task ExportTransactionsAsync()
        {
            if (!FilteredTransactions.Any())
            {
                await ShowErrorMessageAsync("No transactions to export");
                return;
            }

            if (!await _operationLock.WaitAsync(0))
            {
                await ShowErrorMessageAsync("Export already in progress");
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"Transaction_History_{DateTime.Now:yyyyMMdd}"
                };

                // Use InvokeAsync to show the dialog on the UI thread
                bool? result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    saveFileDialog.ShowDialog());

                if (result == true)
                {
                    var csv = new StringBuilder();
                    csv.AppendLine("Transaction ID,Date,Customer,Type,Items,Total Amount,Profit,Status,Cashier,Category");

                    foreach (var transaction in FilteredTransactions)
                    {
                        var profit = transaction.Details?.Sum(d =>
                            (d.UnitPrice - d.PurchasePrice) * d.Quantity) ?? 0;

                        var itemCount = transaction.Details?.Count ?? 0;
                        var categories = string.Join(";", transaction.Details?
                            .Select(d => d.CategoryId)
                            .Distinct()
                            .Select(id => Categories.FirstOrDefault(c => c.CategoryId == id)?.Name ?? "Unknown")
                            ?? Array.Empty<string>());

                        csv.AppendLine($"{transaction.TransactionId}," +
                            $"\"{transaction.TransactionDate:g}\"," +
                            $"\"{transaction.CustomerName}\"," +
                            $"{transaction.TransactionType}," +
                            $"{itemCount}," +
                            $"{transaction.TotalAmount:F2}," +
                            $"{profit:F2}," +
                            $"{transaction.Status}," +
                            $"\"{transaction.CashierName}\"," +
                            $"\"{categories}\"");

                        // Add detailed items if present
                        if (transaction.Details != null)
                        {
                            foreach (var detail in transaction.Details)
                            {
                                csv.AppendLine($"," +
                                    $"," +
                                    $"," +
                                    $"Item Detail," +
                                    $"\"{detail.ProductName}\"," +
                                    $"{detail.Quantity}," +
                                    $"{detail.UnitPrice:F2}," +
                                    $"{detail.Total:F2}");
                            }
                        }
                    }

                    await File.WriteAllTextAsync(saveFileDialog.FileName, csv.ToString());
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var ownerWindow = GetOwnerWindow();
                        MessageBox.Show(
                            ownerWindow,
                            "Transactions exported successfully.",
                            "Export Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                HandleError("Error exporting transactions", ex);
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task PrintTransactionReportAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                await ShowErrorMessageAsync("Print operation already in progress");
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                // Debug: Log the current date range being used
                Debug.WriteLine($"PrintTransactionReportAsync - Using date range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}");

                // Validate date range before proceeding
                if (!IsDateRangeValid)
                {
                    await ShowErrorMessageAsync("Please select a valid date range before printing.");
                    return;
                }

                // Ensure exchange rate is loaded
                await EnsureExchangeRateLoaded();

                // Get the current filter settings
                int? categoryId = SelectedCategory?.CategoryId > 0 ? SelectedCategory.CategoryId : null;

                Debug.WriteLine($"PrintTransactionReportAsync - Category filter: {(categoryId.HasValue ? $"ID {categoryId.Value}" : "All categories")}");
                Debug.WriteLine($"PrintTransactionReportAsync - Search filter: '{SearchText}'");

                // Get all transactions for the selected date range - not just the current page
                var allTransactionsInRange = await _transactionService.GetByDateRangeAsync(StartDate, EndDate);

                Debug.WriteLine($"PrintTransactionReportAsync - Found {allTransactionsInRange.Count()} transactions in date range");

                // Apply category filter if needed
                if (categoryId.HasValue && categoryId.Value > 0)
                {
                    allTransactionsInRange = allTransactionsInRange.Where(t =>
                        t.Details?.Any(d => d.CategoryId == categoryId.Value) == true).ToList();

                    Debug.WriteLine($"PrintTransactionReportAsync - After category filter: {allTransactionsInRange.Count()} transactions");
                }

                // Apply search filter if needed
                var reportTransactions = allTransactionsInRange.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    reportTransactions = reportTransactions.Where(t =>
                        (t.CustomerName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        t.TransactionId.ToString().Contains(SearchText) ||
                        (t.Details?.Any(d => d.ProductName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                        t.CashierName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                }

                // Convert to list for reporting
                var transactionsForReport = reportTransactions.ToList();

                Debug.WriteLine($"PrintTransactionReportAsync - Final transactions for report: {transactionsForReport.Count}");

                if (!transactionsForReport.Any())
                {
                    await ShowErrorMessageAsync($"No transactions found for the selected criteria.\nDate range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}\nTotal transactions in range: {allTransactionsInRange.Count()}");
                    return;
                }

                // Get the owner window
                var ownerWindow = GetOwnerWindow();

                // Create print dialog
                var printDialog = new PrintDialog();

                if (await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => printDialog.ShowDialog() == true))
                {
                    var document = new FlowDocument
                    {
                        PagePadding = new Thickness(20),
                        FontFamily = new FontFamily("Arial"),
                        PageWidth = 280,
                        ColumnWidth = 280
                    };

                    // Report Header
                    var reportHeader = new Paragraph(new Run("Transaction Summary Report"))
                    {
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    document.Blocks.Add(reportHeader);

                    // Date Range - Use the actual dates from the view model
                    var dateRange = new Paragraph
                    {
                        Margin = new Thickness(0, 0, 0, 10),
                        FontSize = 10,
                        TextAlignment = TextAlignment.Center
                    };
                    dateRange.Inlines.Add(new Run($"Period: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}"));
                    document.Blocks.Add(dateRange);

                    // Filter Information if any filters are applied
                    if (!string.IsNullOrWhiteSpace(SearchText) || (categoryId.HasValue && categoryId.Value > 0))
                    {
                        var filterInfo = new Paragraph
                        {
                            Margin = new Thickness(0, 0, 0, 10),
                            FontSize = 9,
                            TextAlignment = TextAlignment.Center
                        };

                        var filterText = "Filters applied: ";
                        if (!string.IsNullOrWhiteSpace(SearchText))
                        {
                            filterText += $"Search: \"{SearchText}\"";
                        }

                        if (categoryId.HasValue && categoryId.Value > 0)
                        {
                            if (filterText.Length > "Filters applied: ".Length)
                                filterText += ", ";
                            filterText += $"Category: {SelectedCategory?.Name}";
                        }

                        filterInfo.Inlines.Add(new Run(filterText));
                        document.Blocks.Add(filterInfo);
                    }

                    // Transaction Count
                    var countInfo = new Paragraph
                    {
                        Margin = new Thickness(0, 0, 0, 10),
                        FontSize = 9,
                        TextAlignment = TextAlignment.Center
                    };
                    countInfo.Inlines.Add(new Run($"Showing {transactionsForReport.Count} transactions"));
                    document.Blocks.Add(countInfo);

                    // Currency Notice
                    var currencyNotice = new Paragraph(new Run("All amounts in US Dollars (USD)"))
                    {
                        FontSize = 10,
                        FontWeight = FontWeights.Normal,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 10),
                        Foreground = Brushes.DarkGreen
                    };
                    document.Blocks.Add(currencyNotice);

                    // Split transactions into discounted and non-discounted
                    var discountedTransactions = transactionsForReport.Where(t =>
                        t.Details?.Any(d => d.Discount > 0) == true).ToList();

                    var nonDiscountedTransactions = transactionsForReport.Where(t =>
                        t.Details?.All(d => d.Discount <= 0) == true || t.Details == null).ToList();

                    // Calculate totals for different transaction types
                    decimal discountedSalesTotal = discountedTransactions.Sum(t => t.TotalAmount);
                    decimal nonDiscountedSalesTotal = nonDiscountedTransactions.Sum(t => t.TotalAmount);
                    decimal totalSalesAmount = transactionsForReport.Sum(t => t.TotalAmount);

                    // Calculate total discount amount
                    decimal totalDiscountAmount = discountedTransactions
                        .SelectMany(t => t.Details ?? Enumerable.Empty<TransactionDetailDTO>())
                        .Sum(d => d.Discount);

                    // Calculate LBP values for totals only
                    decimal lbpTotalSales = CurrencyHelper.ConvertToLBP(totalSalesAmount);

                    // Add a section for non-discounted transactions
                    if (nonDiscountedTransactions.Any())
                    {
                        var nonDiscountedSection = new Section() { Margin = new Thickness(0, 0, 0, 15) };

                        var nonDiscountedHeader = new Paragraph(new Bold(new Run("REGULAR PRICE TRANSACTIONS")))
                        {
                            FontSize = 12,
                            Background = Brushes.LightGray,
                            Padding = new Thickness(5),
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        nonDiscountedSection.Blocks.Add(nonDiscountedHeader);

                        // Add summary for non-discounted with bold values
                        var nonDiscountedSummary = new Paragraph
                        {
                            Margin = new Thickness(0, 0, 0, 10),
                            TextAlignment = TextAlignment.Center
                        };

                        // Count with bold text
                        nonDiscountedSummary.Inlines.Add(new Run("Count: ") { FontSize = 10 });
                        nonDiscountedSummary.Inlines.Add(new Bold(new Run($"{nonDiscountedTransactions.Count}")
                        {
                            FontSize = 12,
                            Foreground = Brushes.DarkBlue
                        }));

                        nonDiscountedSummary.Inlines.Add(new LineBreak());

                        // Sales with bold text in USD
                        nonDiscountedSummary.Inlines.Add(new Run("Sales: ") { FontSize = 10 });
                        nonDiscountedSummary.Inlines.Add(new Bold(new Run($"${nonDiscountedSalesTotal:N2}")
                        {
                            FontSize = 12,
                            Foreground = Brushes.DarkGreen
                        }));

                        nonDiscountedSection.Blocks.Add(nonDiscountedSummary);

                        // Create a table for non-discounted transactions by product
                        CreateProductTableUSD(nonDiscountedSection, nonDiscountedTransactions);

                        document.Blocks.Add(nonDiscountedSection);
                    }

                    // Add a section for discounted transactions
                    if (discountedTransactions.Any())
                    {
                        var discountedSection = new Section() { Margin = new Thickness(0, 0, 0, 15) };

                        var discountedHeader = new Paragraph(new Bold(new Run("DISCOUNTED TRANSACTIONS")))
                        {
                            FontSize = 12,
                            Background = Brushes.LightGray,
                            Padding = new Thickness(5),
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        discountedSection.Blocks.Add(discountedHeader);

                        // Add summary for discounted with bold values
                        var discountedSummary = new Paragraph
                        {
                            Margin = new Thickness(0, 0, 0, 10),
                            TextAlignment = TextAlignment.Center
                        };

                        // Count with bold text
                        discountedSummary.Inlines.Add(new Run("Count: ") { FontSize = 10 });
                        discountedSummary.Inlines.Add(new Bold(new Run($"{discountedTransactions.Count}")
                        {
                            FontSize = 12,
                            Foreground = Brushes.DarkBlue
                        }));

                        discountedSummary.Inlines.Add(new LineBreak());

                        // Sales with bold text in USD
                        discountedSummary.Inlines.Add(new Run("Sales: ") { FontSize = 10 });
                        discountedSummary.Inlines.Add(new Bold(new Run($"${discountedSalesTotal:N2}")
                        {
                            FontSize = 12,
                            Foreground = Brushes.DarkGreen
                        }));

                        discountedSummary.Inlines.Add(new LineBreak());

                        // Discount amount in USD
                        discountedSummary.Inlines.Add(new Run($"Total Discount: ${totalDiscountAmount:N2}")
                        {
                            FontSize = 10,
                            Foreground = Brushes.Crimson
                        });

                        discountedSection.Blocks.Add(discountedSummary);

                        // Create a table for discounted transactions by product
                        CreateProductTableUSD(discountedSection, discountedTransactions);

                        document.Blocks.Add(discountedSection);
                    }

                    // TOTAL SALES SECTION - Show both USD and LBP
                    var totalSalesSection = new Section() { Margin = new Thickness(0, 10, 0, 15) };

                    // Add a divider
                    totalSalesSection.Blocks.Add(new Paragraph(new Run(""))
                    {
                        BorderBrush = Brushes.DarkGray,
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        Margin = new Thickness(0, 0, 0, 10)
                    });

                    // Total sales paragraph - USD and LBP
                    var totalSalesParagraph = new Paragraph
                    {
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 5, 0, 5),
                    };

                    totalSalesParagraph.Inlines.Add(new Bold(new Run("TOTAL SALES: ")
                    {
                        FontSize = 12,
                        Foreground = Brushes.Black
                    }));

                    totalSalesParagraph.Inlines.Add(new Bold(new Run($"${totalSalesAmount:N2}")
                    {
                        FontSize = 12,
                        Foreground = Brushes.DarkGreen
                    }));

                    totalSalesParagraph.Inlines.Add(new LineBreak());

                    totalSalesParagraph.Inlines.Add(new Bold(new Run("TOTAL SALES (LBP): ")
                    {
                        FontSize = 12,
                        Foreground = Brushes.Black
                    }));

                    totalSalesParagraph.Inlines.Add(new Bold(new Run($"{lbpTotalSales:N0} LBP")
                    {
                        FontSize = 12,
                        Foreground = Brushes.DarkGreen
                    }));

                    totalSalesSection.Blocks.Add(totalSalesParagraph);
                    document.Blocks.Add(totalSalesSection);

                    // Footer
                    var footer = new Paragraph(new Run($"Generated: {DateTime.Now:g}"))
                    {
                        FontStyle = FontStyles.Italic,
                        TextAlignment = TextAlignment.Right,
                        Margin = new Thickness(0, 10, 0, 0),
                        FontSize = 8
                    };
                    document.Blocks.Add(footer);

                    // Print the document
                    printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,
                        "Transaction Summary Report");

                    Debug.WriteLine("PrintTransactionReportAsync - Print completed successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PrintTransactionReportAsync - Error: {ex.Message}");
                HandleError("Error printing report", ex);
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }
        // Helper method to create product summary tables in USD


        // Helper method to create product summary tables in USD
        private void CreateProductTableUSD(Section section, List<TransactionDTO> transactions)
        {
            // Create a flattened list of transaction details
            var transactionItems = new List<(string ProductName, int Quantity, decimal FinalUnitPrice,
                decimal Total, decimal DiscountAmount)>();

            foreach (var transaction in transactions)
            {
                if (transaction.Details == null) continue;

                foreach (var detail in transaction.Details)
                {
                    // Skip items with zero or negative quantity
                    if (detail.Quantity <= 0) continue;

                    // Calculate the actual unit price after discount
                    decimal actualUnitPrice = detail.Total / detail.Quantity;

                    transactionItems.Add((
                        detail.ProductName,
                        (int)detail.Quantity,
                        actualUnitPrice,
                        detail.Total,
                        detail.Discount
                    ));
                }
            }

            // Group the flattened items by product name
            var groupedProducts = transactionItems
                .GroupBy(item => item.ProductName)
                .Select(g => new
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(item => item.Quantity),
                    AverageUnitPrice = g.Sum(item => item.Quantity) > 0
                        ? g.Sum(item => item.Total) / g.Sum(item => item.Quantity)
                        : 0m,
                    TotalAmount = g.Sum(item => item.Total),
                    TotalDiscount = g.Sum(item => item.DiscountAmount)
                })
                .Where(g => g.TotalQuantity > 0) // Only include products with positive quantities
                .OrderByDescending(g => g.TotalQuantity)
                .ToList();

            // If no valid products, add a message and return
            if (!groupedProducts.Any())
            {
                var noDataParagraph = new Paragraph(new Run("No product data available for this period."))
                {
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 10),
                    FontStyle = FontStyles.Italic
                };
                section.Blocks.Add(noDataParagraph);
                return;
            }

            var table = new Table { CellSpacing = 0 };

            // Define columns with proportional widths
            table.Columns.Add(new TableColumn { Width = new GridLength(2.5, GridUnitType.Star) }); // Product
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) }); // Qty
            table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) }); // Total (USD)

            // Add header row
            var tableHeaderRow = new TableRow { Background = Brushes.LightGray };
            tableHeaderRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Product"))) { FontSize = 9 }));
            tableHeaderRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Qty")))
            { FontSize = 9, TextAlignment = TextAlignment.Center }));
            tableHeaderRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Total (USD)")))
            { FontSize = 9, TextAlignment = TextAlignment.Right }));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(tableHeaderRow);

            // Add grouped product rows
            foreach (var product in groupedProducts)
            {
                var row = new TableRow();

                // Product name cell
                var nameCell = new TableCell();
                var nameParagraph = new Paragraph { FontSize = 9 };
                nameParagraph.Inlines.Add(new Run(product.ProductName ?? "Unknown"));
                nameCell.Blocks.Add(nameParagraph);
                row.Cells.Add(nameCell);

                // Quantity cell
                var qtyCell = new TableCell();
                var qtyParagraph = new Paragraph { FontSize = 9, TextAlignment = TextAlignment.Center };
                qtyParagraph.Inlines.Add(new Run(product.TotalQuantity.ToString()));
                qtyCell.Blocks.Add(qtyParagraph);
                row.Cells.Add(qtyCell);

                // Total amount cell - USD only
                var totalCell = new TableCell();
                var totalParagraph = new Paragraph { FontSize = 9, TextAlignment = TextAlignment.Right };
                totalParagraph.Inlines.Add(new Run($"${product.TotalAmount:N2}"));
                totalCell.Blocks.Add(totalParagraph);
                row.Cells.Add(totalCell);

                rowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(rowGroup);
            section.Blocks.Add(table);
        }

        // Helper method to create product summary tables in LBP
        private void CreateProductTable(Section section, List<TransactionDTO> transactions)
        {
            // Create a flattened list of transaction details
            var transactionItems = new List<(string ProductName, int Quantity, decimal FinalUnitPrice,
                decimal Total, decimal ProfitPerUnit, decimal DiscountAmount)>();

            foreach (var transaction in transactions)
            {
                if (transaction.Details == null) continue;

                foreach (var detail in transaction.Details)
                {
                    // Skip items with zero or negative quantity
                    if (detail.Quantity <= 0) continue;

                    // Calculate the actual unit price after discount
                    decimal actualUnitPrice = detail.Total / detail.Quantity;

                    // Calculate profit per unit (after discount)
                    decimal profitPerUnit = actualUnitPrice - detail.PurchasePrice;

                    transactionItems.Add((
                        detail.ProductName,
                        (int)detail.Quantity,
                        actualUnitPrice,
                        detail.Total,
                        profitPerUnit,
                        detail.Discount
                    ));
                }
            }

            // Group the flattened items by product name
            var groupedProducts = transactionItems
                .GroupBy(item => item.ProductName)
                .Select(g => new
                {
                    ProductName = g.Key,
                    TotalQuantity = g.Sum(item => item.Quantity),
                    AverageUnitPrice = g.Sum(item => item.Quantity) > 0
                        ? g.Sum(item => item.Total) / g.Sum(item => item.Quantity)
                        : 0m,
                    TotalAmount = g.Sum(item => item.Total),
                    TotalProfit = g.Sum(item => item.Quantity * item.ProfitPerUnit),
                    TotalDiscount = g.Sum(item => item.DiscountAmount)
                })
                .Where(g => g.TotalQuantity > 0) // Only include products with positive quantities
                .OrderByDescending(g => g.TotalQuantity)
                .ToList();

            // If no valid products, add a message and return
            if (!groupedProducts.Any())
            {
                var noDataParagraph = new Paragraph(new Run("No product data available for this period."))
                {
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 10),
                    FontStyle = FontStyles.Italic
                };
                section.Blocks.Add(noDataParagraph);
                return;
            }

            var table = new Table { CellSpacing = 0 };

            // Define columns with proportional widths
            table.Columns.Add(new TableColumn { Width = new GridLength(2.5, GridUnitType.Star) }); // Product
            table.Columns.Add(new TableColumn { Width = new GridLength(0.8, GridUnitType.Star) }); // Qty
            table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) }); // Total (LBP)

            // Add header row
            var tableHeaderRow = new TableRow { Background = Brushes.LightGray };
            tableHeaderRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Product"))) { FontSize = 9 }));
            tableHeaderRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Qty")))
            { FontSize = 9, TextAlignment = TextAlignment.Center }));
            tableHeaderRow.Cells.Add(new TableCell(new Paragraph(new Bold(new Run("Total (LBP)")))
            { FontSize = 9, TextAlignment = TextAlignment.Right }));

            var rowGroup = new TableRowGroup();
            rowGroup.Rows.Add(tableHeaderRow);

            // Add grouped product rows
            foreach (var product in groupedProducts)
            {
                var row = new TableRow();

                // Product name cell
                var nameCell = new TableCell();
                var nameParagraph = new Paragraph { FontSize = 9 };
                nameParagraph.Inlines.Add(new Run(product.ProductName ?? "Unknown"));
                nameCell.Blocks.Add(nameParagraph);
                row.Cells.Add(nameCell);

                // Quantity cell
                var qtyCell = new TableCell();
                var qtyParagraph = new Paragraph { FontSize = 9, TextAlignment = TextAlignment.Center };
                qtyParagraph.Inlines.Add(new Run(product.TotalQuantity.ToString()));
                qtyCell.Blocks.Add(qtyParagraph);
                row.Cells.Add(qtyCell);

                // Total amount cell - LBP only
                var totalCell = new TableCell();
                var totalParagraph = new Paragraph { FontSize = 9, TextAlignment = TextAlignment.Right };

                // Convert and format in LBP
                decimal lbpAmount = CurrencyHelper.ConvertToLBP(product.TotalAmount);
                totalParagraph.Inlines.Add(new Run($"{lbpAmount:N0} LBP"));

                totalCell.Blocks.Add(totalParagraph);
                row.Cells.Add(totalCell);

                rowGroup.Rows.Add(row);
            }

            table.RowGroups.Add(rowGroup);
            section.Blocks.Add(table);
        }
        private void AddMetricRow(Table table, string label, string value, double fontSize = 10,
            bool isBold = false, Brush? foreground = null)
        {
            var row = new TableRow();

            // Label cell
            var labelCell = new TableCell();
            var labelParagraph = new Paragraph { FontSize = fontSize };
            var labelRun = new Run(label);
            if (isBold) labelRun.FontWeight = FontWeights.Bold;
            labelParagraph.Inlines.Add(labelRun);
            labelCell.Blocks.Add(labelParagraph);
            row.Cells.Add(labelCell);

            // Value cell
            var valueCell = new TableCell();
            var valueParagraph = new Paragraph
            {
                FontSize = fontSize,
                TextAlignment = TextAlignment.Right
            };
            var valueRun = new Run(value);
            if (isBold) valueRun.FontWeight = FontWeights.Bold;
            if (foreground != null) valueRun.Foreground = foreground;
            valueParagraph.Inlines.Add(valueRun);
            valueCell.Blocks.Add(valueParagraph);
            row.Cells.Add(valueCell);

            table.RowGroups[0].Rows.Add(row);
        }


        private async Task ShowErrorMessageAsync(string message)
        {
            ErrorMessage = message;

            var ownerWindow = GetOwnerWindow();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(
                    ownerWindow,
                    message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });

            await Task.Run(async () =>
            {
                await Task.Delay(3000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ErrorMessage == message)
                    {
                        ErrorMessage = string.Empty;
                    }
                });
            });
        }
    }
}