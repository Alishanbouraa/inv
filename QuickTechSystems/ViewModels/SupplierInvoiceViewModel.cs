// Path: QuickTechSystems.WPF.ViewModels/SupplierInvoiceViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class SupplierInvoiceViewModel : ViewModelBase
    {
        private readonly ISupplierInvoiceService _supplierInvoiceService;
        private readonly ISupplierService _supplierService;
       private ObservableCollection<SupplierInvoiceDTO> _invoices;
        private SupplierInvoiceDTO? _selectedInvoice;
        private ObservableCollection<SupplierDTO> _suppliers;
        private readonly Action<EntityChangedEvent<SupplierDTO>> _supplierChangedHandler;
        private SupplierDTO? _selectedSupplier;
        private string _invoiceNumber = string.Empty;
        private DateTime _invoiceDate = DateTime.Now;
        private decimal _totalAmount;
        private string _notes = string.Empty;
        private bool _isSaving;
        private string _errorMessage = string.Empty;
        private bool _isInvoicePopupOpen;
        private bool _isProductSelectionPopupOpen;
        private bool _isInvoiceDetailPopupOpen;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;
        private readonly Action<EntityChangedEvent<SupplierInvoiceDTO>> _invoiceChangedHandler;
        private ProductDTO? _selectedProduct;
        private ObservableCollection<ProductDTO> _products;
        private decimal _quantity = 1;
        private decimal _purchasePrice;
        private SupplierInvoiceDetailDTO? _selectedDetail;
        private string _searchQuery = string.Empty;
        private ObservableCollection<ProductDTO> _filteredProducts;
        private string _statusFilter = "All";
        private CancellationTokenSource _cts;
        private bool _isPaymentPopupOpen;
        private decimal _paymentAmount;
        private ObservableCollection<SupplierTransactionDTO> _invoicePayments;
        private bool _isPaymentHistoryPopupOpen;
        public bool IsPaymentPopupOpen
        {
            get => _isPaymentPopupOpen;
            set => SetProperty(ref _isPaymentPopupOpen, value);
        }

        public decimal PaymentAmount
        {
            get => _paymentAmount;
            set => SetProperty(ref _paymentAmount, value);
        }

        public ObservableCollection<SupplierTransactionDTO> InvoicePayments
        {
            get => _invoicePayments;
            set => SetProperty(ref _invoicePayments, value);
        }

        public bool IsPaymentHistoryPopupOpen
        {
            get => _isPaymentHistoryPopupOpen;
            set => SetProperty(ref _isPaymentHistoryPopupOpen, value);
        }

        public ObservableCollection<SupplierInvoiceDTO> Invoices
        {
            get => _invoices;
            set => SetProperty(ref _invoices, value);
        }

        public SupplierInvoiceDTO? SelectedInvoice
        {
            get => _selectedInvoice;
            set => SetProperty(ref _selectedInvoice, value);
        }

        public ObservableCollection<SupplierDTO> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }

        public SupplierDTO? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set => SetProperty(ref _invoiceDate, value);
        }

        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value);
        }

        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsInvoicePopupOpen
        {
            get => _isInvoicePopupOpen;
            set => SetProperty(ref _isInvoicePopupOpen, value);
        }

        public bool IsProductSelectionPopupOpen
        {
            get => _isProductSelectionPopupOpen;
            set => SetProperty(ref _isProductSelectionPopupOpen, value);
        }

        public bool IsInvoiceDetailPopupOpen
        {
            get => _isInvoiceDetailPopupOpen;
            set => SetProperty(ref _isInvoiceDetailPopupOpen, value);
        }

        public ProductDTO? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value) && value != null)
                {
                    PurchasePrice = value.PurchasePrice;
                }
            }
        }

        public ObservableCollection<ProductDTO> Products
        {
            get => _products;
            set => SetProperty(ref _products, value);
        }

        public ObservableCollection<ProductDTO> FilteredProducts
        {
            get => _filteredProducts;
            set => SetProperty(ref _filteredProducts, value);
        }

        public decimal Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set => SetProperty(ref _purchasePrice, value);
        }

        public SupplierInvoiceDetailDTO? SelectedDetail
        {
            get => _selectedDetail;
            set => SetProperty(ref _selectedDetail, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    FilterProducts();
                }
            }
        }

        public string StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (SetProperty(ref _statusFilter, value))
                {
                    _ = LoadInvoicesAsync();
                }
            }
        }

        public ObservableCollection<string> StatusFilterOptions { get; } =
            new ObservableCollection<string> { "All", "Draft", "Validated", "Settled" };

        public ICommand LoadCommand { get; }
        public ICommand AddInvoiceCommand { get; }
        public ICommand SaveInvoiceCommand { get; }
        public ICommand DeleteInvoiceCommand { get; }
        public ICommand AddProductCommand { get; }
        public ICommand SaveProductCommand { get; }
        public ICommand RemoveProductCommand { get; }
        public ICommand ValidateInvoiceCommand { get; }
        public ICommand SettleInvoiceCommand { get; }
        public ICommand ViewInvoiceDetailsCommand { get; }
        public ICommand MakePaymentCommand { get; }
        public ICommand ShowPaymentHistoryCommand { get; }
        public SupplierInvoiceViewModel(
    ISupplierInvoiceService supplierInvoiceService,
    ISupplierService supplierService,
     IEventAggregator eventAggregator) : base(eventAggregator)
        {

            _supplierInvoiceService = supplierInvoiceService;
            _supplierService = supplierService;
            _invoices = new ObservableCollection<SupplierInvoiceDTO>();
            _suppliers = new ObservableCollection<SupplierDTO>();
            _products = new ObservableCollection<ProductDTO>();
            _filteredProducts = new ObservableCollection<ProductDTO>();
            _supplierChangedHandler = HandleSupplierChanged;
            _invoiceChangedHandler = HandleInvoiceChanged;
            _cts = new CancellationTokenSource();
            MakePaymentCommand = new AsyncRelayCommand(async _ => await MakePaymentAsync());
            ShowPaymentHistoryCommand = new RelayCommand(_ => ShowPaymentHistory());
            _invoicePayments = new ObservableCollection<SupplierTransactionDTO>();
            LoadCommand = new AsyncRelayCommand(async _ => await LoadDataAsync());
            AddInvoiceCommand = new RelayCommand(_ => AddInvoice());
            SaveInvoiceCommand = new AsyncRelayCommand(async _ => await SaveInvoiceAsync());
            DeleteInvoiceCommand = new AsyncRelayCommand(async _ => await DeleteInvoiceAsync());
            AddProductCommand = new RelayCommand(_ => AddProduct());
            SaveProductCommand = new AsyncRelayCommand(async _ => await SaveProductAsync());
            RemoveProductCommand = new AsyncRelayCommand(async _ => await RemoveProductAsync());
            ValidateInvoiceCommand = new AsyncRelayCommand(async _ => await ValidateInvoiceAsync());
            SettleInvoiceCommand = new AsyncRelayCommand(async _ => await SettleInvoiceAsync());
            ViewInvoiceDetailsCommand = new RelayCommand(_ => ViewInvoiceDetails());

            _ = LoadDataAsync();
        }
        protected override void SubscribeToEvents()
        {
            // Keep existing subscriptions
            _eventAggregator.Subscribe<EntityChangedEvent<SupplierInvoiceDTO>>(_invoiceChangedHandler);

            // Add this new subscription
            _eventAggregator.Subscribe<EntityChangedEvent<SupplierDTO>>(_supplierChangedHandler);
        }


        protected override void UnsubscribeFromEvents()
        {
            // Keep existing unsubscriptions
            _eventAggregator.Unsubscribe<EntityChangedEvent<SupplierInvoiceDTO>>(_invoiceChangedHandler);

            // Add this new unsubscription
            _eventAggregator.Unsubscribe<EntityChangedEvent<SupplierDTO>>(_supplierChangedHandler);
        }


        private async void HandleInvoiceChanged(EntityChangedEvent<SupplierInvoiceDTO> evt)
        {
            try
            {
                Debug.WriteLine($"SupplierInvoiceViewModel: Handling invoice change: {evt.Action}");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    switch (evt.Action)
                    {
                        case "Create":
                            if (ShouldDisplayInvoice(evt.Entity))
                            {
                                Invoices.Add(evt.Entity);
                                Debug.WriteLine($"Added new invoice {evt.Entity.InvoiceNumber}");
                            }
                            break;
                        case "Update":
                            var existingIndex = Invoices.ToList().FindIndex(i => i.SupplierInvoiceId == evt.Entity.SupplierInvoiceId);
                            if (existingIndex != -1)
                            {
                                if (ShouldDisplayInvoice(evt.Entity))
                                {
                                    Invoices[existingIndex] = evt.Entity;
                                    Debug.WriteLine($"Updated invoice {evt.Entity.InvoiceNumber}");
                                }
                                else
                                {
                                    Invoices.RemoveAt(existingIndex);
                                    Debug.WriteLine($"Removed invoice {evt.Entity.InvoiceNumber} due to filter");
                                }
                            }
                            else if (ShouldDisplayInvoice(evt.Entity))
                            {
                                Invoices.Add(evt.Entity);
                                Debug.WriteLine($"Added invoice {evt.Entity.InvoiceNumber} after update");
                            }

                            // If this is the selected invoice, update it
                            if (SelectedInvoice?.SupplierInvoiceId == evt.Entity.SupplierInvoiceId)
                            {
                                SelectedInvoice = evt.Entity;
                            }
                            break;
                        case "Delete":
                            var invoiceToRemove = Invoices.FirstOrDefault(i => i.SupplierInvoiceId == evt.Entity.SupplierInvoiceId);
                            if (invoiceToRemove != null)
                            {
                                Invoices.Remove(invoiceToRemove);
                                Debug.WriteLine($"Removed invoice {invoiceToRemove.InvoiceNumber}");
                            }
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SupplierInvoiceViewModel: Error handling invoice change: {ex.Message}");
            }
        }

        private async void HandleSupplierChanged(EntityChangedEvent<SupplierDTO> evt)
        {
            try
            {
                Debug.WriteLine($"SupplierInvoiceViewModel: Handling supplier change: {evt.Action}");

                // Only handle active suppliers since that's what we display in dropdowns
                if (evt.Entity.IsActive)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        switch (evt.Action)
                        {
                            case "Create":
                                // Add the new supplier to our collection if it's not already there
                                if (!Suppliers.Any(s => s.SupplierId == evt.Entity.SupplierId))
                                {
                                    Suppliers.Add(evt.Entity);
                                    Debug.WriteLine($"Added new supplier {evt.Entity.Name} to invoice view");
                                }
                                break;

                            case "Update":
                                var existingIndex = Suppliers.ToList().FindIndex(s => s.SupplierId == evt.Entity.SupplierId);
                                if (existingIndex != -1)
                                {
                                    // Update the existing supplier
                                    Suppliers[existingIndex] = evt.Entity;
                                    Debug.WriteLine($"Updated supplier {evt.Entity.Name} in invoice view");
                                }
                                else
                                {
                                    // This is a supplier that wasn't in our list but is now active
                                    Suppliers.Add(evt.Entity);
                                    Debug.WriteLine($"Added updated supplier {evt.Entity.Name} to invoice view");
                                }
                                break;

                            case "Delete":
                                var supplierToRemove = Suppliers.FirstOrDefault(s => s.SupplierId == evt.Entity.SupplierId);
                                if (supplierToRemove != null)
                                {
                                    Suppliers.Remove(supplierToRemove);
                                    Debug.WriteLine($"Removed supplier {supplierToRemove.Name} from invoice view");
                                }
                                break;
                        }
                    });
                }
                else if (evt.Action == "Update")
                {
                    // If supplier was set to inactive, remove it from our list
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var supplierToRemove = Suppliers.FirstOrDefault(s => s.SupplierId == evt.Entity.SupplierId);
                        if (supplierToRemove != null)
                        {
                            Suppliers.Remove(supplierToRemove);
                            Debug.WriteLine($"Removed inactive supplier {supplierToRemove.Name} from invoice view");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SupplierInvoiceViewModel: Error handling supplier change: {ex.Message}");
            }
        }
        private bool ShouldDisplayInvoice(SupplierInvoiceDTO invoice)
        {
            if (StatusFilter == "All")
                return true;

            return invoice.Status == StatusFilter;
        }

        protected override async Task LoadDataAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                // Already executing an operation, skip this one
                Debug.WriteLine("LoadDataAsync skipped - already in progress");
                return;
            }

            // Create a new CancellationTokenSource for this operation
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;

                await Task.WhenAll(
                    LoadSuppliersAsync(),
                    LoadInvoicesAsync()
                );
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Operation was canceled");
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading data: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                _operationLock.Release();
            }
        }

        private async Task LoadSuppliersAsync()
        {
            try
            {
                var suppliers = await _supplierService.GetActiveAsync();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Suppliers = new ObservableCollection<SupplierDTO>(suppliers);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading suppliers: {ex.Message}");
                throw;
            }
        }

     

        private async Task LoadInvoicesAsync()
        {
            try
            {
                IEnumerable<SupplierInvoiceDTO> invoices;

                if (StatusFilter == "All")
                {
                    invoices = await _supplierInvoiceService.GetAllAsync();
                }
                else
                {
                    invoices = await _supplierInvoiceService.GetByStatusAsync(StatusFilter);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Invoices = new ObservableCollection<SupplierInvoiceDTO>(invoices);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading invoices: {ex.Message}");
                throw;
            }
        }

        private void FilterProducts()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredProducts = new ObservableCollection<ProductDTO>(Products);
                return;
            }

            var query = SearchQuery.ToLower();
            var filtered = Products.Where(p =>
                p.Name.ToLower().Contains(query) ||
                p.Barcode.ToLower().Contains(query) ||
                p.CategoryName.ToLower().Contains(query) ||
                (p.Description?.ToLower()?.Contains(query) ?? false)
            ).ToList();

            FilteredProducts = new ObservableCollection<ProductDTO>(filtered);
        }

        private void AddInvoice()
        {
            // Refresh supplier list before opening dialog
            Task.Run(async () =>
            {
                try
                {
                    await LoadSuppliersAsync();

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ResetInvoiceFields();
                        var createWindow = new SupplierInvoiceCreateWindow(this);
                        createWindow.ShowDialog();
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error refreshing suppliers: {ex.Message}");
                    // Still show the dialog even if refresh fails
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ResetInvoiceFields();
                        var createWindow = new SupplierInvoiceCreateWindow(this);
                        createWindow.ShowDialog();
                    });
                }
            });
        }
        private void ResetInvoiceFields()
        {
            SelectedSupplier = null;
            InvoiceNumber = string.Empty;
            InvoiceDate = DateTime.Now;
            TotalAmount = 0;
            Notes = string.Empty;
        }

        private async Task SaveInvoiceAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Save operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;

                if (SelectedSupplier == null)
                {
                    ShowTemporaryErrorMessage("Please select a supplier.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(InvoiceNumber))
                {
                    ShowTemporaryErrorMessage("Invoice number is required.");
                    return;
                }

                if (TotalAmount <= 0)
                {
                    ShowTemporaryErrorMessage("Total amount must be greater than zero.");
                    return;
                }

                // Check for duplicate invoice number
                var existingInvoice = await _supplierInvoiceService.GetByInvoiceNumberAsync(InvoiceNumber, SelectedSupplier.SupplierId);
                if (existingInvoice != null)
                {
                    ShowTemporaryErrorMessage($"Invoice number {InvoiceNumber} already exists for this supplier.");
                    return;
                }

                var invoice = new SupplierInvoiceDTO
                {
                    SupplierId = SelectedSupplier.SupplierId,
                    SupplierName = SelectedSupplier.Name,
                    InvoiceNumber = InvoiceNumber,
                    InvoiceDate = InvoiceDate,
                    TotalAmount = TotalAmount,
                    CalculatedAmount = 0, // Will be updated as products are added
                    Status = "Draft",
                    Notes = Notes,
                    CreatedAt = DateTime.Now
                };

                var savedInvoice = await _supplierInvoiceService.CreateAsync(invoice);

                IsInvoicePopupOpen = false;
                SelectedInvoice = savedInvoice;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Invoice created successfully. You can now add products to this invoice.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error saving invoice: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                _operationLock.Release();
            }
        }

        private async Task DeleteInvoiceAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Delete operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedInvoice == null) return;

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show($"Are you sure you want to delete invoice {SelectedInvoice.InvoiceNumber}?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    IsSaving = true;
                    ErrorMessage = string.Empty;

                    await _supplierInvoiceService.DeleteAsync(SelectedInvoice.SupplierInvoiceId);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Invoice deleted successfully.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });

                    SelectedInvoice = null;
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error deleting invoice: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                _operationLock.Release();
            }
        }

        private void AddProduct()
        {
            if (SelectedInvoice == null)
            {
                ShowTemporaryErrorMessage("Please select an invoice first.");
                return;
            }

            if (SelectedInvoice.Status != "Draft")
            {
                ShowTemporaryErrorMessage($"Cannot add products to invoice in {SelectedInvoice.Status} status.");
                return;
            }

            ShowTemporaryErrorMessage("Please add products from the Products tab by selecting this invoice when creating or editing a product.");
            return;

            // The code below is now unused since we're adding products from the ProductDetailsWindow
            // SelectedProduct = null;
            // Quantity = 1;
            // PurchasePrice = 0;
            // SearchQuery = string.Empty;
            // FilterProducts();
            // IsProductSelectionPopupOpen = true;
        }

        private async Task SaveProductAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Save operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsSaving = true;
                ErrorMessage = string.Empty;

                if (SelectedInvoice == null)
                {
                    ShowTemporaryErrorMessage("No invoice selected.");
                    return;
                }

                if (SelectedProduct == null)
                {
                    ShowTemporaryErrorMessage("Please select a product.");
                    return;
                }

                if (Quantity <= 0)
                {
                    ShowTemporaryErrorMessage("Quantity must be greater than zero.");
                    return;
                }

                if (PurchasePrice <= 0)
                {
                    ShowTemporaryErrorMessage("Purchase price must be greater than zero.");
                    return;
                }

                var detail = new SupplierInvoiceDetailDTO
                {
                    SupplierInvoiceId = SelectedInvoice.SupplierInvoiceId,
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.Name,
                    ProductBarcode = SelectedProduct.Barcode,
                    Quantity = Quantity,
                    PurchasePrice = PurchasePrice,
                    TotalPrice = Quantity * PurchasePrice
                };

                await _supplierInvoiceService.AddProductToInvoiceAsync(detail);

                // Refresh the invoice to show the updated details
                SelectedInvoice = await _supplierInvoiceService.GetByIdAsync(SelectedInvoice.SupplierInvoiceId);

                IsProductSelectionPopupOpen = false;

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show("Product added to invoice successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error adding product to invoice: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                _operationLock.Release();
            }
        }

        private async Task RemoveProductAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Remove operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedInvoice == null || SelectedDetail == null) return;

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show($"Are you sure you want to remove {SelectedDetail.ProductName} from this invoice?",
                        "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    IsSaving = true;
                    ErrorMessage = string.Empty;

                    await _supplierInvoiceService.RemoveProductFromInvoiceAsync(SelectedDetail.SupplierInvoiceDetailId);

                    // Refresh the invoice to show the updated details
                    SelectedInvoice = await _supplierInvoiceService.GetByIdAsync(SelectedInvoice.SupplierInvoiceId);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Product removed from invoice successfully.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error removing product from invoice: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                _operationLock.Release();
            }
        }
        private IEnumerable<SupplierTransactionDTO> EnsureCollection(IEnumerable<SupplierTransactionDTO> collection)
        {
            // If the collection is null, return an empty list instead
            return collection ?? new List<SupplierTransactionDTO>();
        }
        private async Task ValidateInvoiceAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Validate operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedInvoice == null) return;

                if (SelectedInvoice.Details.Count == 0)
                {
                    ShowTemporaryErrorMessage("Cannot validate an invoice with no products.");
                    return;
                }

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var message = "Are you sure you want to validate this invoice?\n\n" +
                                 $"Invoice total: {SelectedInvoice.TotalAmount:C}\n" +
                                 $"Calculated total: {SelectedInvoice.CalculatedAmount:C}";

                    if (SelectedInvoice.HasDiscrepancy)
                    {
                        message += $"\n\nDiscrepancy: {SelectedInvoice.Difference:C}";
                    }

                    return MessageBox.Show(message, "Confirm Validation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    IsSaving = true;
                    ErrorMessage = string.Empty;

                    await _supplierInvoiceService.ValidateInvoiceAsync(SelectedInvoice.SupplierInvoiceId);

                    // Refresh the invoice
                    SelectedInvoice = await _supplierInvoiceService.GetByIdAsync(SelectedInvoice.SupplierInvoiceId);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Invoice validated successfully.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error validating invoice: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                _operationLock.Release();
            }
        }

        private async Task SettleInvoiceAsync()
        {
            Debug.WriteLine("Starting SettleInvoiceAsync method");
            try
            {
                if (!await _operationLock.WaitAsync(0))
                {
                    ShowTemporaryErrorMessage("Settle operation already in progress. Please wait.");
                    return;
                }

                try
                {
                    if (SelectedInvoice == null)
                    {
                        ShowTemporaryErrorMessage("No invoice selected. Please select an invoice first.");
                        return;
                    }

                    Debug.WriteLine($"Getting payment history for invoice {SelectedInvoice.SupplierInvoiceId}");

                    // Get payment history to calculate remaining amount
                    IEnumerable<SupplierTransactionDTO> paymentsResult = null;
                    try
                    {
                        paymentsResult = await _supplierInvoiceService.GetInvoicePaymentsAsync(SelectedInvoice.SupplierInvoiceId);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting payments: {ex.Message}");
                        paymentsResult = new List<SupplierTransactionDTO>();
                    }

                    // Safety check - ensure we have a non-null collection
                    if (paymentsResult == null)
                    {
                        paymentsResult = new List<SupplierTransactionDTO>();
                    }

                    InvoicePayments = new ObservableCollection<SupplierTransactionDTO>(paymentsResult);

                    Debug.WriteLine($"Found {InvoicePayments.Count} existing payments");

                    // Store the current invoice ID to handle possible changes during async operation
                    int currentInvoiceId = SelectedInvoice.SupplierInvoiceId;

                    // Ensure UI operations occur on UI thread
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            Debug.WriteLine("Creating payment window");

                            // Create the window
                            var paymentWindow = new QuickTechSystems.WPF.Views.SettlePaymentSupplierWindow(
                                SelectedInvoice,  // Pass invoice 
                                _supplierInvoiceService,  // Pass service
                                InvoicePayments);  // Pass payments collection

                            Debug.WriteLine("Showing payment window");

                            // Show the window and get result - make sure it's handled on UI thread
                            bool? result = paymentWindow.ShowDialog();
                            Debug.WriteLine($"Payment window dialog result: {result}");

                            if (result == true)
                            {
                                Debug.WriteLine("Payment was processed, refreshing data");

                                // Refresh invoices in a separate try-catch block
                                try
                                {
                                    await LoadInvoicesAsync();
                                }
                                catch (Exception refreshEx)
                                {
                                    Debug.WriteLine($"Error refreshing invoices: {refreshEx.Message}");
                                }

                                // Update the selected invoice in a separate try-catch block 
                                try
                                {
                                    if (SelectedInvoice != null && SelectedInvoice.SupplierInvoiceId == currentInvoiceId)
                                    {
                                        var refreshedInvoice = await _supplierInvoiceService.GetByIdAsync(currentInvoiceId);
                                        if (refreshedInvoice != null)
                                        {
                                            SelectedInvoice = refreshedInvoice;
                                            Debug.WriteLine("Successfully refreshed selected invoice");
                                        }
                                    }
                                }
                                catch (Exception refreshEx)
                                {
                                    Debug.WriteLine($"Error refreshing selected invoice: {refreshEx.Message}");
                                }
                            }
                        }
                        catch (Exception windowEx)
                        {
                            Debug.WriteLine($"Error with payment window: {windowEx}");
                            MessageBox.Show($"Error showing payment window: {windowEx.Message}",
                                "Window Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                }
                finally
                {
                    // Always release the lock to prevent deadlocks
                    _operationLock.Release();
                    Debug.WriteLine("Released operation lock in SettleInvoiceAsync");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SettleInvoiceAsync: Unhandled exception: {ex}");
                MessageBox.Show($"An unexpected error occurred while processing the payment. Please try again.",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task MakePaymentAsync()
        {
            try
            {
                if (!await _operationLock.WaitAsync(0))
                {
                    ShowTemporaryErrorMessage("Payment operation already in progress. Please wait.");
                    return;
                }

                try
                {
                    // Basic validation
                    if (SelectedInvoice == null)
                    {
                        Debug.WriteLine("MakePaymentAsync: SelectedInvoice is null");
                        ShowTemporaryErrorMessage("No invoice selected. Please select an invoice first.");
                        return;
                    }

                    // Verify payment amount
                    if (PaymentAmount <= 0)
                    {
                        Debug.WriteLine($"MakePaymentAsync: Invalid payment amount: {PaymentAmount}");
                        ShowTemporaryErrorMessage("Payment amount must be greater than zero.");
                        return;
                    }

                    // Verify we can make this call
                    if (_supplierInvoiceService == null)
                    {
                        Debug.WriteLine("MakePaymentAsync: _supplierInvoiceService is null");
                        ShowTemporaryErrorMessage("Internal error: Service not available");
                        return;
                    }

                    // Start processing
                    IsSaving = true;
                    ErrorMessage = string.Empty;

                    Debug.WriteLine($"MakePaymentAsync: Processing payment of {PaymentAmount:C} for invoice {SelectedInvoice.InvoiceNumber}");

                    // Perform the actual payment - with additional error catching
                    try
                    {
                        bool success = await _supplierInvoiceService.SettleInvoiceAsync(SelectedInvoice.SupplierInvoiceId, PaymentAmount);

                        if (!success)
                        {
                            Debug.WriteLine("MakePaymentAsync: SettleInvoiceAsync returned false");
                            ShowTemporaryErrorMessage("Payment processing failed. Please try again.");
                            return;
                        }
                    }
                    catch (Exception settleEx)
                    {
                        Debug.WriteLine($"MakePaymentAsync: Exception during SettleInvoiceAsync: {settleEx}");
                        ShowTemporaryErrorMessage($"Error during payment processing: {settleEx.Message}");
                        return;
                    }

                    // Close popup
                    IsPaymentPopupOpen = false;

                    // Refresh data
                    try
                    {
                        await LoadInvoicesAsync();

                        if (SelectedInvoice != null)
                        {
                            var updatedInvoice = await _supplierInvoiceService.GetByIdAsync(SelectedInvoice.SupplierInvoiceId);
                            SelectedInvoice = updatedInvoice;
                        }
                    }
                    catch (Exception refreshEx)
                    {
                        Debug.WriteLine($"MakePaymentAsync: Error refreshing data: {refreshEx}");
                        // Continue - we've already made the payment, just show the success message
                    }

                    // Show success message
                    try
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            MessageBox.Show("Payment processed successfully.", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception uiEx)
                    {
                        Debug.WriteLine($"MakePaymentAsync: UI exception: {uiEx}");
                        // Just log, don't show to user - the payment was successful
                    }
                }
                finally
                {
                    IsSaving = false;
                    _operationLock.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MakePaymentAsync: Unhandled exception: {ex}");
                MessageBox.Show($"An unexpected error occurred. Please try again.\n\nDetails: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Update the ShowPaymentHistory method to be async:
        private async void ShowPaymentHistory()
        {
            try
            {
                if (SelectedInvoice == null)
                {
                    ShowTemporaryErrorMessage("No invoice selected. Please select an invoice first.");
                    return;
                }

                try
                {
                    // Show loading indicator
                    IsSaving = true;

                    if (_supplierInvoiceService != null)
                    {
                        // Use await instead of .Result to prevent UI thread blocking
                        var paymentsResult = await _supplierInvoiceService.GetInvoicePaymentsAsync(SelectedInvoice.SupplierInvoiceId);
                        InvoicePayments = new ObservableCollection<SupplierTransactionDTO>(
                            paymentsResult ?? new List<SupplierTransactionDTO>());
                    }
                    else
                    {
                        InvoicePayments = new ObservableCollection<SupplierTransactionDTO>();
                    }

                    // Hide loading indicator
                    IsSaving = false;

                    // Use the new window instead of the popup
                    var historyWindow = new SupplierInvoicePaymentHistoryWindow(this);
                    historyWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    IsSaving = false;
                    Debug.WriteLine($"ShowPaymentHistory: Error loading payment history: {ex}");
                    ShowTemporaryErrorMessage($"Error loading payment history: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                IsSaving = false;
                Debug.WriteLine($"ShowPaymentHistory: Unhandled exception: {ex}");
                MessageBox.Show($"An unexpected error occurred. Please try again.\n\nDetails: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ViewInvoiceDetails()
        {
            if (SelectedInvoice == null)
            {
                ShowTemporaryErrorMessage("Please select an invoice first.");
                return;
            }

            // Use the new window instead of the popup
            var detailsWindow = new SupplierInvoiceDetailsWindow(this);
            detailsWindow.ShowDialog();
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            ErrorMessage = message;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });

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

        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _operationLock?.Dispose();
                UnsubscribeFromEvents();
                _isDisposed = true;
            }
            base.Dispose();
        }
    }
}