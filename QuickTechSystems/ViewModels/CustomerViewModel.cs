using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Printing;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class CustomerViewModel : ViewModelBase, IDisposable
    {
        private readonly ICustomerService _customerService;
        private readonly IProductService _productService;
        private readonly ITransactionService _transactionService;
        private ObservableCollection<CustomerDTO> _customers;
        private CustomerDTO? _selectedCustomer;
        private bool _isEditing;
        private string _searchText = string.Empty;
        private Action<EntityChangedEvent<CustomerDTO>> _customerChangedHandler;
        private bool _isProductPricesDialogOpen;
        private ObservableCollection<CustomerProductPriceViewModel> _customerProducts;
        private bool _isSaving;
        private bool _isCustomerPopupOpen;
        private ExpenseDTO _currentExpense = new ExpenseDTO();
        private bool _isNewExpense;
        private bool _isNewCustomer;
        private FlowDirection _currentFlowDirection = FlowDirection.LeftToRight;
        private decimal _paymentAmount;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private bool _operationInProgress = false;
        private bool _useDateFilter = true;
        private string _errorMessage = string.Empty;
        private bool _hasErrors;
        private bool _isPaymentDialogOpen;
        private DateTime _filterStartDate = DateTime.Now.AddDays(-7);
        private DateTime _filterEndDate = DateTime.Now;
        private ObservableCollection<TransactionDTO> _paymentHistory = new ObservableCollection<TransactionDTO>();
        private bool _isPaymentHistoryVisible;
      
        private TransactionDTO _selectedTransaction;
        private decimal _originalPaymentAmount;
        private decimal _newPaymentAmount;
        private string _paymentUpdateReason = string.Empty;
        public bool IsNotSaving => !IsSaving;
        private string _productSearchText = string.Empty;
        private ObservableCollection<CustomerProductPriceViewModel> _filteredCustomerProducts;
        public ObservableCollection<CustomerProductPriceViewModel> FilteredCustomerProducts
        {
            get => _filteredCustomerProducts ?? CustomerProducts;
            set => SetProperty(ref _filteredCustomerProducts, value);
        }
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (SetProperty(ref _productSearchText, value))
                {
                    FilterCustomerProducts();
                }
            }
        }
        public TransactionDTO SelectedTransaction
        {
            get => _selectedTransaction;
            set => SetProperty(ref _selectedTransaction, value);
        }

        public decimal OriginalPaymentAmount
        {
            get => _originalPaymentAmount;
            set => SetProperty(ref _originalPaymentAmount, value);
        }

        public decimal NewPaymentAmount
        {
            get => _newPaymentAmount;
            set => SetProperty(ref _newPaymentAmount, value);
        }

        public string PaymentUpdateReason
        {
            get => _paymentUpdateReason;
            set => SetProperty(ref _paymentUpdateReason, value);
        }
        #region Properties
        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                SetProperty(ref _isSaving, value);
                OnPropertyChanged(nameof(IsNotSaving));
            }
        }


        public bool IsProductPricesDialogOpen
        {
            get => _isProductPricesDialogOpen;
            set => SetProperty(ref _isProductPricesDialogOpen, value);
        }

        public bool IsCustomerPopupOpen
        {
            get => _isCustomerPopupOpen;
            set => SetProperty(ref _isCustomerPopupOpen, value);
        }

        public bool IsNewCustomer
        {
            get => _isNewCustomer;
            set => SetProperty(ref _isNewCustomer, value);
        }

        public ObservableCollection<CustomerProductPriceViewModel> CustomerProducts
        {
            get => _customerProducts;
            set => SetProperty(ref _customerProducts, value);
        }

        public ObservableCollection<CustomerDTO> Customers
        {
            get => _customers;
            set => SetProperty(ref _customers, value);
        }

        public CustomerDTO? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                SetProperty(ref _selectedCustomer, value);
                IsEditing = value != null;
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                _ = SearchCustomersAsync();
            }
        }

        public FlowDirection CurrentFlowDirection
        {
            get => _currentFlowDirection;
            set => SetProperty(ref _currentFlowDirection, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool HasErrors
        {
            get => _hasErrors;
            set => SetProperty(ref _hasErrors, value);
        }

        public bool IsPaymentDialogOpen
        {
            get => _isPaymentDialogOpen;
            set => SetProperty(ref _isPaymentDialogOpen, value);
        }

        public decimal PaymentAmount
        {
            get => _paymentAmount;
            set => SetProperty(ref _paymentAmount, value);
        }

        public ExpenseDTO CurrentExpense
        {
            get => _currentExpense;
            set => SetProperty(ref _currentExpense, value);
        }

        public bool IsNewExpense
        {
            get => _isNewExpense;
            set => SetProperty(ref _isNewExpense, value);
        }

        public ObservableCollection<TransactionDTO> PaymentHistory
        {
            get => _paymentHistory;
            set => SetProperty(ref _paymentHistory, value);
        }

        public bool IsPaymentHistoryVisible
        {
            get => _isPaymentHistoryVisible;
            set => SetProperty(ref _isPaymentHistoryVisible, value);
        }

        public string PaymentHistoryTitle
        {
            get
            {
                if (UseDateFilter && SelectedCustomer != null)
                {
                    return $"Payment History ({FilterStartDate:MM/dd/yyyy} - {FilterEndDate:MM/dd/yyyy})";
                }
                return "All Payment History";
            }
        }

        public string PaymentHistorySummary
        {
            get
            {
                if (PaymentHistory == null || PaymentHistory.Count == 0)
                    return "No transactions found";

                decimal totalPaid = PaymentHistory.Sum(t => t.PaidAmount);
                return $"Total Paid: {totalPaid:C2}";
            }
        }

        public DateTime FilterStartDate
        {
            get => _filterStartDate;
            set => SetProperty(ref _filterStartDate, value);
        }

        public DateTime FilterEndDate
        {
            get => _filterEndDate;
            set => SetProperty(ref _filterEndDate, value);
        }

        public bool UseDateFilter
        {
            get => _useDateFilter;
            set
            {
                if (SetProperty(ref _useDateFilter, value))
                {
                    _ = LoadPaymentHistory();
                }
            }
        }
        #endregion

        #region Commands
        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ApplyDateFilterCommand { get; }
        public ICommand UpdatePaymentCommand { get; }
        public ICommand SetProductPricesCommand { get; }
        public ICommand SaveCustomPricesCommand { get; }
        public ICommand CloseProductPricesDialogCommand { get; }
        public ICommand ResetCustomPriceCommand { get; }
        public ICommand ResetAllCustomPricesCommand { get; }
        public ICommand ProcessPaymentCommand { get; }
        public ICommand ShowPaymentDialogCommand { get; }
        public ICommand ClosePaymentDialogCommand { get; }
        public ICommand ShowPaymentHistoryCommand { get; }
        public ICommand ClosePaymentHistoryCommand { get; }
    
        public ICommand PrintPaymentHistoryCommand { get; }
        #endregion

        public CustomerViewModel(
            ICustomerService customerService,
            IProductService productService,
            ITransactionService transactionService,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _customers = new ObservableCollection<CustomerDTO>();
            _customerProducts = new ObservableCollection<CustomerProductPriceViewModel>();
            _customerChangedHandler = HandleCustomerChanged;

            ShowPaymentDialogCommand = new AsyncRelayCommand(
                async _ => await ShowPaymentDialog(),
                _ => !IsSaving && SelectedCustomer != null && SelectedCustomer.Balance > 0);

            ClosePaymentDialogCommand = new RelayCommand(
                _ => ClosePaymentDialog(),
                _ => !IsSaving);


            UpdatePaymentCommand = new AsyncRelayCommand(
    async _ => await UpdatePayment(),
    _ => !IsSaving && SelectedTransaction != null &&
         NewPaymentAmount > 0 &&
         !string.IsNullOrWhiteSpace(PaymentUpdateReason));


            ProcessPaymentCommand = new AsyncRelayCommand(
                async _ => await ProcessPayment(),
                _ => !IsSaving && PaymentAmount > 0 && SelectedCustomer != null);

            ShowPaymentHistoryCommand = new AsyncRelayCommand(
                async _ => await ShowPaymentHistory(),
                _ => SelectedCustomer != null);

            ClosePaymentHistoryCommand = new RelayCommand(
                _ => ClosePaymentHistory(),
                _ => IsPaymentHistoryVisible);

         

            ApplyDateFilterCommand = new AsyncRelayCommand(
                async _ => await LoadPaymentHistory(),
                _ => SelectedCustomer != null);

            PrintPaymentHistoryCommand = new AsyncRelayCommand(
                async _ => await PrintPaymentHistory(),
                _ => PaymentHistory != null && PaymentHistory.Count > 0);

            LoadCommand = new AsyncRelayCommand(async _ => await LoadDataAsync(), _ => !IsSaving);
            AddCommand = new RelayCommand(_ => AddNew(), _ => !IsSaving);
            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync(), _ => !IsSaving);
            DeleteCommand = new AsyncRelayCommand(async _ => await DeleteAsync(), _ => !IsSaving);
            SearchCommand = new AsyncRelayCommand(async _ => await SearchCustomersAsync(), _ => !IsSaving);
            SetProductPricesCommand = new AsyncRelayCommand(async _ => await ShowProductPricesDialog(), _ => !IsSaving && SelectedCustomer != null);
            SaveCustomPricesCommand = new AsyncRelayCommand(async _ => await SaveCustomPrices(), _ => !IsSaving);
            CloseProductPricesDialogCommand = new RelayCommand(_ => CloseProductPricesDialog(), _ => !IsSaving);
            ResetCustomPriceCommand = new RelayCommand(param => ResetCustomPrice(param as CustomerProductPriceViewModel), _ => !IsSaving);
            ResetAllCustomPricesCommand = new RelayCommand(_ => ResetAllCustomPrices(), _ => !IsSaving);

            _ = LoadDataAsync();
        }

        protected override void SubscribeToEvents()
        {
            _eventAggregator.Subscribe<EntityChangedEvent<CustomerDTO>>(_customerChangedHandler);
        }

        protected override void UnsubscribeFromEvents()
        {
            _eventAggregator.Unsubscribe<EntityChangedEvent<CustomerDTO>>(_customerChangedHandler);
        }
        public async Task UpdateCustomerDirectEdit(CustomerDTO customer)
        {
            try
            {
                if (customer == null) return;

                // Don't set IsSaving to true for direct edits to avoid UI flickering
                ErrorMessage = string.Empty;
                HasErrors = false;

                // Create a copy to avoid issues during async operation
                var customerToSave = new CustomerDTO
                {
                    CustomerId = customer.CustomerId,
                    Name = customer.Name,
                    Phone = customer.Phone ?? string.Empty,
                    Email = customer.Email ?? string.Empty,
                    Address = customer.Address ?? string.Empty,
                    IsActive = customer.IsActive,
                    CreatedAt = customer.CreatedAt,
                    UpdatedAt = DateTime.Now,
                    Balance = customer.Balance,
                    TransactionCount = customer.TransactionCount
                };

                await ExecuteDbOperationSafelyAsync(async () =>
                {
                    await _customerService.UpdateAsync(customerToSave);
                }, "Updating customer");

                // No need to refresh the UI as the binding already updated the displayed values
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating customer: {ex.Message}");
                await ShowErrorMessageAsync($"Error updating customer: {ex.Message}");

                // Refresh to revert changes if there was an error
                await LoadDataAsync();
            }
        }
        public async void EditPayment(TransactionDTO transaction)
        {
            if (transaction == null)
                return;

            SelectedTransaction = transaction;
            OriginalPaymentAmount = transaction.PaidAmount;
            NewPaymentAmount = transaction.PaidAmount; // Start with current amount
            PaymentUpdateReason = string.Empty;

            // Show the payment edit window
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var paymentEditWindow = new PaymentEditWindow(this);
                paymentEditWindow.ShowDialog();
            });
        }

        private async Task UpdatePayment()
        {
            try
            {
                if (SelectedTransaction == null || SelectedCustomer == null)
                    return;

                if (NewPaymentAmount <= 0)
                {
                    await ShowErrorMessageAsync("Payment amount must be greater than zero.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(PaymentUpdateReason))
                {
                    await ShowErrorMessageAsync("Please provide a reason for updating the payment.");
                    return;
                }

                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                Debug.WriteLine($"Starting payment update for transaction {SelectedTransaction.TransactionId}, new amount: {NewPaymentAmount}");

                try
                {
                    // Call the service method to update the payment
                    bool success = await ExecuteDbOperationSafelyAsync(async () => {
                        return await _customerService.UpdatePaymentTransactionAsync(
                            SelectedTransaction.TransactionId,
                            NewPaymentAmount,
                            PaymentUpdateReason);
                    }, "Updating payment transaction");

                    if (success)
                    {
                        Debug.WriteLine("Payment updated successfully");

                        await ForceDataRefresh();

                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                "Payment updated successfully.",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        });

                        // Refresh payment history
                        await LoadPaymentHistory();
                    }
                    else
                    {
                        Debug.WriteLine("Payment update returned false");
                        await ShowErrorMessageAsync("Payment update could not be processed. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception during payment update: {ex.Message}");
                    await ShowErrorMessageAsync($"Error updating payment: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in payment update method: {ex.Message}");
                await ShowErrorMessageAsync($"Error updating payment: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }
    

    
        // Add this method to use the window approach
        public void ShowCustomerDetailsWindow()
        {
            // Create and show the window 
            var detailsWindow = new CustomerDetailsWindow(this);
            bool? result = detailsWindow.ShowDialog();

            // Handle the result if needed
            if (result.HasValue && result.Value)
            {
                // Customer was saved successfully
                // No need to close popup manually as the window handles this
            }
        }

        // Modify your existing ShowCustomerPopup method to use the window approach
        public void ShowCustomerPopup()
        {
            // Instead of opening the popup, show the window
            ShowCustomerDetailsWindow();

            // Don't set this flag anymore as we're not using the popup
            // IsCustomerPopupOpen = true;
        }

        // Keep this method for backward compatibility
        public void CloseCustomerPopup()
        {
            IsCustomerPopupOpen = false;
        }
        private async void HandleCustomerChanged(EntityChangedEvent<CustomerDTO> evt)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                switch (evt.Action)
                {
                    case "Create":
                        Customers.Add(evt.Entity);
                        break;
                    case "Update":
                        var existingCustomer = Customers.FirstOrDefault(c => c.CustomerId == evt.Entity.CustomerId);
                        if (existingCustomer != null)
                        {
                            var index = Customers.IndexOf(existingCustomer);
                            if (index >= 0)
                            {
                                Customers[index] = evt.Entity;
                            }
                        }
                        break;
                    case "Delete":
                        var customerToRemove = Customers.FirstOrDefault(c => c.CustomerId == evt.Entity.CustomerId);
                        if (customerToRemove != null)
                        {
                            Customers.Remove(customerToRemove);
                        }
                        break;
                }
            });
        }

        #region Database Operations
        private async Task<T> ExecuteDbOperationSafelyAsync<T>(Func<Task<T>> operation, string operationName = "Database operation")
        {
            Debug.WriteLine($"BEGIN: {operationName}");

            // If an operation is already in progress, wait a bit
            int waitCount = 0;
            while (_operationInProgress)
            {
                waitCount++;
                Debug.WriteLine($"Operation in progress, waiting... (attempt {waitCount})");
                await Task.Delay(100);

                // Safety timeout - auto-reset after 5 seconds
                if (waitCount > 50) // 5 seconds max wait
                {
                    Debug.WriteLine("TIMEOUT waiting for operation lock, resetting lock");
                    _operationInProgress = false;
                    if (_operationLock.CurrentCount == 0)
                    {
                        _operationLock.Release();
                    }
                    break;
                }
            }

            Debug.WriteLine($"Acquiring operation lock for: {operationName}");

            // Use a timeout for acquiring the lock
            bool lockAcquired = await _operationLock.WaitAsync(5000); // 5 second timeout

            // In ExecuteDbOperationSafelyAsync method, replace this code:
            if (!lockAcquired)
            {
                Debug.WriteLine($"FAILED to acquire lock for {operationName}, forcing reset");
                // Force reset the lock
                _operationInProgress = false;

                // Instead of creating a new SemaphoreSlim, reset the existing one
                while (_operationLock.CurrentCount == 0)
                {
                    _operationLock.Release();
                }

                await _operationLock.WaitAsync();
            }

            _operationInProgress = true;

            try
            {
                Debug.WriteLine($"Executing operation: {operationName}");
                // Add a small delay to ensure any previous operation is fully complete
                await Task.Delay(200);
                var result = await operation();
                Debug.WriteLine($"Operation completed successfully: {operationName}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in {operationName}: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                await ShowErrorMessageAsync($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                _operationInProgress = false;
                _operationLock.Release();
                Debug.WriteLine($"Released operation lock for: {operationName}");
                Debug.WriteLine($"END: {operationName}");
            }
        }

        private async Task ExecuteDbOperationSafelyAsync(Func<Task> operation, string operationName = "Database operation")
        {
            await ExecuteDbOperationSafelyAsync<bool>(async () =>
            {
                await operation();
                return true;
            }, operationName);
        }

        private async Task ShowErrorMessageAsync(string message)
        {
            ErrorMessage = message;
            HasErrors = true;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (message.Contains("critical") || message.Contains("exception"))
                {
                    MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ErrorMessage == message)
                    {
                        ErrorMessage = string.Empty;
                        HasErrors = false;
                    }
                });
            });
        }
        #endregion

        #region Data Loading
        protected override async Task LoadDataAsync()
        {
            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                var customers = await ExecuteDbOperationSafelyAsync(async () =>
                {
                    return await _customerService.GetAllAsync();
                }, "Loading customers");

                int? selectedCustomerId = SelectedCustomer?.CustomerId;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Customers = new ObservableCollection<CustomerDTO>(customers);

                    if (selectedCustomerId.HasValue)
                    {
                        SelectedCustomer = Customers.FirstOrDefault(c => c.CustomerId == selectedCustomerId.Value);
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading customers: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.Message.Contains("second operation"))
                {
                    await ShowErrorMessageAsync("Database is busy. Please try again in a moment.");
                }
                else
                {
                    await ShowErrorMessageAsync($"Error loading customers: {ex.Message}");
                }
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task SearchCustomersAsync()
        {
            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                var searchTerm = SearchText;

                var customers = await ExecuteDbOperationSafelyAsync(async () =>
                {
                    return string.IsNullOrWhiteSpace(searchTerm)
                        ? await _customerService.GetAllAsync()
                        : await _customerService.GetByNameAsync(searchTerm);
                }, $"Searching customers for '{searchTerm}'");

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Customers = new ObservableCollection<CustomerDTO>(customers);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error searching customers: {ex.Message}");
                await ShowErrorMessageAsync($"Error searching customers: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }
        #endregion

        #region Customer Popup Management
     

    
        private void AddNew()
        {
            SelectedCustomer = new CustomerDTO
            {
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            IsNewCustomer = true;
            ShowCustomerPopup();
        }

        public void EditCustomer(CustomerDTO customer)
        {
            if (customer != null)
            {
                SelectedCustomer = customer;
                IsNewCustomer = false;
                ShowCustomerPopup();
            }
        }
        #endregion

        #region Save and Delete
        private async Task SaveAsync()
        {
            try
            {
                if (SelectedCustomer == null) return;

                if (string.IsNullOrWhiteSpace(SelectedCustomer.Name))
                {
                    MessageBox.Show("Customer name is required.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                // Store the customer ID to ensure it's preserved
                int customerId = SelectedCustomer.CustomerId;

                // Create a copy to avoid issues during async operation
                var customerToSave = new CustomerDTO
                {
                    CustomerId = customerId,
                    Name = SelectedCustomer.Name,
                    Phone = SelectedCustomer.Phone ?? string.Empty,
                    Email = SelectedCustomer.Email ?? string.Empty,
                    Address = SelectedCustomer.Address ?? string.Empty,
                    IsActive = SelectedCustomer.IsActive,
                    CreatedAt = customerId == 0 ? DateTime.Now : SelectedCustomer.CreatedAt,
                    UpdatedAt = customerId != 0 ? DateTime.Now : null,
                    Balance = SelectedCustomer.Balance,
                    TransactionCount = SelectedCustomer.TransactionCount
                };

                if (customerToSave.CustomerId == 0)
                {
                    // Create new customer
                    var savedCustomer = await ExecuteDbOperationSafelyAsync(async () =>
                    {
                        return await _customerService.CreateAsync(customerToSave);
                    }, "Creating new customer");

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Customers.Add(savedCustomer);
                        SelectedCustomer = savedCustomer; // Update selection to saved customer
                    });
                }
                else
                {
                    // Update existing customer
                    await ExecuteDbOperationSafelyAsync(async () =>
                    {
                        await _customerService.UpdateAsync(customerToSave);
                    }, "Updating customer");

                    // Update local collection with a more reliable method
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Find the index of the customer directly by ID
                        int index = -1;
                        for (int i = 0; i < Customers.Count; i++)
                        {
                            if (Customers[i].CustomerId == customerToSave.CustomerId)
                            {
                                index = i;
                                break;
                            }
                        }

                        if (index != -1)
                        {
                            Customers[index] = customerToSave;
                            SelectedCustomer = customerToSave; // Update current selection
                        }
                        else
                        {
                            // If we can't find the customer in the collection, refresh all data
                            LoadDataAsync();
                        }
                    });
                }

                // Close the popup automatically
                CloseCustomerPopup();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Customer saved successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving customer: {ex.Message}");

                if (ex.Message.Contains("second operation") || ex.Message.Contains("concurrency"))
                {
                    await ShowErrorMessageAsync("Database is busy. Please try again in a moment.");
                    await LoadDataAsync(); // Refresh to get latest data
                }
                else
                {
                    await ShowErrorMessageAsync($"Error saving customer: {ex.Message}");
                }
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task DeleteAsync()
        {
            try
            {
                if (SelectedCustomer == null) return;

                if (MessageBox.Show("Are you sure you want to delete this customer?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    IsSaving = true;
                    ErrorMessage = string.Empty;
                    HasErrors = false;

                    int customerIdToDelete = SelectedCustomer.CustomerId;

                    CloseCustomerPopup();

                    try
                    {
                        await ExecuteDbOperationSafelyAsync(async () =>
                        {
                            await _customerService.DeleteAsync(customerIdToDelete);
                        }, "Deleting customer");

                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var customerToRemove = Customers.FirstOrDefault(c => c.CustomerId == customerIdToDelete);
                            if (customerToRemove != null)
                            {
                                Customers.Remove(customerToRemove);
                            }

                            SelectedCustomer = null;
                        });

                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show("Customer deleted successfully.", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        });

                        await LoadDataAsync();
                    }
                    catch (InvalidOperationException ex)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(ex.Message, "Cannot Delete Customer",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        });

                        var result = MessageBox.Show("Would you like to mark this customer as inactive instead?",
                            "Mark Inactive", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
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
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting customer: {ex.Message}");

                if (ex.Message.Contains("second operation") || ex.Message.Contains("concurrency") ||
                    ex.Message.Contains("affect") || ex.Message.Contains("modified") ||
                    ex.Message.Contains("deleted"))
                {
                    await ShowErrorMessageAsync(
                        "The customer may have been modified or deleted by another user.\n" +
                        "The customer list will be refreshed.");
                }
                else
                {
                    await ShowErrorMessageAsync($"Error deleting customer: {ex.Message}");
                }

                await LoadDataAsync();
            }
            finally
            {
                IsSaving = false;
            }
        }
        #endregion

        #region Product Pricing
        // Update the existing method to use window approach
        public async Task ShowProductPricesDialog()
        {
            if (SelectedCustomer == null) return;

            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                var products = await ExecuteDbOperationSafelyAsync(async () =>
                {
                    return await _productService.GetAllAsync();
                }, "Loading products");

                var customPrices = await ExecuteDbOperationSafelyAsync(async () =>
                {
                    return await _customerService.GetCustomProductPricesAsync(SelectedCustomer.CustomerId);
                }, "Loading custom prices");

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CustomerProducts = new ObservableCollection<CustomerProductPriceViewModel>(
                        products.Select(p =>
                        {
                            var customPrice = customPrices.FirstOrDefault(cp => cp.ProductId == p.ProductId);
                            return new CustomerProductPriceViewModel
                            {
                                ProductId = p.ProductId,
                                ProductName = p.Name,
                                Barcode = p.Barcode, // Add barcode information
                                DefaultPrice = p.SalePrice,
                                CustomPrice = customPrice?.Price ?? p.SalePrice
                            };
                        }));

                    // Make sure IsSaving is false before showing the window
                    IsSaving = false;

                    // Show the window
                    var pricesWindow = new ProductPricesWindow(this);
                    pricesWindow.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading product prices: {ex.Message}");

                if (ex.Message.Contains("second operation") || ex.Message.Contains("concurrency"))
                {
                    await ShowErrorMessageAsync("Database is busy. Please try again in a moment.");
                }
                else
                {
                    await ShowErrorMessageAsync($"Error loading product prices: {ex.Message}");
                }
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void FilterCustomerProducts()
        {
            if (CustomerProducts == null)
                return;

            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                FilteredCustomerProducts = CustomerProducts;
                return;
            }

            var searchText = ProductSearchText.ToLower();
            var filtered = CustomerProducts.Where(p =>
                p.ProductName.ToLower().Contains(searchText) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(searchText))
            ).ToList();

            FilteredCustomerProducts = new ObservableCollection<CustomerProductPriceViewModel>(filtered);
        }
        // Update the existing method to use window approach
        private async Task ShowPaymentHistory()
        {
            if (SelectedCustomer == null)
                return;

            try
            {
                // Don't set IsPaymentHistoryVisible flag - we don't want popup
                // First load the payment history data
                await LoadPaymentHistory();

                // Show only the window
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var historyWindow = new PaymentHistoryWindow(this);
                    historyWindow.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing payment history: {ex.Message}");
                await ShowErrorMessageAsync($"Error loading payment history: {ex.Message}");
            }
        }

        // You may want to keep these methods for backward compatibility,
        // but they will now be empty or just call the new methods
        public void CloseProductPricesDialog()
        {
            // No longer needed with window approach
            IsProductPricesDialogOpen = false;
        }

        public void ClosePaymentHistory()
        {
            // This method might still be used elsewhere, but we don't need it
            // for the normal window closing flow
            IsPaymentHistoryVisible = false;
        }

        private async Task SaveCustomPrices()
        {
            if (SelectedCustomer == null) return;

            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                var customerId = SelectedCustomer.CustomerId;
                var prices = CustomerProducts.Select(cp => new CustomerProductPriceDTO
                {
                    CustomerId = customerId,
                    ProductId = cp.ProductId,
                    Price = cp.CustomPrice
                }).ToList();

                await ExecuteDbOperationSafelyAsync(async () =>
                {
                    await _customerService.SetCustomProductPricesAsync(customerId, prices);
                }, "Saving custom prices");

                // Set this flag to false to indicate saving is complete
                // This will trigger the window to close via PropertyChanged
                IsProductPricesDialogOpen = false;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Custom prices saved successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving custom prices: {ex.Message}");

                if (ex.Message.Contains("second operation") || ex.Message.Contains("concurrency"))
                {
                    await ShowErrorMessageAsync("Database is busy. Please try again in a moment.");
                }
                else
                {
                    await ShowErrorMessageAsync($"Error saving custom prices: {ex.Message}");
                }
            }
            finally
            {
                IsSaving = false;
            }
        }



        private void ResetCustomPrice(CustomerProductPriceViewModel price)
        {
            if (price != null)
            {
                price.CustomPrice = price.DefaultPrice;
            }
        }

        private void ResetAllCustomPrices()
        {
            if (CustomerProducts != null)
            {
                foreach (var product in CustomerProducts)
                {
                    product.CustomPrice = product.DefaultPrice;
                }
            }
        }
        #endregion

        #region Payment Management
        private async Task ShowPaymentDialog()
        {
            if (SelectedCustomer == null || SelectedCustomer.Balance <= 0)
            {
                await ShowErrorMessageAsync("Customer has no balance to pay.");
                return;
            }

            try
            {
                // First refresh the customer data to ensure we have the latest balance
                var refreshedCustomer = await _customerService.GetByIdAsync(SelectedCustomer.CustomerId);
                if (refreshedCustomer != null)
                {
                    // Update the selected customer with fresh data
                    SelectedCustomer = refreshedCustomer;
                }

                // Reset payment amount to current balance
                PaymentAmount = SelectedCustomer.Balance;

                // IMPORTANT: Don't set IsPaymentDialogOpen flag - this prevents the popup
                // The flag is only used for the popup which we don't want to show

                // Show the window directly
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var paymentWindow = new PaymentWindow(this);
                    paymentWindow.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing payment dialog: {ex.Message}");
                await ShowErrorMessageAsync($"Error preparing payment: {ex.Message}");
            }
        }

        private void ClosePaymentDialog()
        {
            IsPaymentDialogOpen = false;
            PaymentAmount = 0;
        }

        private async Task ProcessPayment()
        {
            try
            {
                if (SelectedCustomer == null)
                    return;

                if (PaymentAmount <= 0)
                {
                    await ShowErrorMessageAsync("Payment amount must be greater than zero.");
                    return;
                }

                if (PaymentAmount > SelectedCustomer.Balance)
                {
                    await ShowErrorMessageAsync("Payment amount cannot exceed customer's balance.");
                    return;
                }

                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                // Generate unique reference
                string reference = $"DEBT-{DateTime.Now:yyyyMMddHHmmss}";

                Debug.WriteLine($"Starting payment process for customer {SelectedCustomer.CustomerId}, amount: {PaymentAmount}");

                try
                {
                    // Process the payment with timeout handling
                    var paymentTask = _customerService.ProcessPaymentAsync(
                        SelectedCustomer.CustomerId,
                        PaymentAmount,
                        reference);

                    // Add timeout to prevent hanging
                    var timeoutTask = Task.Delay(10000); // 10 second timeout

                    var completedTask = await Task.WhenAny(paymentTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        Debug.WriteLine("Payment processing timed out");
                        await ShowErrorMessageAsync("Payment processing timed out. Please try again.");
                        return;
                    }

                    bool success = await paymentTask;

                    if (success)
                    {
                        Debug.WriteLine("Payment processed successfully");

                        await Task.Delay(200); // Add a small delay for UI responsiveness
                        ClosePaymentDialog();

                        await ForceDataRefresh();

                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show(
                                "Payment processed successfully.",
                                "Success",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        });
                    }
                    else
                    {
                        Debug.WriteLine("Payment processing returned false");
                        await ShowErrorMessageAsync("Payment could not be processed. Please try again.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception during payment processing: {ex.Message}");
                    await ShowErrorMessageAsync($"Error processing payment: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in payment method: {ex.Message}");
                await ShowErrorMessageAsync($"Error processing payment: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task ForceDataRefresh()
        {
            Debug.WriteLine("Forcing complete data refresh");

            try
            {
                // Clear operation lock if stuck
                if (_operationInProgress)
                {
                    _operationInProgress = false;
                    if (_operationLock.CurrentCount == 0)
                    {
                        _operationLock.Release();
                        Debug.WriteLine("Released potentially stuck operation lock");
                    }
                }

                // Instead of reassigning the readonly field, just make sure it's released
                while (_operationLock.CurrentCount == 0)
                {
                    _operationLock.Release();
                }

                // Reload customer data using direct database call
                var refreshedCustomers = await _customerService.GetAllAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Get currently selected customer ID
                    int? selectedId = SelectedCustomer?.CustomerId;

                    // Update the customers collection
                    Customers = new ObservableCollection<CustomerDTO>(refreshedCustomers);

                    // Reselect the customer if needed
                    if (selectedId.HasValue)
                    {
                        SelectedCustomer = Customers.FirstOrDefault(c => c.CustomerId == selectedId.Value);
                    }
                });

                // Reload payment history if visible
                if (IsPaymentHistoryVisible)
                {
                    await LoadPaymentHistory();
                }

                Debug.WriteLine("Data refresh completed successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during force refresh: {ex.Message}");
            }
        }

      
        private async Task LoadPaymentHistory()
        {
            if (SelectedCustomer == null)
                return;

            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                var transactions = await ExecuteDbOperationSafelyAsync(async () =>
                {
                    if (UseDateFilter)
                    {
                        DateTime endDateInclusive = FilterEndDate.AddDays(1).AddSeconds(-1);
                        return await _transactionService.GetByCustomerAndDateRangeAsync(
                            SelectedCustomer.CustomerId,
                            FilterStartDate,
                            endDateInclusive);
                    }
                    else
                    {
                        return await _transactionService.GetByCustomerAsync(SelectedCustomer.CustomerId);
                    }
                }, "Loading payment history");

                foreach (var transaction in transactions)
                {
                    if (transaction.TransactionType.ToString() == "Payment" ||
                        transaction.TransactionType.ToString() == "Adjustment")
                    {
                        if (transaction.PaidAmount <= 0)
                        {
                            transaction.PaidAmount = transaction.TotalAmount;
                        }
                    }
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PaymentHistory = new ObservableCollection<TransactionDTO>(
                        transactions.OrderByDescending(t => t.TransactionDate));

                    OnPropertyChanged(nameof(PaymentHistoryTitle));
                    OnPropertyChanged(nameof(PaymentHistorySummary));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading payment history: {ex.Message}");
                await ShowErrorMessageAsync($"Error loading payment history: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }

      
        private async Task PrintPaymentHistory()
        {
            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;
                HasErrors = false;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    PrintDialog printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() == true)
                    {
                        FlowDocument document = new FlowDocument();
                        document.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
                        document.PagePadding = new Thickness(50);

                        Paragraph title = new Paragraph(new Run(
                            $"Payment History for {SelectedCustomer.Name}"))
                        {
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 10)
                        };
                        document.Blocks.Add(title);

                        // Add current balance below the name
                        Paragraph balance = new Paragraph(new Run(
                            $"Current Balance: {SelectedCustomer.Balance:C2}"))
                        {
                            FontSize = 14,
                            FontWeight = FontWeights.SemiBold,
                            TextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 20)
                        };
                        document.Blocks.Add(balance);

                        if (UseDateFilter)
                        {
                            Paragraph dateRange = new Paragraph(new Run(
                                $"Period: {FilterStartDate:MM/dd/yyyy} - {FilterEndDate:MM/dd/yyyy}"))
                            {
                                FontSize = 12,
                                TextAlignment = TextAlignment.Center,
                                Margin = new Thickness(0, 0, 0, 20)
                            };
                            document.Blocks.Add(dateRange);
                        }

                        Table table = new Table();
                        table.CellSpacing = 0;
                        table.BorderBrush = Brushes.Black;
                        table.BorderThickness = new Thickness(1);

                        table.Columns.Add(new TableColumn() { Width = new GridLength(120) }); // Date
                        table.Columns.Add(new TableColumn() { Width = new GridLength(100) }); // Trx #
                        table.Columns.Add(new TableColumn() { Width = new GridLength(100) }); // Paid Amount

                        TableRowGroup headerRowGroup = new TableRowGroup();
                        TableRow headerRow = new TableRow();
                        headerRow.Background = new SolidColorBrush(Colors.LightGray);

                        headerRow.Cells.Add(CreateHeaderCell("Date"));
                        headerRow.Cells.Add(CreateHeaderCell("Trx #"));
                        headerRow.Cells.Add(CreateHeaderCell("Paid Amount"));

                        headerRowGroup.Rows.Add(headerRow);
                        table.RowGroups.Add(headerRowGroup);

                        TableRowGroup dataRowGroup = new TableRowGroup();

                        foreach (var transaction in PaymentHistory)
                        {
                            TableRow row = new TableRow();

                            row.Cells.Add(CreateCell(transaction.TransactionDate.ToString("MM/dd/yyyy HH:mm")));
                            row.Cells.Add(CreateCell(transaction.TransactionId.ToString()));
                            row.Cells.Add(CreateCell(transaction.PaidAmount.ToString("C2"), TextAlignment.Right));

                            dataRowGroup.Rows.Add(row);
                        }

                        table.RowGroups.Add(dataRowGroup);
                        document.Blocks.Add(table);

                        Paragraph summary = new Paragraph(new Run(PaymentHistorySummary))
                        {
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            TextAlignment = TextAlignment.Right,
                            Margin = new Thickness(0, 20, 0, 0)
                        };
                        document.Blocks.Add(summary);

                        Paragraph footer = new Paragraph(new Run(
                            $"Printed on {DateTime.Now:MM/dd/yyyy HH:mm:ss}"))
                        {
                            FontSize = 10,
                            TextAlignment = TextAlignment.Right,
                            Margin = new Thickness(0, 40, 0, 0)
                        };
                        document.Blocks.Add(footer);

                        IDocumentPaginatorSource paginatorSource = document;
                        printDialog.PrintDocument(paginatorSource.DocumentPaginator, "Payment History");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error printing payment history: {ex.Message}");
                await ShowErrorMessageAsync($"Error printing payment history: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        private TableCell CreateHeaderCell(string text)
        {
            TableCell cell = new TableCell();
            Paragraph p = new Paragraph(new Run(text));
            p.FontWeight = FontWeights.Bold;
            cell.Blocks.Add(p);
            cell.BorderBrush = Brushes.Black;
            cell.BorderThickness = new Thickness(1);
            cell.Padding = new Thickness(5);
            return cell;
        }

        private TableCell CreateCell(string text, TextAlignment alignment = TextAlignment.Left)
        {
            TableCell cell = new TableCell();
            Paragraph p = new Paragraph(new Run(text));
            p.TextAlignment = alignment;
            cell.Blocks.Add(p);
            cell.BorderBrush = Brushes.Black;
            cell.BorderThickness = new Thickness(1);
            cell.Padding = new Thickness(5);
            return cell;
        }
        #endregion

        #region IDisposable Implementation
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
                CustomerProducts?.Clear();
                PaymentHistory?.Clear();
            }
        }
        #endregion
    }
}