using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using QuickTechSystems.WPF.Views;
using QuickTechSystems.WPF.Services;
using System.Diagnostics;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class DrawerViewModel : ViewModelBase
    {
        private readonly IDrawerService _drawerService;
        private readonly IWindowService _windowService;

        private TransactionHistoryViewModel _transactionHistoryViewModel;

        public TransactionHistoryViewModel TransactionHistoryViewModel
        {
            get => _transactionHistoryViewModel;
            set => SetProperty(ref _transactionHistoryViewModel, value);
        }

        private ProfitViewModel _profitViewModel;

        public ProfitViewModel ProfitViewModel
        {
            get => _profitViewModel;
            set => SetProperty(ref _profitViewModel, value);
        }

        public DrawerViewModel(
            IDrawerService drawerService,
            IWindowService windowService,
            IEventAggregator eventAggregator,
            TransactionHistoryViewModel transactionHistoryViewModel,
            ProfitViewModel profitViewModel) : base(eventAggregator)
        {
            _drawerService = drawerService;
            _windowService = windowService;
            _drawerSessions = new ObservableCollection<DrawerSessionItem>();

            ProfitViewModel = profitViewModel;
            _drawerHistory = new ObservableCollection<DrawerTransactionDTO>();
            TransactionHistoryViewModel = transactionHistoryViewModel;

            // Initialize commands
            OpenDrawerCommand = new AsyncRelayCommand(async _ => await OpenDrawerAsync(), _ => CanOpenDrawer && !IsProcessing);
            AddCashCommand = new AsyncRelayCommand(async _ => await AddCashAsync(), _ => IsDrawerOpen && !IsProcessing);
            RemoveCashCommand = new AsyncRelayCommand(async _ => await RemoveCashAsync(), _ => IsDrawerOpen && !IsProcessing);
            CloseDrawerCommand = new AsyncRelayCommand(async _ => await CloseDrawerAsync(), _ => IsDrawerOpen && !IsProcessing);
            PrintReportCommand = new AsyncRelayCommand(async _ => await PrintReportAsync(), _ => IsDrawerOpen && !IsProcessing);
            LoadFinancialDataCommand = new AsyncRelayCommand(async _ => await LoadFinancialOverviewAsync());
            ApplyDateFilterCommand = new AsyncRelayCommand(async _ => await ApplyDateFilterAsync());
            LoadDrawerSessionsCommand = new AsyncRelayCommand(async _ => await LoadDrawerSessionsAsync());
            ApplySessionFilterCommand = new AsyncRelayCommand(async _ => await ApplySessionFilterAsync());
            ViewCurrentSessionCommand = new AsyncRelayCommand(async _ => await ViewCurrentSessionAsync());
            // Initialize date range
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
            MinimumDate = DateTime.Today.AddYears(-1);
            MaximumDate = DateTime.Today;

            // Subscribe to events
            eventAggregator.Subscribe<SupplierPaymentEvent>(HandleSupplierPayment);
            eventAggregator.Subscribe<EntityChangedEvent<DrawerDTO>>(HandleDrawerChanged);
            eventAggregator.Subscribe<DrawerUpdateEvent>(HandleDrawerUpdate);
            _ = LoadDataAsync();
        }

        protected override async Task LoadDataAsync()
        {
            try
            {
                IsProcessing = true;
                await LoadDataSequentiallyAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorMessageAsync($"Error loading drawer data: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }
     
        private async Task LoadDataSequentiallyAsync()
        {
            int maxRetries = 3;
            int currentRetry = 0;

            while (currentRetry < maxRetries)
            {
                try
                {
                    IsProcessing = true;

                    CurrentDrawer = await _drawerService.GetCurrentDrawerAsync();
                    if (CurrentDrawer == null)
                    {
                        DrawerHistory.Clear();
                        return;
                    }

                    await LoadDrawerHistoryAsync();
                    UpdateStatus();
                    return;
                }
                catch (Exception ex)
                {
                    currentRetry++;
                    Debug.WriteLine($"Attempt {currentRetry} failed: {ex.Message}");

                    if (currentRetry == maxRetries)
                    {
                        await ShowErrorMessageAsync("Unable to load drawer data. Please try again.");
                        throw;
                    }

                    await Task.Delay(1000 * currentRetry); // Exponential backoff
                }
                finally
                {
                    IsProcessing = false;
                }
            }
        }

    }
}