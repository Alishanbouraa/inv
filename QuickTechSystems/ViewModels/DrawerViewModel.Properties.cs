using System;
using System.Collections.ObjectModel;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class DrawerViewModel
    {
        private DrawerDTO? _currentDrawer;
        private ObservableCollection<DrawerTransactionDTO> _drawerHistory;
        private string _statusMessage = string.Empty;
        private bool _isProcessing;
        // Add these properties to DrawerViewModel.Properties.cs
        private decimal _initialCashAmount;
        private decimal _cashAmount;
        private string _cashDescription = string.Empty;
        private decimal _finalCashAmount;
        // Add to DrawerViewModel.Properties.cs
        private bool _includeTransactionDetails = true;
        private bool _includeFinancialSummary = true;
        private bool _printCashierCopy = false;
        private ObservableCollection<DrawerSessionItem> _drawerSessions;
        private DrawerSessionItem? _selectedDrawerSession;
        private DateTime _sessionStartDate = DateTime.Today.AddDays(-30);
        private DateTime _sessionEndDate = DateTime.Today;
        private bool _isViewingHistoricalSession;

        public bool IncludeTransactionDetails
        {
            get => _includeTransactionDetails;
            set => SetProperty(ref _includeTransactionDetails, value);
        }

        public bool IncludeFinancialSummary
        {
            get => _includeFinancialSummary;
            set => SetProperty(ref _includeFinancialSummary, value);
        }

        public bool PrintCashierCopy
        {
            get => _printCashierCopy;
            set => SetProperty(ref _printCashierCopy, value);
        }
        public decimal InitialCashAmount
        {
            get => _initialCashAmount;
            set => SetProperty(ref _initialCashAmount, value);
        }

        public decimal CashAmount
        {
            get => _cashAmount;
            set => SetProperty(ref _cashAmount, value);
        }

        public string CashDescription
        {
            get => _cashDescription;
            set => SetProperty(ref _cashDescription, value);
        }

        public decimal FinalCashAmount
        {
            get => _finalCashAmount;
            set
            {
                if (SetProperty(ref _finalCashAmount, value))
                {
                    OnPropertyChanged(nameof(DrawerClosingDifference));
                }
            }
        }
        public ObservableCollection<DrawerSessionItem> DrawerSessions
        {
            get => _drawerSessions;
            set => SetProperty(ref _drawerSessions, value);
        }

        public DrawerSessionItem? SelectedDrawerSession
        {
            get => _selectedDrawerSession;
            set
            {
                if (SetProperty(ref _selectedDrawerSession, value))
                {
                    _ = LoadSelectedSessionAsync();
                }
            }
        }

        public DateTime SessionStartDate
        {
            get => _sessionStartDate;
            set => SetProperty(ref _sessionStartDate, value);
        }

        public DateTime SessionEndDate
        {
            get => _sessionEndDate;
            set => SetProperty(ref _sessionEndDate, value);
        }

        public bool IsViewingHistoricalSession
        {
            get => _isViewingHistoricalSession;
            set => SetProperty(ref _isViewingHistoricalSession, value);
        }

        // Helper class for dropdown items
        public class DrawerSessionItem
        {
            public int DrawerId { get; set; }
            public DateTime OpenedAt { get; set; }
            public DateTime? ClosedAt { get; set; }
            public string CashierName { get; set; } = string.Empty;
            public string CashierId { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;

            public string DisplayText =>
                $"Drawer {DrawerId}. {OpenedAt:MM/dd/yyyy}, Opened By: {CashierId}, {OpenedAt:MM/dd/yyyy}, {CashierName}";
        }
        public decimal DrawerClosingDifference =>
            CurrentDrawer != null ? FinalCashAmount - CurrentDrawer.CurrentBalance : 0;
        public decimal CurrentBalance => CurrentDrawer?.CurrentBalance ?? 0;

        public decimal ExpectedBalance => CurrentDrawer?.ExpectedBalance ?? 0;

        public decimal Difference => CurrentDrawer?.Difference ?? 0;
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;
        private DateTime _minimumDate = DateTime.Today.AddYears(-1);
        private DateTime _maximumDate = DateTime.Today;

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    // Ensure EndDate is not before StartDate
                    if (EndDate < value)
                    {
                        EndDate = value;
                    }
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public DateTime MinimumDate
        {
            get => _minimumDate;
            set => SetProperty(ref _minimumDate, value);
        }

        public DateTime MaximumDate
        {
            get => _maximumDate;
            set => SetProperty(ref _maximumDate, value);
        }

        public ICommand ApplyDateFilterCommand { get; }
        public decimal TotalSales
        {
            get => _totalSales;
            set => SetProperty(ref _totalSales, value);
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

        public decimal NetEarnings
        {
            get => _netEarnings;
            set => SetProperty(ref _netEarnings, value);
        }

        public decimal TotalReturns
        {
            get => _totalReturns;
            set => SetProperty(ref _totalReturns, value);
        }

        public decimal NetSales
        {
            get => _netSales;
            set => SetProperty(ref _netSales, value);
        }

        public decimal NetCashflow
        {
            get => _netCashflow;
            set => SetProperty(ref _netCashflow, value);
        }

        public decimal DailySales
        {
            get => _dailySales;
            set => SetProperty(ref _dailySales, value);
        }

        public decimal DailyReturns
        {
            get => _dailyReturns;
            set => SetProperty(ref _dailyReturns, value);
        }

        public decimal DailyExpenses
        {
            get => _dailyExpenses;
            set => SetProperty(ref _dailyExpenses, value);
        }

        public decimal SupplierPayments
        {
            get => _supplierPayments;
            set => SetProperty(ref _supplierPayments, value);
        }

        public DrawerDTO? CurrentDrawer
        {
            get => _currentDrawer;
            set
            {
                if (SetProperty(ref _currentDrawer, value))
                {
                    OnPropertyChanged(nameof(IsDrawerOpen));
                    OnPropertyChanged(nameof(CanOpenDrawer));
                    OnPropertyChanged(nameof(CurrentBalance));
                    OnPropertyChanged(nameof(ExpectedBalance));
                    OnPropertyChanged(nameof(Difference));
                }
            }
        }

        public ObservableCollection<DrawerTransactionDTO> DrawerHistory
        {
            get => _drawerHistory;
            set => SetProperty(ref _drawerHistory, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsDrawerOpen => CurrentDrawer?.Status == "Open";
        public bool CanOpenDrawer => CurrentDrawer == null || CurrentDrawer.Status == "Closed";
    }
}