using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class CustomerViewModel : ViewModelBase, IDisposable
    {
        private readonly ICustomerService _customerService;
        private readonly ICustomerPrintService _printService;
        private readonly SemaphoreSlim _operationLock = new(1, 1);
        private readonly Dictionary<string, Func<Task>> _asyncOperations;
        private readonly List<Action> _pendingUIUpdates = new();

        private ObservableCollection<CustomerDTO> _customers = new();
        private CustomerDTO? _selectedCustomer;
        private string _searchText = string.Empty;
        private string _errorMessage = string.Empty;
        private decimal _paymentAmount;
        private bool _isSaving, _isEditing, _isNewCustomer, _hasErrors, _isPaymentDialogOpen, _operationInProgress;
        private FlowDirection _currentFlowDirection = FlowDirection.LeftToRight;

        public bool IsSaving { get => _isSaving; set { SetProperty(ref _isSaving, value); OnPropertyChanged(nameof(IsNotSaving)); } }
        public bool IsNotSaving => !IsSaving;
        public ObservableCollection<CustomerDTO> Customers { get => _customers; set => SetProperty(ref _customers, value); }
        public CustomerDTO? SelectedCustomer { get => _selectedCustomer; set { SetProperty(ref _selectedCustomer, value); IsEditing = value != null; } }
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }
        public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); _ = SearchCustomersAsync(); } }
        public FlowDirection CurrentFlowDirection { get => _currentFlowDirection; set => SetProperty(ref _currentFlowDirection, value); }
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool HasErrors { get => _hasErrors; set => SetProperty(ref _hasErrors, value); }
        public bool IsNewCustomer { get => _isNewCustomer; set => SetProperty(ref _isNewCustomer, value); }
        public bool IsPaymentDialogOpen { get => _isPaymentDialogOpen; set => SetProperty(ref _isPaymentDialogOpen, value); }
        public decimal PaymentAmount { get => _paymentAmount; set => SetProperty(ref _paymentAmount, value); }

        public ICommand LoadCommand => new AsyncRelayCommand(async _ => await ExecuteOperation("Load"), _ => !IsSaving);
        public ICommand AddCommand => new RelayCommand(_ => AddNew(), _ => !IsSaving);
        public ICommand SaveCommand => new AsyncRelayCommand(async _ => await ExecuteOperation("Save"), _ => !IsSaving);
        public ICommand DeleteCommand => new AsyncRelayCommand(async _ => await ExecuteOperation("Delete"), _ => !IsSaving);
        public ICommand SearchCommand => new AsyncRelayCommand(async _ => await ExecuteOperation("Search"), _ => !IsSaving);
        public ICommand ProcessPaymentCommand => new AsyncRelayCommand(async _ => await ExecuteOperation("ProcessPayment"), _ => !IsSaving && PaymentAmount > 0 && SelectedCustomer != null);
        public ICommand ShowPaymentDialogCommand => new AsyncRelayCommand(async _ => await ExecuteOperation("ShowPaymentDialog"), _ => !IsSaving && SelectedCustomer != null && SelectedCustomer.Balance > 0);
        public ICommand ClosePaymentDialogCommand => new RelayCommand(_ => ClosePaymentDialog(), _ => !IsSaving);

        public CustomerViewModel(ICustomerService customerService, ICustomerPrintService printService, IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _printService = printService ?? throw new ArgumentNullException(nameof(printService));

            _asyncOperations = new Dictionary<string, Func<Task>>
            {
                ["Load"] = LoadDataAsync,
                ["Search"] = SearchCustomersAsync,
                ["Save"] = SaveAsync,
                ["Delete"] = DeleteAsync,
                ["ProcessPayment"] = ProcessPayment,
                ["ShowPaymentDialog"] = ShowPaymentDialog
            };

            _ = LoadDataAsync();
        }

        protected override void SubscribeToEvents() => _eventAggregator.Subscribe<EntityChangedEvent<CustomerDTO>>(HandleCustomerChanged);
        protected override void UnsubscribeFromEvents() => _eventAggregator.Unsubscribe<EntityChangedEvent<CustomerDTO>>(HandleCustomerChanged);

        private async void HandleCustomerChanged(EntityChangedEvent<CustomerDTO> evt)
        {
            await DispatchUI(() =>
            {
                var existingCustomer = Customers.FirstOrDefault(c => c.CustomerId == evt.Entity.CustomerId);
                var operations = new Dictionary<string, Action>
                {
                    ["Create"] = () => Customers.Add(evt.Entity),
                    ["Update"] = () => { if (existingCustomer != null) { var index = Customers.IndexOf(existingCustomer); if (index >= 0) Customers[index] = evt.Entity; } },
                    ["Delete"] = () => { if (existingCustomer != null) Customers.Remove(existingCustomer); }
                };
                operations.TryGetValue(evt.Action, out var operation);
                operation?.Invoke();
            });
        }

        private async Task ExecuteOperation(string operationName)
        {
            if (_asyncOperations.TryGetValue(operationName, out var operation))
                await operation();
        }

        private async Task<T> ExecuteDbOperation<T>(Func<Task<T>> operation)
        {
            await EnsureOperationLock();
            try
            {
                await Task.Delay(200);
                return await operation();
            }
            catch (Exception ex)
            {
                await ShowError($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                _operationInProgress = false;
                _operationLock.Release();
            }
        }

        private async Task ExecuteDbOperation(Func<Task> operation) => await ExecuteDbOperation<bool>(async () => { await operation(); return true; });

        private async Task EnsureOperationLock()
        {
            var waitCount = 0;
            while (_operationInProgress && waitCount < 50)
            {
                await Task.Delay(100);
                waitCount++;
            }

            if (waitCount >= 50)
            {
                _operationInProgress = false;
                if (_operationLock.CurrentCount == 0) _operationLock.Release();
            }

            var lockAcquired = await _operationLock.WaitAsync(5000);
            if (!lockAcquired)
            {
                _operationInProgress = false;
                while (_operationLock.CurrentCount == 0) _operationLock.Release();
                await _operationLock.WaitAsync();
            }
            _operationInProgress = true;
        }

        private async Task ShowError(string message)
        {
            SetErrorState(message, true);
            if (message.Contains("critical") || message.Contains("exception"))
                await DispatchUI(() => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            _ = ClearErrorAfterDelay(message);
        }

        private void SetErrorState(string message, bool hasErrors)
        {
            ErrorMessage = message;
            HasErrors = hasErrors;
        }

        private async Task ClearErrorAfterDelay(string message)
        {
            await Task.Delay(5000);
            await DispatchUI(() => { if (ErrorMessage == message) SetErrorState(string.Empty, false); });
        }

        private async Task DispatchUI(Action action) => await System.Windows.Application.Current.Dispatcher.InvokeAsync(action);

        protected override async Task LoadDataAsync()
        {
            try
            {
                await SetOperationState(true);
                var customers = await ExecuteDbOperation(() => _customerService.GetAllAsync());
                var selectedCustomerId = SelectedCustomer?.CustomerId;

                await DispatchUI(() =>
                {
                    Customers = new ObservableCollection<CustomerDTO>(customers);
                    if (selectedCustomerId.HasValue)
                        SelectedCustomer = Customers.FirstOrDefault(c => c.CustomerId == selectedCustomerId.Value);
                });
            }
            catch (Exception ex)
            {
                await ShowError(ex.Message.Contains("second operation") ? "Database is busy. Please try again in a moment." : $"Error loading customers: {ex.Message}");
            }
            finally { await SetOperationState(false); }
        }

        private async Task SearchCustomersAsync()
        {
            try
            {
                await SetOperationState(true);
                var searchTerm = SearchText;
                var customers = await ExecuteDbOperation(() => string.IsNullOrWhiteSpace(searchTerm) ? _customerService.GetAllAsync() : _customerService.GetByNameAsync(searchTerm));
                await DispatchUI(() => Customers = new ObservableCollection<CustomerDTO>(customers));
            }
            catch (Exception ex) { await ShowError($"Error searching customers: {ex.Message}"); }
            finally { await SetOperationState(false); }
        }

        private async Task SetOperationState(bool isSaving)
        {
            IsSaving = isSaving;
            SetErrorState(string.Empty, false);
        }

        private void AddNew()
        {
            SelectedCustomer = new CustomerDTO { IsActive = true, CreatedAt = DateTime.Now };
            IsNewCustomer = true;
            ShowCustomerDetailsWindow();
        }

        public void EditCustomer(CustomerDTO customer)
        {
            if (customer != null)
            {
                SelectedCustomer = customer;
                IsNewCustomer = false;
                ShowCustomerDetailsWindow();
            }
        }

        public void ShowCustomerDetailsWindow() => new CustomerDetailsWindow(this).ShowDialog();

        public async Task UpdateCustomerDirectEdit(CustomerDTO customer)
        {
            if (customer == null) return;
            try
            {
                SetErrorState(string.Empty, false);
                var customerToSave = CreateCustomerDTO(customer);
                await ExecuteDbOperation(() => _customerService.UpdateAsync(customerToSave));
            }
            catch (Exception ex)
            {
                await ShowError($"Error updating customer: {ex.Message}");
                await LoadDataAsync();
            }
        }

        private async Task SaveAsync()
        {
            if (SelectedCustomer?.Name?.Trim().Length == 0)
            {
                MessageBox.Show("Customer name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await SetOperationState(true);
                var customerToSave = CreateCustomerDTO(SelectedCustomer);

                if (customerToSave.CustomerId == 0)
                {
                    var savedCustomer = await ExecuteDbOperation(() => _customerService.CreateAsync(customerToSave));
                    await DispatchUI(() => { Customers.Add(savedCustomer); SelectedCustomer = savedCustomer; });
                }
                else
                {
                    await ExecuteDbOperation(() => _customerService.UpdateAsync(customerToSave));
                    await UpdateCustomerInCollection(customerToSave);
                }

                await DispatchUI(() => MessageBox.Show("Customer saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information));
            }
            catch (Exception ex)
            {
                var isBusyError = ex.Message.Contains("second operation") || ex.Message.Contains("concurrency");
                await ShowError(isBusyError ? "Database is busy. Please try again in a moment." : $"Error saving customer: {ex.Message}");
                if (isBusyError) await LoadDataAsync();
            }
            finally { await SetOperationState(false); }
        }

        private CustomerDTO CreateCustomerDTO(CustomerDTO source) => new()
        {
            CustomerId = source.CustomerId,
            Name = source.Name,
            Phone = source.Phone ?? string.Empty,
            Email = source.Email ?? string.Empty,
            Address = source.Address ?? string.Empty,
            IsActive = source.IsActive,
            CreatedAt = source.CustomerId == 0 ? DateTime.Now : source.CreatedAt,
            UpdatedAt = source.CustomerId != 0 ? DateTime.Now : null,
            Balance = source.Balance,
            TransactionCount = source.TransactionCount
        };

        private async Task UpdateCustomerInCollection(CustomerDTO customerToSave)
        {
            await DispatchUI(() =>
            {
                var index = Customers.ToList().FindIndex(c => c.CustomerId == customerToSave.CustomerId);
                if (index != -1)
                {
                    Customers[index] = customerToSave;
                    SelectedCustomer = customerToSave;
                }
                else _ = LoadDataAsync();
            });
        }

        private async Task DeleteAsync()
        {
            if (SelectedCustomer == null || MessageBox.Show("Are you sure you want to delete this customer?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                await SetOperationState(true);
                var customerIdToDelete = SelectedCustomer.CustomerId;

                try
                {
                    await ExecuteDbOperation(() => _customerService.DeleteAsync(customerIdToDelete));
                    await DispatchUI(() =>
                    {
                        var customerToRemove = Customers.FirstOrDefault(c => c.CustomerId == customerIdToDelete);
                        if (customerToRemove != null) Customers.Remove(customerToRemove);
                        SelectedCustomer = null;
                        MessageBox.Show("Customer deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                    await LoadDataAsync();
                }
                catch (InvalidOperationException ex)
                {
                    await DispatchUI(() => MessageBox.Show(ex.Message, "Cannot Delete Customer", MessageBoxButton.OK, MessageBoxImage.Warning));
                    if (MessageBox.Show("Would you like to mark this customer as inactive instead?", "Mark Inactive", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        SelectedCustomer = Customers.FirstOrDefault(c => c.CustomerId == customerIdToDelete);
                        if (SelectedCustomer != null)
                        {
                            SelectedCustomer.IsActive = false;
                            await SaveAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessages = new[] { "second operation", "concurrency", "affect", "modified", "deleted" };
                var isKnownError = errorMessages.Any(msg => ex.Message.Contains(msg));
                await ShowError(isKnownError ? "The customer may have been modified or deleted by another user.\nThe customer list will be refreshed." : $"Error deleting customer: {ex.Message}");
                await LoadDataAsync();
            }
            finally { await SetOperationState(false); }
        }

        private async Task ShowPaymentDialog()
        {
            if (SelectedCustomer?.Balance <= 0)
            {
                await ShowError("Customer has no balance to pay.");
                return;
            }

            try
            {
                var refreshedCustomer = await _customerService.GetByIdAsync(SelectedCustomer.CustomerId);
                if (refreshedCustomer != null) SelectedCustomer = refreshedCustomer;
                PaymentAmount = SelectedCustomer.Balance;
                await DispatchUI(() => new PaymentWindow(this).ShowDialog());
            }
            catch (Exception ex) { await ShowError($"Error preparing payment: {ex.Message}"); }
        }

        private void ClosePaymentDialog() => (IsPaymentDialogOpen, PaymentAmount) = (false, 0);

        private async Task ProcessPayment()
        {
            if (SelectedCustomer == null || PaymentAmount <= 0)
            {
                await ShowError("Invalid payment details.");
                return;
            }

            if (PaymentAmount > SelectedCustomer.Balance)
            {
                await ShowError("Payment amount cannot exceed customer's balance.");
                return;
            }

            try
            {
                await SetOperationState(true);
                var reference = $"DEBT-{DateTime.Now:yyyyMMddHHmmss}";
                var paymentTask = _customerService.ProcessPaymentAsync(SelectedCustomer.CustomerId, PaymentAmount, reference);
                var completedTask = await Task.WhenAny(paymentTask, Task.Delay(10000));

                if (completedTask != paymentTask)
                {
                    await ShowError("Payment processing timed out. Please try again.");
                    return;
                }

                if (await paymentTask)
                {
                    await Task.Delay(200);
                    ClosePaymentDialog();
                    await ForceDataRefresh();
                    await DispatchUI(() => MessageBox.Show("Payment processed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information));
                }
                else await ShowError("Payment could not be processed. Please try again.");
            }
            catch (Exception ex) { await ShowError($"Error processing payment: {ex.Message}"); }
            finally { await SetOperationState(false); }
        }

        private async Task ForceDataRefresh()
        {
            try
            {
                if (_operationInProgress)
                {
                    _operationInProgress = false;
                    if (_operationLock.CurrentCount == 0) _operationLock.Release();
                }

                while (_operationLock.CurrentCount == 0) _operationLock.Release();

                var refreshedCustomers = await _customerService.GetAllAsync();
                await DispatchUI(() =>
                {
                    var selectedId = SelectedCustomer?.CustomerId;
                    Customers = new ObservableCollection<CustomerDTO>(refreshedCustomers);
                    if (selectedId.HasValue) SelectedCustomer = Customers.FirstOrDefault(c => c.CustomerId == selectedId.Value);
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Error during force refresh: {ex.Message}"); }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnsubscribeFromEvents();
                _operationLock?.Dispose();
                Customers?.Clear();
                _pendingUIUpdates?.Clear();
            }
        }
    }
}