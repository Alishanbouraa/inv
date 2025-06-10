using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuickTechSystems.Domain.Interfaces.Repositories;
using System.Diagnostics;
using System.Linq;

namespace QuickTechSystems.WPF.ViewModels
{
    public class ProfitViewModel : ViewModelBase
    {
        private readonly ITransactionService _transactionService;
        private readonly IDrawerService _drawerService;
        private readonly IUnitOfWork _unitOfWork;
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;
        private bool _isDisposed;
        private CancellationTokenSource _cts;

        private decimal _grossProfit;
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;
        private decimal _otherDeductions;
        private decimal _netProfit;
        private decimal _totalSales;
        private decimal _totalExpenses;
        private decimal _totalSupplierPayments;
        private decimal _costOfGoodsSold;
        private int _totalTransactions;
        private decimal _grossProfitPercentage;
        private decimal _netProfitPercentage;
        private ObservableCollection<ProfitDetailDTO> _profitDetails;

        public ProfitViewModel(
            ITransactionService transactionService,
            IDrawerService drawerService,
            IUnitOfWork unitOfWork,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _transactionService = transactionService;
            _drawerService = drawerService;
            _unitOfWork = unitOfWork;
            _profitDetails = new ObservableCollection<ProfitDetailDTO>();
            _cts = new CancellationTokenSource();

            ExportCommand = new AsyncRelayCommand(async _ => await ExportReportAsync(), CanExecuteCommand);
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadDataAsync(), CanExecuteCommand);

            _eventAggregator.Subscribe<EntityChangedEvent<TransactionDTO>>(HandleTransactionChanged);
            _eventAggregator.Subscribe<DrawerUpdateEvent>(HandleDrawerUpdate);

            _ = LoadDataAsync();
        }

