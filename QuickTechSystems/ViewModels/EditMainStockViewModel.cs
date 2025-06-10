// Path: QuickTechSystems.WPF.ViewModels/EditMainStockViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using QuickTechSystems.WPF.Views;

namespace QuickTechSystems.WPF.ViewModels
{
    public class EditMainStockViewModel : ViewModelBase
    {
        #region Services

        private readonly IMainStockService _mainStockService;
        private readonly ICategoryService _categoryService;
        private readonly ISupplierService _supplierService;
        private readonly ISupplierInvoiceService _supplierInvoiceService;
        private readonly IBarcodeService _barcodeService;
        private readonly IImagePathService _imagePathService;
        private readonly IProductService _productService;

        #endregion

        #region Properties

        private MainStockDTO _editingItem;
        private ObservableCollection<CategoryDTO> _categories;
        private ObservableCollection<SupplierDTO> _suppliers;
        private ObservableCollection<SupplierInvoiceDTO> _draftInvoices;
        private CategoryDTO _selectedCategory;
        private SupplierDTO _selectedSupplier;
        private SupplierInvoiceDTO _selectedInvoice;
        private bool _isSaving;
        private string _statusMessage;
        private bool? _dialogResult;
        private bool _autoSyncToProducts = false; // Default to false for editing

        public MainStockDTO EditingItem
        {
            get => _editingItem;
            set
            {
                if (value != null && value.IndividualItems <= 0)
                {
                    // Set individual items to be equal to current stock
                    value.IndividualItems = (int)value.CurrentStock;
                }
                SetProperty(ref _editingItem, value);
            }
        }

        public ObservableCollection<CategoryDTO> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public ObservableCollection<SupplierDTO> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }

        public ObservableCollection<SupplierInvoiceDTO> DraftInvoices
        {
            get => _draftInvoices;
            set => SetProperty(ref _draftInvoices, value);
        }

