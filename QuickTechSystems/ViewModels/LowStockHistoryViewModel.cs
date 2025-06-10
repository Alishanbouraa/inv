// Path: QuickTechSystems.WPF.ViewModels/LowStockHistoryViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class LowStockHistoryViewModel : ViewModelBase
    {
        private readonly ILowStockHistoryService _lowStockHistoryService;
        private ObservableCollection<LowStockHistoryDTO> _lowStockHistories;
        private DateTime _startDate;
        private DateTime _endDate;
        private bool _isLoading;
        private string _loadingMessage = "Loading...";
        private string _statusMessage = string.Empty;
        private LowStockHistoryDTO _selectedHistory;
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;
        private CancellationTokenSource _searchCts;
        private readonly SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);
        private bool _showResolved = true;
        private bool _showPending = true;

        public ObservableCollection<LowStockHistoryDTO> LowStockHistories
        {
            get => _lowStockHistories;
            set
            {
                SetProperty(ref _lowStockHistories, value);
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(ResolvedCount));
                OnPropertyChanged(nameof(PendingCount));
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        public FlowDirection FlowDirection
        {
            get => _flowDirection;
            set => SetProperty(ref _flowDirection, value);
        }
        public string LoadingMessage
        {
            get => _loadingMessage;
            set => SetProperty(ref _loadingMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public LowStockHistoryDTO SelectedHistory
        {
            get => _selectedHistory;
            set => SetProperty(ref _selectedHistory, value);
        }

        public bool ShowResolved
        {
            get => _showResolved;
            set
            {
                if (SetProperty(ref _showResolved, value))
                {
                    ApplyFilters();
                }
            }
        }

        public bool ShowPending
        {
            get => _showPending;
            set
            {
                if (SetProperty(ref _showPending, value))
                {
                    ApplyFilters();
                }
            }
        }

        // Calculated properties
        public bool HasItems => LowStockHistories != null && LowStockHistories.Any();
        public int ResolvedCount => LowStockHistories?.Count(x => x.IsResolved) ?? 0;
        public int PendingCount => LowStockHistories?.Count(x => !x.IsResolved) ?? 0;

        // Commands
        public ICommand SearchCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ResolveCommand { get; }
        public ICommand ResolveAllCommand { get; }

        public LowStockHistoryViewModel(
            ILowStockHistoryService lowStockHistoryService,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _lowStockHistoryService = lowStockHistoryService;
            _lowStockHistories = new ObservableCollection<LowStockHistoryDTO>();

            // Set default date range to last 30 days
            _endDate = DateTime.Today;
            _startDate = _endDate.AddDays(-30);

            // Initialize commands
            SearchCommand = new AsyncRelayCommand(async _ => await SearchAsync());
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadDataAsync());
            ExportCommand = new AsyncRelayCommand(async _ => await ExportDataAsync());
            ResolveCommand = new AsyncRelayCommand<LowStockHistoryDTO>(async (item) => await ResolveItemAsync(item));
            ResolveAllCommand = new AsyncRelayCommand(async _ => await ResolveAllItemsAsync());

            // Subscribe to low stock history events
            _eventAggregator.Subscribe<EntityChangedEvent<LowStockHistoryDTO>>(OnLowStockHistoryChanged);

            // Initial load
            LoadDataAsync().ConfigureAwait(false);
        }

        private async void OnLowStockHistoryChanged(EntityChangedEvent<LowStockHistoryDTO> evt)
        {
            await LoadDataAsync();
        }

        private async Task SearchAsync()
        {
            try
            {
                // Cancel any previous search
                if (_searchCts != null)
                {
                    _searchCts.Cancel();
                    _searchCts.Dispose();
                }

                _searchCts = new CancellationTokenSource();

                IsLoading = true;
                LoadingMessage = "Searching...";

                await LoadDataAsync();
            }
            catch (OperationCanceledException)
            {
                // Search was canceled, do nothing
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in search: {ex.Message}");
                StatusMessage = $"Search error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilters()
        {
            if (LowStockHistories == null)
                return;

            // Get the unfiltered data from database
            LoadDataAsync().ConfigureAwait(false);
        }

        protected override async Task LoadDataAsync()
        {
            // Use a semaphore to prevent concurrent operations
            if (!await _loadingSemaphore.WaitAsync(0))
            {
                Debug.WriteLine("LoadDataAsync skipped - already in progress");
                return;
            }

            try
            {
                IsLoading = true;
                LoadingMessage = "Loading low stock history...";

                // Small delay to allow UI to update
                await Task.Delay(100);

                // Use a separate task to query the database
                var history = await Task.Run(async () =>
                {
                    try
                    {
                        return await _lowStockHistoryService.GetByDateRangeAsync(StartDate, EndDate);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error querying low stock history: {ex.Message}");
                        throw;
                    }
                });

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Filter by show status if applicable
                    var filteredHistory = history;
                    if (!ShowResolved || !ShowPending)
                    {
                        filteredHistory = history.Where(h =>
                            (ShowResolved && h.IsResolved) ||
                            (ShowPending && !h.IsResolved)).ToList();
                    }

                    LowStockHistories = new ObservableCollection<LowStockHistoryDTO>(filteredHistory);
                    StatusMessage = $"Found {LowStockHistories.Count} records (Resolved: {ResolvedCount}, Pending: {PendingCount})";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading low stock history: {ex.Message}");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = $"Error loading data: {ex.Message}";
                });
            }
            finally
            {
                IsLoading = false;
                _loadingSemaphore.Release();
            }
        }

        private async Task ResolveItemAsync(LowStockHistoryDTO item)
        {
            if (item == null) return;

            try
            {
                IsLoading = true;
                LoadingMessage = "Resolving item...";

                var currentUser = App.Current.Properties["CurrentUser"] as EmployeeDTO;
                string resolvedBy = currentUser?.FullName ?? "Unknown";

                // Update the item
                item.IsResolved = true;
                item.ResolvedDate = DateTime.Now;
                item.ResolvedBy = resolvedBy;

                // Save to database
                await _lowStockHistoryService.UpdateAsync(item);

                // Refresh the selected item
                SelectedHistory = item;

                StatusMessage = "Item marked as resolved successfully.";

                // Refresh data to update counts
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resolving item: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ResolveAllItemsAsync()
        {
            try
            {
                // Check if there are any pending items
                int pendingCount = PendingCount;
                if (pendingCount == 0)
                {
                    StatusMessage = "No unresolved items to mark as resolved.";
                    return;
                }

                // Confirm with user
                var result = MessageBox.Show(
                    $"Are you sure you want to mark all {pendingCount} pending items as resolved?",
                    "Confirm Resolution",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                IsLoading = true;
                LoadingMessage = "Resolving all items...";

                var currentUser = App.Current.Properties["CurrentUser"] as EmployeeDTO;
                string resolvedBy = currentUser?.FullName ?? "Unknown";

                // Resolve each unresolved item
                foreach (var item in LowStockHistories.Where(i => !i.IsResolved).ToList())
                {
                    item.IsResolved = true;
                    item.ResolvedDate = DateTime.Now;
                    item.ResolvedBy = resolvedBy;
                    await _lowStockHistoryService.UpdateAsync(item);
                }

                // Refresh data
                await LoadDataAsync();
                StatusMessage = $"Successfully resolved {pendingCount} items.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resolving all items: {ex.Message}");
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportDataAsync()
        {
            try
            {
                IsLoading = true;
                LoadingMessage = "Exporting data...";

                // Placeholder for actual export implementation
                await Task.Delay(1000);
                StatusMessage = "Export feature coming soon.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting data: {ex.Message}");
                StatusMessage = $"Error exporting data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public override void Dispose()
        {
            // Unsubscribe from events
            _eventAggregator.Unsubscribe<EntityChangedEvent<LowStockHistoryDTO>>(OnLowStockHistoryChanged);

            // Dispose resources
            _loadingSemaphore?.Dispose();

            if (_searchCts != null)
            {
                try
                {
                    _searchCts.Cancel();
                    _searchCts.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing search token: {ex.Message}");
                }
                _searchCts = null;
            }

            base.Dispose();
        }
    }
}