        #region Properties
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    _ = LoadDataAsync();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    _ = LoadDataAsync();
                }
            }
        }

        public decimal TotalSales
        {
            get => _totalSales;
            set => SetProperty(ref _totalSales, value);
        }

        public decimal CostOfGoodsSold
        {
            get => _costOfGoodsSold;
            set => SetProperty(ref _costOfGoodsSold, value);
        }

        public decimal GrossProfit
        {
            get => _grossProfit;
            set => SetProperty(ref _grossProfit, value);
        }

        public decimal TotalExpenses
        {
            get => _totalExpenses;
            set => SetProperty(ref _totalExpenses, value);
        }

        public decimal TotalSupplierPayments
        {
            get => _totalSupplierPayments;
            set => SetProperty(ref _totalSupplierPayments, value);
        }

        public decimal NetProfit
        {
            get => _netProfit;
            set => SetProperty(ref _netProfit, value);
        }

        public int TotalTransactions
        {
            get => _totalTransactions;
            set => SetProperty(ref _totalTransactions, value);
        }

        public decimal GrossProfitPercentage
        {
            get => _grossProfitPercentage;
            set => SetProperty(ref _grossProfitPercentage, value);
        }

        public decimal NetProfitPercentage
        {
            get => _netProfitPercentage;
            set => SetProperty(ref _netProfitPercentage, value);
        }

        public ObservableCollection<ProfitDetailDTO> ProfitDetails
        {
            get => _profitDetails;
            set => SetProperty(ref _profitDetails, value);
        }

        public ICommand ExportCommand { get; }
        public ICommand RefreshCommand { get; }
        #endregion

        private bool CanExecuteCommand(object? parameter)
        {
            return !IsLoading;
        }
        public FlowDirection FlowDirection
        {
            get => _flowDirection;
            set => SetProperty(ref _flowDirection, value);
        }

        public decimal OtherDeductions
        {
            get => _otherDeductions;
            set
            {
                if (SetProperty(ref _otherDeductions, value))
                {
                    // Update net profit calculation when deductions change
                    NetProfit = GrossProfit - TotalExpenses - TotalSupplierPayments - OtherDeductions;
                    NetProfitPercentage = TotalSales > 0 ? (NetProfit / TotalSales) * 100 : 0;
                }
            }
        }
        protected override async Task LoadDataAsync()
        {
            // Skip if already loading
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadDataAsync skipped - already in progress");
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

                    // Get transactions with the current UnitOfWork
                    var transactions = await _transactionService.GetByDateRangeAsync(StartDate, EndDate);
                    if (linkedCts.Token.IsCancellationRequested) return;

                    var transactionsList = transactions.ToList();
                    TotalTransactions = transactionsList.Count;

                    // Calculate summary metrics directly from transactions
                    decimal totalSalesFromTransactions = transactionsList.Sum(t => t.TotalAmount);

                    // Calculate COGS (cost of goods sold)
                    CostOfGoodsSold = transactionsList
                        .SelectMany(t => t.Details)
                        .Sum(d => d.PurchasePrice * d.Quantity);

                    // Get financial summary for expenses and supplier payments only
                    var financialSummary = await _drawerService.GetFinancialSummaryAsync(StartDate, EndDate);
                    if (linkedCts.Token.IsCancellationRequested) return;

                    // Use transaction-based calculations for sales
                    TotalSales = totalSalesFromTransactions;

                    // Keep expenses and supplier payments from drawer service
                    TotalExpenses = financialSummary.Expenses;
                    TotalSupplierPayments = financialSummary.SupplierPayments;

                    // Calculate profits with simplified logic
                    GrossProfit = transactionsList.Sum(t =>
                    {
                        // Skip if transaction has no details
                        if (t.Details == null || !t.Details.Any())
                            return 0;

                        // Skip profit calculation if total amount is zero
                        if (t.TotalAmount == 0)
                            return 0;

                        // Calculate base profit
                        return t.Details.Sum(d => (d.UnitPrice - d.PurchasePrice) * d.Quantity);
                    });

                    // Net profit calculation
                    NetProfit = GrossProfit - TotalExpenses - TotalSupplierPayments;

                    // Calculate percentages
                    GrossProfitPercentage = TotalSales > 0 ? (GrossProfit / TotalSales) * 100 : 0;
                    NetProfitPercentage = TotalSales > 0 ? (NetProfit / TotalSales) * 100 : 0;

                    // Create detailed profit breakdown
                    if (linkedCts.Token.IsCancellationRequested) return;

                    var details = new List<ProfitDetailDTO>();
                    foreach (var transaction in transactionsList)
                    {
                        foreach (var detail in transaction.Details)
                        {
                            decimal sales = detail.UnitPrice * detail.Quantity;
                            decimal cost = detail.PurchasePrice * detail.Quantity;

                            details.Add(new ProfitDetailDTO
                            {
                                Date = transaction.TransactionDate,
                                Sales = sales,
                                Cost = cost,
                                TransactionCount = 1
                            });
                        }
                    }

                    details = details.OrderByDescending(d => d.Date).ToList();

                    // Reconciliation check for debugging purposes
                    decimal detailSalesSum = details.Sum(d => d.Sales);
                    decimal detailCostSum = details.Sum(d => d.Cost);
                    decimal detailGrossProfitSum = details.Sum(d => d.GrossProfit);

                    if (Math.Abs(detailSalesSum - TotalSales) > 0.01m ||
                        Math.Abs(detailGrossProfitSum - GrossProfit) > 0.01m)
                    {
                        Debug.WriteLine($"Calculation discrepancy detected:");
                        Debug.WriteLine($"Summary - Sales: {TotalSales:C2}, Gross Profit: {GrossProfit:C2}");
                        Debug.WriteLine($"Details - Sales: {detailSalesSum:C2}, Gross Profit: {detailGrossProfitSum:C2}");
                    }

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!linkedCts.Token.IsCancellationRequested)
                        {
                            ProfitDetails = new ObservableCollection<ProfitDetailDTO>(details);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation was canceled");
                }
                catch (Exception ex)
                {
                    await HandleExceptionAsync("Error loading profit data", ex);
                }
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private decimal AggregateTransactionValues(List<TransactionDTO> transactions, Func<TransactionDTO, bool> filter, Func<TransactionDTO, decimal> selector)
        {
            return transactions
                .Where(filter)
                .Sum(selector);
        }

        private async Task ExportReportAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Export operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    FileName = $"Profit_Report_{DateTime.Now:yyyyMMdd}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var csv = new StringBuilder();

                    // Add header summary
                    csv.AppendLine($"Profit Report for {StartDate:d} to {EndDate:d}");
                    csv.AppendLine();
                    csv.AppendLine($"Total Sales,{TotalSales:F2}");
                    csv.AppendLine($"Cost of Goods Sold,{CostOfGoodsSold:F2}");
                    csv.AppendLine($"Gross Profit,{GrossProfit:F2}");
                    csv.AppendLine($"Gross Profit Percentage,{GrossProfitPercentage:F2}%");
                    csv.AppendLine($"Total Expenses,{TotalExpenses:F2}");
                    csv.AppendLine($"Supplier Payments,{TotalSupplierPayments:F2}");
                    csv.AppendLine($"Net Profit,{NetProfit:F2}");
                    csv.AppendLine($"Net Profit Percentage,{NetProfitPercentage:F2}%");
                    csv.AppendLine();

                    // Add transaction details
                    csv.AppendLine("Date,Time,Sales,Cost,Gross Profit,Profit Margin");

                    foreach (var detail in ProfitDetails)
                    {
                        csv.AppendLine($"{detail.Date:d},{detail.Date:t}," +
                            $"{detail.Sales:F2}," +
                            $"{detail.Cost:F2}," +
                            $"{detail.GrossProfit:F2}," +
                            $"{detail.ProfitMargin:P2}");
                    }

                    await File.WriteAllTextAsync(saveFileDialog.FileName, csv.ToString());
                    await ShowMessageAsync("Report exported successfully.", "Export Complete");
                }
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Error exporting report", ex);
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task HandleExceptionAsync(string context, Exception ex)
        {
            Debug.WriteLine($"{context}: {ex}");

            // Special handling for known database errors
            if (ex.Message.Contains("A second operation was started") ||
                (ex.InnerException != null && ex.InnerException.Message.Contains("A second operation was started")))
            {
                ShowTemporaryErrorMessage("Database is busy processing another request. Please try again in a moment.");
            }
            else if (ex.Message.Contains("entity with the specified primary key") ||
                    (ex.InnerException != null && ex.InnerException.Message.Contains("entity with the specified primary key")))
            {
                ShowTemporaryErrorMessage("Requested record not found. It may have been deleted.");
            }
            else if (ex.Message.Contains("The connection was closed") ||
                    (ex.InnerException != null && ex.InnerException.Message.Contains("The connection was closed")))
            {
                ShowTemporaryErrorMessage("Database connection lost. Please check your connection and try again.");
            }
            else
            {
                ShowTemporaryErrorMessage($"{context}: {ex.Message}");
            }
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            ErrorMessage = message;

            // Automatically clear error after delay
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ErrorMessage == message) // Only clear if still the same message
                    {
                        ErrorMessage = string.Empty;
                    }
                });
            });
        }

        private async Task ShowMessageAsync(string message, string title)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(message, title,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            });
        }

        // Debounced event handlers to prevent multiple calls
        private DateTime _lastTransactionEventTime = DateTime.MinValue;
        private DateTime _lastDrawerEventTime = DateTime.MinValue;
        private readonly object _eventLock = new object();

        private void HandleTransactionChanged(EntityChangedEvent<TransactionDTO> evt)
        {
            if (IsLoading) return;

            lock (_eventLock)
            {
                var now = DateTime.Now;
                if ((now - _lastTransactionEventTime).TotalMilliseconds < 500)
                {
                    return; // Ignore events that come too quickly
                }
                _lastTransactionEventTime = now;
            }

            Task.Run(async () =>
            {
                await Task.Delay(500); // Debounce delay
                await LoadDataAsync();
            });
        }

        private void HandleDrawerUpdate(DrawerUpdateEvent evt)
        {
            if (IsLoading) return;

            lock (_eventLock)
            {
                var now = DateTime.Now;
                if ((now - _lastDrawerEventTime).TotalMilliseconds < 500)
                {
                    return; // Ignore events that come too quickly
                }
                _lastDrawerEventTime = now;
            }

            Task.Run(async () =>
            {
                await Task.Delay(500); // Debounce delay
                await LoadDataAsync();
            });
        }

        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _operationLock?.Dispose();
                _isDisposed = true;
            }
            base.Dispose();
        }
    }
}