        public CategoryDTO SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value) && value != null && EditingItem != null)
                {
                    EditingItem.CategoryId = value.CategoryId;
                    EditingItem.CategoryName = value.Name;
                }
            }
        }

        public SupplierDTO SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value) && value != null && EditingItem != null)
                {
                    EditingItem.SupplierId = value.SupplierId;
                    EditingItem.SupplierName = value.Name;
                }
            }
        }

        public SupplierInvoiceDTO SelectedInvoice
        {
            get => _selectedInvoice;
            set
            {
                if (SetProperty(ref _selectedInvoice, value) && EditingItem != null)
                {
                    EditingItem.SupplierInvoiceId = value?.SupplierInvoiceId;
                }
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool? DialogResult
        {
            get => _dialogResult;
            set => SetProperty(ref _dialogResult, value);
        }

        /// <summary>
        /// Controls whether changes should be automatically synced to the Products/Store
        /// </summary>
        public bool AutoSyncToProducts
        {
            get => _autoSyncToProducts;
            set => SetProperty(ref _autoSyncToProducts, value);
        }

        /// <summary>
        /// Indicates if this is a new item (for UI logic)
        /// </summary>
        public bool IsNewItem => EditingItem?.MainStockId == 0;

        #endregion

        #region Commands

        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand GenerateBarcodeCommand { get; private set; }
        public ICommand UploadImageCommand { get; private set; }
        public ICommand ClearImageCommand { get; private set; }
        public ICommand AddNewCategoryCommand { get; private set; }
        public ICommand AddNewSupplierCommand { get; private set; }
        public ICommand AddNewInvoiceCommand { get; private set; }
        public ICommand ClearInvoiceCommand { get; private set; }
        public ICommand LookupProductCommand { get; private set; }
        public ICommand LookupBoxBarcodeCommand { get; private set; }

        #endregion

        public EditMainStockViewModel(
            IMainStockService mainStockService,
            ICategoryService categoryService,
            ISupplierService supplierService,
            ISupplierInvoiceService supplierInvoiceService,
            IBarcodeService barcodeService,
            IImagePathService imagePathService,
            IProductService productService,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            _mainStockService = mainStockService;
            _categoryService = categoryService;
            _supplierService = supplierService;
            _supplierInvoiceService = supplierInvoiceService;
            _barcodeService = barcodeService;
            _imagePathService = imagePathService;
            _productService = productService;

            InitializeCommands();
        }

        private void InitializeCommands()
        {
            SaveCommand = new AsyncRelayCommand(async _ => await SaveAsync());
            CancelCommand = new RelayCommand(_ => Cancel());
            GenerateBarcodeCommand = new RelayCommand(_ => GenerateBarcode());
            UploadImageCommand = new RelayCommand(_ => UploadImage());
            ClearImageCommand = new RelayCommand(_ => ClearImage());
            AddNewCategoryCommand = new AsyncRelayCommand(async _ => await AddNewCategoryAsync());
            AddNewSupplierCommand = new AsyncRelayCommand(async _ => await AddNewSupplierAsync());
            AddNewInvoiceCommand = new AsyncRelayCommand(async _ => await AddNewInvoiceAsync());
            ClearInvoiceCommand = new RelayCommand(_ => ClearInvoice());
            LookupProductCommand = new AsyncRelayCommand<MainStockDTO>(async item => await LookupProductAsync(item));
            LookupBoxBarcodeCommand = new AsyncRelayCommand<MainStockDTO>(async item => await LookupBoxBarcodeAsync(item));
        }

        public async Task InitializeAsync(MainStockDTO item)
        {
            // Create a new item if none provided
            EditingItem = item ?? new MainStockDTO
            {
                IsActive = true,
                ItemsPerBox = 0,  // Default to 0
                NumberOfBoxes = 0,
                IndividualItems = 1 // Ensure we have at least 1 individual item by default
            };

            // Set AutoSyncToProducts based on whether this is a new item or existing item
            // For new items, default to true; for existing items, default to false
            AutoSyncToProducts = IsNewItem;

            try
            {
                IsSaving = true;
                StatusMessage = "Loading data...";

                await LoadDataAsync();

                // Set selected values from the editing item
                if (EditingItem != null)
                {
                    if (EditingItem.CategoryId > 0)
                    {
                        SelectedCategory = Categories?.FirstOrDefault(c => c.CategoryId == EditingItem.CategoryId);
                    }

                    if (EditingItem.SupplierId.HasValue && EditingItem.SupplierId.Value > 0)
                    {
                        SelectedSupplier = Suppliers?.FirstOrDefault(s => s.SupplierId == EditingItem.SupplierId.Value);
                    }

                    // Enhanced invoice selection logic
                    if (EditingItem.SupplierInvoiceId.HasValue && EditingItem.SupplierInvoiceId.Value > 0)
                    {
                        Debug.WriteLine($"Looking for invoice ID: {EditingItem.SupplierInvoiceId.Value}");

                        // First check if the invoice is already loaded in DraftInvoices
                        var existingInvoice = DraftInvoices?.FirstOrDefault(i =>
                            i.SupplierInvoiceId == EditingItem.SupplierInvoiceId.Value);

                        if (existingInvoice != null)
                        {
                            Debug.WriteLine($"Found existing invoice in collection: {existingInvoice.InvoiceNumber}");
                            SelectedInvoice = existingInvoice;
                        }
                        else
                        {
                            // If not found in the loaded invoices, try to fetch it specifically
                            try
                            {
                                Debug.WriteLine("Invoice not found in collection, fetching from database...");
                                var invoice = await _supplierInvoiceService.GetByIdAsync(EditingItem.SupplierInvoiceId.Value);

                                if (invoice != null)
                                {
                                    Debug.WriteLine($"Successfully loaded invoice from database: {invoice.InvoiceNumber}");

                                    // Add to collection if not already there
                                    DraftInvoices.Add(invoice);

                                    // Set as selected invoice (AFTER adding to collection)
                                    SelectedInvoice = invoice;

                                    // Force UI update to ensure selection appears
                                    OnPropertyChanged(nameof(SelectedInvoice));
                                    OnPropertyChanged(nameof(DraftInvoices));
                                }
                                else
                                {
                                    Debug.WriteLine("No invoice found in database with that ID");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error loading invoice: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("No SupplierInvoiceId found on the editing item");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing edit dialog: {ex.Message}");
                StatusMessage = $"Error loading data: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
            }
        }

        protected override async Task LoadDataAsync()
        {
            try
            {
                // Load categories
                var categories = await _categoryService.GetActiveAsync();
                Categories = new ObservableCollection<CategoryDTO>(categories);

                // Load suppliers
                var suppliers = await _supplierService.GetActiveAsync();
                Suppliers = new ObservableCollection<SupplierDTO>(suppliers);

                // Load invoices with enhanced approach
                var invoices = new List<SupplierInvoiceDTO>();

                // Get draft invoices (for new items)
                var draftInvoices = await _supplierInvoiceService.GetByStatusAsync("Draft");
                invoices.AddRange(draftInvoices);

                // Also load recent invoices of all statuses (for editing existing items)
                var recentDate = DateTime.Now.AddDays(-90);  // Go back 90 days to ensure we find most invoices
                var recentInvoices = await _supplierInvoiceService.GetRecentInvoicesAsync(recentDate);

                // Add recent invoices without duplicating
                foreach (var invoice in recentInvoices)
                {
                    if (!invoices.Any(i => i.SupplierInvoiceId == invoice.SupplierInvoiceId))
                    {
                        invoices.Add(invoice);
                    }
                }

                DraftInvoices = new ObservableCollection<SupplierInvoiceDTO>(invoices);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading data: {ex.Message}");
                throw;
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                IsSaving = true;
                StatusMessage = "Saving changes...";

                if (EditingItem == null)
                {
                    StatusMessage = "No item to save";
                    return;
                }

                if (string.IsNullOrWhiteSpace(EditingItem.BoxBarcode) && !string.IsNullOrWhiteSpace(EditingItem.Barcode))
                    EditingItem.BoxBarcode = $"BX{EditingItem.Barcode}";

                // Ensure consistent pricing
                EnsureConsistentPricing(EditingItem);

                // Set CurrentStock based on IndividualItems
                EditingItem.CurrentStock = EditingItem.IndividualItems;

                // Set the AutoSyncToProducts flag on the item
                EditingItem.AutoSyncToProducts = AutoSyncToProducts;

                // Validate the item
                if (!ValidateItem())
                    return;

                // Ensure the invoice ID is set on the item before saving
                if (SelectedInvoice != null)
                    EditingItem.SupplierInvoiceId = SelectedInvoice.SupplierInvoiceId;

                MainStockDTO savedItem;
                bool isNewItem = EditingItem.MainStockId == 0;

                if (isNewItem)
                    savedItem = await _mainStockService.CreateAsync(EditingItem);
                else
                    savedItem = await _mainStockService.UpdateAsync(EditingItem);

                // FIXED: Only sync to products if AutoSyncToProducts is enabled
                if (AutoSyncToProducts)
                {
                    await ProcessProductSyncAndInvoiceIntegration(savedItem, isNewItem);
                }
                else
                {
                    // Show success message for MainStock-only save
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                        string message = $"MainStock item {(isNewItem ? "created" : "updated")} successfully.\n\n" +
                                       "Item was NOT automatically synced to the store.\n" +
                                       "You can transfer it manually later if needed.";
                        MessageBox.Show(message, "MainStock Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }

                StatusMessage = "Changes saved successfully";
                DialogResult = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving MainStock item: {ex.Message}");
                StatusMessage = $"Error saving: {ex.Message}";
                MessageBox.Show($"Error saving item: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>
        /// Processes product synchronization and invoice integration when AutoSyncToProducts is enabled
        /// </summary>
        private async Task ProcessProductSyncAndInvoiceIntegration(MainStockDTO savedItem, bool isNewItem)
        {
            try
            {
                StatusMessage = "Syncing to store products...";

                // Check if product already exists
                var existingProduct = await _productService.FindProductByBarcodeAsync(savedItem.Barcode);

                ProductDTO storeProduct;
                if (existingProduct != null)
                {
                    // Update existing product
                    storeProduct = new ProductDTO
                    {
                        ProductId = existingProduct.ProductId,
                        Name = savedItem.Name,
                        Barcode = savedItem.Barcode,
                        BoxBarcode = savedItem.BoxBarcode,
                        CategoryId = savedItem.CategoryId,
                        CategoryName = savedItem.CategoryName,
                        SupplierId = savedItem.SupplierId,
                        SupplierName = savedItem.SupplierName,
                        Description = savedItem.Description,
                        PurchasePrice = savedItem.PurchasePrice,
                        WholesalePrice = savedItem.WholesalePrice,
                        SalePrice = savedItem.SalePrice,
                        MainStockId = savedItem.MainStockId,
                        BoxPurchasePrice = savedItem.BoxPurchasePrice,
                        BoxWholesalePrice = savedItem.BoxWholesalePrice,
                        BoxSalePrice = savedItem.BoxSalePrice,
                        ItemsPerBox = savedItem.ItemsPerBox,
                        MinimumBoxStock = savedItem.MinimumBoxStock,
                        MinimumStock = savedItem.MinimumStock,
                        ImagePath = savedItem.ImagePath,
                        Speed = savedItem.Speed,
                        IsActive = savedItem.IsActive,
                        CurrentStock = existingProduct.CurrentStock, // Preserve existing stock
                        CreatedAt = existingProduct.CreatedAt, // Preserve creation date
                        UpdatedAt = DateTime.Now
                    };

                    await _productService.UpdateAsync(storeProduct);
                }
                else
                {
                    // Create new product
                    storeProduct = new ProductDTO
                    {
                        Name = savedItem.Name,
                        Barcode = savedItem.Barcode,
                        BoxBarcode = savedItem.BoxBarcode,
                        CategoryId = savedItem.CategoryId,
                        CategoryName = savedItem.CategoryName,
                        SupplierId = savedItem.SupplierId,
                        SupplierName = savedItem.SupplierName,
                        Description = savedItem.Description,
                        PurchasePrice = savedItem.PurchasePrice,
                        WholesalePrice = savedItem.WholesalePrice,
                        SalePrice = savedItem.SalePrice,
                        MainStockId = savedItem.MainStockId,
                        BoxPurchasePrice = savedItem.BoxPurchasePrice,
                        BoxWholesalePrice = savedItem.BoxWholesalePrice,
                        BoxSalePrice = savedItem.BoxSalePrice,
                        ItemsPerBox = savedItem.ItemsPerBox,
                        MinimumBoxStock = savedItem.MinimumBoxStock,
                        CurrentStock = 0, // New products start with 0 stock
                        MinimumStock = savedItem.MinimumStock,
                        ImagePath = savedItem.ImagePath,
                        Speed = savedItem.Speed,
                        IsActive = savedItem.IsActive,
                        CreatedAt = DateTime.Now
                    };
                    storeProduct = await _productService.CreateAsync(storeProduct);
                }

                // Add to invoice if one is selected
                if (SelectedInvoice != null)
                {
                    StatusMessage = "Adding to supplier invoice...";

                    var invoiceDetail = new SupplierInvoiceDetailDTO
                    {
                        SupplierInvoiceId = SelectedInvoice.SupplierInvoiceId,
                        ProductId = storeProduct.ProductId,
                        ProductName = savedItem.Name,
                        ProductBarcode = savedItem.Barcode,
                        BoxBarcode = savedItem.BoxBarcode,
                        NumberOfBoxes = savedItem.NumberOfBoxes,
                        ItemsPerBox = savedItem.ItemsPerBox,
                        BoxPurchasePrice = savedItem.BoxPurchasePrice,
                        BoxSalePrice = savedItem.BoxSalePrice,
                        Quantity = savedItem.CurrentStock,
                        PurchasePrice = savedItem.PurchasePrice,
                        TotalPrice = savedItem.PurchasePrice * savedItem.CurrentStock
                    };
                    await _supplierInvoiceService.AddProductToInvoiceAsync(invoiceDetail);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    string message = $"Product {(isNewItem ? "created" : "updated")} and synced to store successfully.";
                    if (SelectedInvoice != null)
                    {
                        message += $"\nItem was added to invoice '{SelectedInvoice.InvoiceNumber}'.";
                    }
                    MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in product sync and invoice integration: {ex.Message}");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                    MessageBox.Show($"MainStock item saved but sync to store failed: {ex.Message}",
                        "Partial Success", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
        }

        private void Cancel()
        {
            DialogResult = false;
        }

        private void GenerateBarcode()
        {
            if (EditingItem == null || string.IsNullOrWhiteSpace(EditingItem.Barcode))
            {
                MessageBox.Show("Please enter a barcode first.", "Barcode Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                EditingItem.BarcodeImage = _barcodeService.GenerateBarcode(EditingItem.Barcode);

                // If box barcode is empty, generate it automatically
                if (string.IsNullOrWhiteSpace(EditingItem.BoxBarcode))
                {
                    EditingItem.BoxBarcode = $"BX{EditingItem.Barcode}";
                }

                StatusMessage = "Barcode generated successfully";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating barcode: {ex.Message}");
                StatusMessage = $"Error generating barcode: {ex.Message}";
            }
        }

        private void UploadImage()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                    Title = "Select an image for the item"
                };

                if (dialog.ShowDialog() == true)
                {
                    string imagePath = _imagePathService.SaveProductImage(dialog.FileName);
                    EditingItem.ImagePath = imagePath;
                    StatusMessage = "Image uploaded successfully";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error uploading image: {ex.Message}");
                StatusMessage = $"Error uploading image: {ex.Message}";
            }
        }

        private void ClearImage()
        {
            if (EditingItem != null && !string.IsNullOrEmpty(EditingItem.ImagePath))
            {
                try
                {
                    _imagePathService.DeleteProductImage(EditingItem.ImagePath);
                    EditingItem.ImagePath = null;
                    StatusMessage = "Image cleared successfully";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error clearing image: {ex.Message}");
                    StatusMessage = $"Error clearing image: {ex.Message}";
                }
            }
        }

        private async Task AddNewCategoryAsync()
        {
            try
            {
                var dialog = new QuickCategoryDialogWindow();
                var result = dialog.ShowDialog();

                if (result == true && dialog.NewCategory != null)
                {
                    var newCategory = await _categoryService.CreateAsync(dialog.NewCategory);
                    Categories.Add(newCategory);
                    SelectedCategory = newCategory;
                    StatusMessage = $"Category '{newCategory.Name}' added successfully";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding category: {ex.Message}");
                StatusMessage = $"Error adding category: {ex.Message}";
            }
        }

        private async Task AddNewSupplierAsync()
        {
            try
            {
                var dialog = new QuickSupplierDialogWindow();
                var result = dialog.ShowDialog();

                if (result == true && dialog.NewSupplier != null)
                {
                    var newSupplier = await _supplierService.CreateAsync(dialog.NewSupplier);
                    Suppliers.Add(newSupplier);
                    SelectedSupplier = newSupplier;
                    StatusMessage = $"Supplier '{newSupplier.Name}' added successfully";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding supplier: {ex.Message}");
                StatusMessage = $"Error adding supplier: {ex.Message}";
            }
        }

        private async Task AddNewInvoiceAsync()
        {
            try
            {
                var dialog = new QuickSupplierInvoiceDialog();
                var result = dialog.ShowDialog();

                if (result == true && dialog.CreatedInvoice != null)
                {
                    DraftInvoices.Add(dialog.CreatedInvoice);
                    SelectedInvoice = dialog.CreatedInvoice;
                    StatusMessage = $"Invoice '{dialog.CreatedInvoice.InvoiceNumber}' created successfully";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating invoice: {ex.Message}");
                StatusMessage = $"Error creating invoice: {ex.Message}";
            }
        }

        private void ClearInvoice()
        {
            SelectedInvoice = null;
            if (EditingItem != null)
            {
                EditingItem.SupplierInvoiceId = null;
            }
            StatusMessage = "Invoice selection cleared";
        }

        private async Task LookupProductAsync(MainStockDTO item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Barcode))
                return;

            try
            {
                StatusMessage = "Searching for product...";
                IsSaving = true;

                var existingProduct = await _mainStockService.GetByBarcodeAsync(item.Barcode);

                if (existingProduct != null)
                {
                    // Preserve important user-entered fields
                    int numberOfBoxes = item.NumberOfBoxes;
                    int individualItems = item.IndividualItems;

                    // Update fields from existing product
                    item.MainStockId = existingProduct.MainStockId;
                    item.Name = existingProduct.Name;
                    item.Description = existingProduct.Description;
                    item.CategoryId = existingProduct.CategoryId;
                    item.CategoryName = existingProduct.CategoryName;
                    item.SupplierId = existingProduct.SupplierId;
                    item.SupplierName = existingProduct.SupplierName;
                    item.PurchasePrice = existingProduct.PurchasePrice;
                    item.WholesalePrice = existingProduct.WholesalePrice;
                    item.SalePrice = existingProduct.SalePrice;
                    item.BoxBarcode = existingProduct.BoxBarcode;
                    item.BoxPurchasePrice = existingProduct.BoxPurchasePrice;
                    item.BoxWholesalePrice = existingProduct.BoxWholesalePrice;
                    item.BoxSalePrice = existingProduct.BoxSalePrice;
                    item.ItemsPerBox = existingProduct.ItemsPerBox > 0 ? existingProduct.ItemsPerBox : 1;
                    item.MinimumStock = existingProduct.MinimumStock;
                    item.MinimumBoxStock = existingProduct.MinimumBoxStock;
                    item.Speed = existingProduct.Speed;
                    item.IsActive = existingProduct.IsActive;
                    item.ImagePath = existingProduct.ImagePath;
                    item.CurrentStock = existingProduct.CurrentStock;

                    // Restore individual items or use current stock if it's greater than zero
                    if (individualItems > 0)
                        item.IndividualItems = individualItems;
                    else if (existingProduct.CurrentStock > 0)
                        item.IndividualItems = (int)existingProduct.CurrentStock;
                    else
                        item.IndividualItems = 1; // Ensure at least 1

                    // Restore the number of boxes the user entered
                    if (numberOfBoxes > 0)
                        item.NumberOfBoxes = numberOfBoxes;
                    else
                        item.NumberOfBoxes = existingProduct.NumberOfBoxes;

                    // Update selected values
                    SelectedCategory = Categories?.FirstOrDefault(c => c.CategoryId == item.CategoryId);
                    SelectedSupplier = Suppliers?.FirstOrDefault(s => s.SupplierId == item.SupplierId);

                    StatusMessage = $"Found existing product: {item.Name}";
                }
                else
                {
                    // New product - ensure box barcode if item barcode is provided
                    if (string.IsNullOrWhiteSpace(item.BoxBarcode))
                    {
                        item.BoxBarcode = $"BX{item.Barcode}";
                    }
                    else if (item.BoxBarcode == item.Barcode)
                    {
                        item.BoxBarcode = $"BX{item.Barcode}";
                    }

                    StatusMessage = "New product. Please enter details.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error looking up product: {ex.Message}");
                StatusMessage = $"Error looking up product: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        private async Task LookupBoxBarcodeAsync(MainStockDTO item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.BoxBarcode))
                return;

            try
            {
                StatusMessage = "Searching for product by box barcode...";
                IsSaving = true;

                var existingProduct = await _mainStockService.GetByBoxBarcodeAsync(item.BoxBarcode);

                if (existingProduct != null)
                {
                    // Keep the current box quantities
                    int numberOfBoxes = item.NumberOfBoxes > 0 ? item.NumberOfBoxes : 1;
                    int individualItems = item.IndividualItems;

                    // Update fields from existing product
                    item.MainStockId = existingProduct.MainStockId;
                    item.Name = existingProduct.Name;
                    item.Barcode = existingProduct.Barcode;
                    item.Description = existingProduct.Description;
                    item.CategoryId = existingProduct.CategoryId;
                    item.CategoryName = existingProduct.CategoryName;
                    item.SupplierId = existingProduct.SupplierId;
                    item.SupplierName = existingProduct.SupplierName;
                    item.PurchasePrice = existingProduct.PurchasePrice;
                    item.WholesalePrice = existingProduct.WholesalePrice;
                    item.SalePrice = existingProduct.SalePrice;
                    item.BoxPurchasePrice = existingProduct.BoxPurchasePrice;
                    item.BoxWholesalePrice = existingProduct.BoxWholesalePrice;
                    item.BoxSalePrice = existingProduct.BoxSalePrice;
                    item.ItemsPerBox = existingProduct.ItemsPerBox > 0 ? existingProduct.ItemsPerBox : 1;
                    item.MinimumStock = existingProduct.MinimumStock;
                    item.MinimumBoxStock = existingProduct.MinimumBoxStock;
                    item.Speed = existingProduct.Speed;
                    item.IsActive = existingProduct.IsActive;
                    item.ImagePath = existingProduct.ImagePath;
                    item.CurrentStock = existingProduct.CurrentStock;
                    item.IndividualItems = (int)existingProduct.CurrentStock;

                    // Restore the number of boxes
                    item.NumberOfBoxes = numberOfBoxes;

                    // If individualItems was set, keep it
                    if (individualItems > 0)
                    {
                        item.IndividualItems = individualItems;
                    }

                    // Update selected values
                    SelectedCategory = Categories?.FirstOrDefault(c => c.CategoryId == item.CategoryId);
                    SelectedSupplier = Suppliers?.FirstOrDefault(s => s.SupplierId == item.SupplierId);

                    StatusMessage = $"Found existing product by box barcode: {item.Name}";
                }
                else
                {
                    // If not found and the box barcode doesn't start with "BX", try with it
                    if (!item.BoxBarcode.StartsWith("BX", StringComparison.OrdinalIgnoreCase))
                    {
                        var modifiedBoxBarcode = $"BX{item.BoxBarcode}";
                        try
                        {
                            var productWithPrefix = await _mainStockService.GetByBoxBarcodeAsync(modifiedBoxBarcode);
                            if (productWithPrefix != null)
                            {
                                // Update the box barcode to the correct format
                                item.BoxBarcode = modifiedBoxBarcode;

                                // Recursively call this method again with the updated barcode
                                await LookupBoxBarcodeAsync(item);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error looking up modified box barcode: {ex.Message}");
                        }
                    }

                    StatusMessage = "No product found with this box barcode.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error looking up box barcode: {ex.Message}");
                StatusMessage = $"Error looking up box barcode: {ex.Message}";
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>
        /// Ensures consistent pricing for the item, respecting manually entered values
        /// </summary>
        private void EnsureConsistentPricing(MainStockDTO item)
        {
            // If user has entered both Box Purchase Price and Purchase Price directly
            // We don't adjust either one - respect both user inputs
            if (item.BoxPurchasePrice > 0 && item.PurchasePrice > 0)
            {
                // Do nothing - respect both values as the user entered them
            }
            // Calculate purchase price from box price if needed
            else if (item.PurchasePrice <= 0 && item.BoxPurchasePrice > 0 && item.ItemsPerBox > 0)
            {
                item.PurchasePrice = Math.Round(item.BoxPurchasePrice / item.ItemsPerBox, 2);
            }
            // Calculate box purchase price from item price if needed (only if ItemsPerBox is set)
            else if (item.BoxPurchasePrice <= 0 && item.PurchasePrice > 0 && item.ItemsPerBox > 0)
            {
                item.BoxPurchasePrice = Math.Round(item.PurchasePrice * item.ItemsPerBox, 2);
            }

            // Handle Wholesale Price similarly - only if ItemsPerBox > 0
            if (item.BoxWholesalePrice > 0 && item.WholesalePrice > 0)
            {
                // Respect both user inputs
            }
            // Calculate wholesale price from box wholesale price if needed
            else if (item.WholesalePrice <= 0 && item.BoxWholesalePrice > 0 && item.ItemsPerBox > 0)
            {
                item.WholesalePrice = Math.Round(item.BoxWholesalePrice / item.ItemsPerBox, 2);
            }
            // Calculate box wholesale price from item wholesale price if needed (only if ItemsPerBox is set)
            else if (item.BoxWholesalePrice <= 0 && item.WholesalePrice > 0 && item.ItemsPerBox > 0)
            {
                item.BoxWholesalePrice = Math.Round(item.WholesalePrice * item.ItemsPerBox, 2);
            }

            // Handle Sale Price similarly - only if ItemsPerBox > 0
            if (item.BoxSalePrice > 0 && item.SalePrice > 0)
            {
                // Respect both user inputs
            }
            // Calculate sale price from box sale price if needed
            else if (item.SalePrice <= 0 && item.BoxSalePrice > 0 && item.ItemsPerBox > 0)
            {
                item.SalePrice = Math.Round(item.BoxSalePrice / item.ItemsPerBox, 2);
            }
            // Calculate box sale price from item sale price if needed (only if ItemsPerBox is set)
            else if (item.BoxSalePrice <= 0 && item.SalePrice > 0 && item.ItemsPerBox > 0)
            {
                item.BoxSalePrice = Math.Round(item.SalePrice * item.ItemsPerBox, 2);
            }

            // Ensure we have a valid wholesale price (default to purchase price + 10% if not set)
            if (item.WholesalePrice <= 0 && item.PurchasePrice > 0)
            {
                item.WholesalePrice = Math.Round(item.PurchasePrice * 1.1m, 2);
            }

            // Ensure we have a valid sale price (default to purchase price + 20% if not set)
            if (item.SalePrice <= 0 && item.PurchasePrice > 0)
            {
                item.SalePrice = Math.Round(item.PurchasePrice * 1.2m, 2);
            }

            // Only set box prices from item prices if ItemsPerBox is valid
            if (item.ItemsPerBox > 0)
            {
                // Ensure we have a valid box wholesale price (default to wholesale price * ItemsPerBox if not set)
                if (item.BoxWholesalePrice <= 0 && item.WholesalePrice > 0)
                {
                    item.BoxWholesalePrice = Math.Round(item.WholesalePrice * item.ItemsPerBox, 2);
                }

                // Ensure we have a valid box sale price (default to sale price * ItemsPerBox if not set)
                if (item.BoxSalePrice <= 0 && item.SalePrice > 0)
                {
                    item.BoxSalePrice = Math.Round(item.SalePrice * item.ItemsPerBox, 2);
                }
            }
        }

        private bool ValidateItem()
        {
            var errors = new List<string>();

            // Required fields validation
            if (string.IsNullOrWhiteSpace(EditingItem.Name))
                errors.Add("• Item name is required");

            if (EditingItem.CategoryId <= 0)
                errors.Add("• Please select a category");

            if (EditingItem.SupplierId <= 0 || !EditingItem.SupplierId.HasValue)
                errors.Add("• Please select a supplier");

            // At least one pricing option must be provided
            if (EditingItem.PurchasePrice <= 0 && EditingItem.BoxPurchasePrice <= 0)
                errors.Add("• Either item purchase price or box purchase price is required");

            if (EditingItem.SalePrice <= 0 && EditingItem.BoxSalePrice <= 0)
                errors.Add("• Either item sale price or box sale price is required");

            // Individual items quantity is required
            if (EditingItem.IndividualItems <= 0)
                errors.Add("• Individual items quantity must be greater than zero");

            // Modified: Only require invoice selection if AutoSyncToProducts is enabled
            if (AutoSyncToProducts && SelectedInvoice == null)
                errors.Add("• Please select a supplier invoice (required when auto-sync is enabled)");

            if (errors.Any())
            {
                MessageBox.Show($"Please fix the following errors:\n\n{string.Join("\n", errors)}",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }
    }
}