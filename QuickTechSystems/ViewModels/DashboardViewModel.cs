using Microsoft.Extensions.DependencyInjection;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SemaphoreSlim _loadingSemaphore = new(1, 1);
        private bool _isDisposed;

        private bool _isLoading;
        private bool _hasErrors;
        private string _errorMessage = string.Empty;

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasErrors
        {
            get => _hasErrors;
            set => SetProperty(ref _hasErrors, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand RefreshCommand { get; }

        public DashboardViewModel(
            IServiceProvider serviceProvider,
             IDrawerService drawerService,
             IEventAggregator eventAggregator)
            : base(eventAggregator)
        {
            Debug.WriteLine("DashboardViewModel: Constructor called");
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            Debug.WriteLine("DashboardViewModel: Creating commands");
            NavigateCommand = new RelayCommand(ExecuteNavigation);
            RefreshCommand = new AsyncRelayCommand(async _ => await RefreshDataAsync());

            Debug.WriteLine("DashboardViewModel: Constructor completed");
        }

        private async Task RefreshDataAsync()
        {
            Debug.WriteLine("DashboardViewModel: RefreshDataAsync called");
            await LoadDataAsync();
            Debug.WriteLine("DashboardViewModel: RefreshDataAsync completed");
        }

        protected override async Task LoadDataAsync()
        {
            Debug.WriteLine("DashboardViewModel: LoadDataAsync started");
            if (!await _loadingSemaphore.WaitAsync(0))
            {
                Debug.WriteLine("DashboardViewModel: LoadDataAsync skipped - already in progress");
                return;
            }

            try
            {
                Debug.WriteLine("DashboardViewModel: Setting loading state to true");
                IsLoading = true;
                HasErrors = false;
                ErrorMessage = string.Empty;

                // Dashboard now only contains navigation buttons, no data to load
                Debug.WriteLine("DashboardViewModel: No data to load for dashboard");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DashboardViewModel: Error loading dashboard data: {ex}");
                HasErrors = true;
                ErrorMessage = "Failed to load dashboard data. Please try refreshing.";
            }
            finally
            {
                Debug.WriteLine("DashboardViewModel: Setting loading state to false");
                IsLoading = false;
                Debug.WriteLine("DashboardViewModel: Releasing semaphore");
                _loadingSemaphore.Release();
            }
        }

        private void ExecuteNavigation(object? parameter)
        {
            try
            {
                Debug.WriteLine($"DashboardViewModel: ExecuteNavigation called with parameter: {parameter}");
                if (parameter is not string destination)
                {
                    Debug.WriteLine("DashboardViewModel: Invalid navigation parameter - not a string");
                    ShowTemporaryErrorMessage("Invalid navigation destination");
                    return;
                }

                Debug.WriteLine($"DashboardViewModel: Navigating to '{destination}' view");
                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                mainViewModel.NavigateCommand.Execute(destination);
                Debug.WriteLine($"DashboardViewModel: Navigation to '{destination}' executed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DashboardViewModel: Navigation error: {ex}");
                ShowTemporaryErrorMessage($"Navigation failed: {ex.Message}");
            }
        }

        protected override void SubscribeToEvents()
        {
            Debug.WriteLine("DashboardViewModel: SubscribeToEvents called");
            // No events to subscribe to for dashboard metrics
            Debug.WriteLine("DashboardViewModel: No events to subscribe to for dashboard");
        }

        protected override void UnsubscribeFromEvents()
        {
            Debug.WriteLine("DashboardViewModel: UnsubscribeFromEvents called");
            // No events to unsubscribe from
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            Debug.WriteLine($"DashboardViewModel: Showing temporary error message: '{message}'");
            ErrorMessage = message;
            HasErrors = true;

            // Automatically clear error after delay
            Task.Run(async () =>
            {
                Debug.WriteLine($"DashboardViewModel: Starting 5-second timeout for error message: '{message}'");
                await Task.Delay(5000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Debug.WriteLine("DashboardViewModel: Error message timeout completed");
                    if (ErrorMessage == message) // Only clear if still the same message
                    {
                        Debug.WriteLine("DashboardViewModel: Clearing error message");
                        ErrorMessage = string.Empty;
                        HasErrors = false;
                    }
                    else
                    {
                        Debug.WriteLine($"DashboardViewModel: Error message changed, not clearing. Current: '{ErrorMessage}'");
                    }
                });
            });
        }

        public override void Dispose()
        {
            Debug.WriteLine("DashboardViewModel: Dispose called");
            if (!_isDisposed)
            {
                Debug.WriteLine("DashboardViewModel: Disposing resources");
                _loadingSemaphore?.Dispose();
                _isDisposed = true;
                Debug.WriteLine("DashboardViewModel: Resources disposed");
            }

            base.Dispose();
            Debug.WriteLine("DashboardViewModel: Dispose completed");
        }
    }
}