// Path: QuickTechSystems.WPF/ViewModels/TableManagementViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class TableManagementViewModel : ViewModelBase
    {
        private readonly IRestaurantTableService _tableService;
        private ObservableCollection<RestaurantTableDTO> _tables;
        private RestaurantTableDTO _selectedTable;
        private RestaurantTableDTO _newTable;
        private bool _isEditing;
        private bool _isAdding;
        private string _searchText;
        private ObservableCollection<string> _tableStatuses;

        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ChangeStatusCommand { get; }

        public ObservableCollection<RestaurantTableDTO> Tables
        {
            get => _tables;
            set => SetProperty(ref _tables, value);
        }

        public RestaurantTableDTO SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (SetProperty(ref _selectedTable, value) && value != null)
                {
                    // Create a copy for editing to prevent direct modification
                    _newTable = new RestaurantTableDTO
                    {
                        Id = value.Id,
                        TableNumber = value.TableNumber,
                        Status = value.Status,
                        Description = value.Description,
                        IsActive = value.IsActive,
                        CreatedAt = value.CreatedAt,
                        UpdatedAt = value.UpdatedAt
                    };
                }
            }
        }

        public RestaurantTableDTO NewTable
        {
            get => _newTable;
            set
            {
                if (SetProperty(ref _newTable, value))
                {
                    // Optionally add debug output here
                    System.Diagnostics.Debug.WriteLine($"NewTable set to: Table #{value?.TableNumber}, Status: {value?.Status}");
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public bool IsAdding
        {
            get => _isAdding;
            set => SetProperty(ref _isAdding, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterTables();
                }
            }
        }

        public ObservableCollection<string> TableStatuses
        {
            get => _tableStatuses;
            set => SetProperty(ref _tableStatuses, value);
        }
        public void ShowSelectedTableDetails()
        {
            if (SelectedTable != null)
            {
                System.Diagnostics.Debug.WriteLine($"Table selected: #{SelectedTable.TableNumber}");
            }
        }
        public TableManagementViewModel(
            IRestaurantTableService tableService,
            IEventAggregator eventAggregator,
            IDbContextScopeService dbContextScopeService)
            : base(eventAggregator, dbContextScopeService)
        {
            _tableService = tableService;
            _tables = new ObservableCollection<RestaurantTableDTO>();
            _newTable = new RestaurantTableDTO { Status = "Available" };
            _tableStatuses = new ObservableCollection<string>
            {
                "Available",
                "Occupied",
                "Reserved",
                "Maintenance"
            };

            AddCommand = new RelayCommand(_ => StartAddingTable());
            EditCommand = new RelayCommand(_ => StartEditingTable(), _ => SelectedTable != null);
            SaveCommand = new AsyncRelayCommand(async _ => await SaveTableAsync(), _ => IsValidTable());
            CancelCommand = new RelayCommand(_ => CancelEditing());
            DeleteCommand = new AsyncRelayCommand(async _ => await DeleteTableAsync(), _ => SelectedTable != null);
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadDataAsync());
            ChangeStatusCommand = new AsyncRelayCommand<string>(async status => await ChangeTableStatusAsync(status));
        }

        private void FilterTables()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                LoadDataAsync().ConfigureAwait(false);
                return;
            }

            // Use async operation to not block UI
            Task.Run(async () =>
            {
                var allTables = await _tableService.GetActiveTablesAsync();
                var filteredTables = allTables.Where(t =>
                    t.TableNumber.ToString().Contains(SearchText) ||
                    t.Status.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Tables = new ObservableCollection<RestaurantTableDTO>(filteredTables);
                });
            });
        }

        private void StartAddingTable()
        {
            NewTable = new RestaurantTableDTO { Status = "Available" };
            IsAdding = true;
            IsEditing = false;
            SelectedTable = null;
        }

        private void StartEditingTable()
        {
            if (SelectedTable == null) return;

            // Create a fresh copy of the selected table for editing
            NewTable = new RestaurantTableDTO
            {
                Id = SelectedTable.Id,
                TableNumber = SelectedTable.TableNumber,
                Status = SelectedTable.Status,
                Description = SelectedTable.Description,
                IsActive = SelectedTable.IsActive,
                CreatedAt = SelectedTable.CreatedAt,
                UpdatedAt = SelectedTable.UpdatedAt
            };

            // Set flags to show edit form
            IsEditing = true;
            IsAdding = false;

            // For debugging - verify data is copied correctly
            System.Diagnostics.Debug.WriteLine($"Editing Table #{NewTable.TableNumber}, Description: {NewTable.Description}, Status: {NewTable.Status}");
        }

        private async Task SaveTableAsync()
        {
            if (!IsValidTable())
                return;

            try
            {
                // Check if table number is unique
                bool isUnique = await _tableService.IsTableNumberUniqueAsync(
                    NewTable.TableNumber,
                    IsEditing ? SelectedTable?.Id : null);

                if (!isUnique)
                {
                    await ShowErrorMessageAsync("Table number already exists. Please choose a different number.");
                    return;
                }

                if (IsAdding)
                {
                    // For adding, create a new DTO without an ID
                    var tableToAdd = new RestaurantTableDTO
                    {
                        TableNumber = NewTable.TableNumber,
                        Status = NewTable.Status,
                        Description = NewTable.Description,
                        IsActive = true
                    };
                    await _tableService.CreateAsync(tableToAdd);
                    await ShowSuccessMessage("Table added successfully!");
                }
                else if (IsEditing && SelectedTable != null)
                {
                    // Ensure the ID is preserved for editing
                    NewTable.Id = SelectedTable.Id;

                    // Log the table being updated
                    System.Diagnostics.Debug.WriteLine($"Updating Table #{NewTable.TableNumber}, ID: {NewTable.Id}");

                    await _tableService.UpdateAsync(NewTable);
                    await ShowSuccessMessage("Table updated successfully!");
                }

                CancelEditing();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Error saving table", ex);
            }
        }
        private void CancelEditing()
        {
            IsAdding = false;
            IsEditing = false;
            NewTable = new RestaurantTableDTO { Status = "Available" };
        }

        private async Task DeleteTableAsync()
        {
            if (SelectedTable == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete table #{SelectedTable.TableNumber}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _tableService.DeleteAsync(SelectedTable.Id);
                await ShowSuccessMessage("Table deleted successfully!");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Error deleting table", ex);
            }
        }

        private async Task ChangeTableStatusAsync(string status)
        {
            if (SelectedTable == null) return;

            try
            {
                await _tableService.UpdateTableStatusAsync(SelectedTable.Id, status);
                await ShowSuccessMessage($"Table status changed to {status}!");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Error changing table status", ex);
            }
        }

        private bool IsValidTable()
        {
            if (NewTable == null) return false;
            if (NewTable.TableNumber <= 0)
            {
                ShowTemporaryErrorMessage("Table number must be greater than zero");
                return false;
            }
            return true;
        }

        protected override async Task LoadDataImplementationAsync()
        {
            try
            {
                var tables = await _tableService.GetActiveTablesAsync();
                Tables = new ObservableCollection<RestaurantTableDTO>(tables.OrderBy(t => t.TableNumber));
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Error loading tables", ex);
            }
        }

        protected override void SubscribeToEvents()
        {
            _eventAggregator.Subscribe<EntityChangedEvent<RestaurantTableDTO>>(HandleTableChanged);
        }

        protected override void UnsubscribeFromEvents()
        {
            _eventAggregator.Unsubscribe<EntityChangedEvent<RestaurantTableDTO>>(HandleTableChanged);
        }

        private void HandleTableChanged(EntityChangedEvent<RestaurantTableDTO> evt)
        {
            // Replace "EntityType" with "Action" to match your actual event class
            if (evt.Action == "Update" || evt.Action == "Create" || evt.Action == "Delete")
            {
                Debug.WriteLine($"Table changed: {evt.Action} - Table #{evt.Entity.TableNumber}");
                LoadDataAsync().ConfigureAwait(false);
            }
        }
    }
}