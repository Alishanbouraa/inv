// Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using QuickTechSystems.Application.Services;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using System.Text;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class MainStockViewModel : ViewModelBase
    {
        // Private service fields
        private readonly IMainStockService _mainStockService;
        private readonly ICategoryService _categoryService;
        private readonly IBarcodeService _barcodeService;
        private readonly ISupplierService _supplierService;
        private readonly IImagePathService _imagePathService;
        private readonly IInventoryTransferService _inventoryTransferService;
        private readonly IProductService _productService;
        private readonly ISupplierInvoiceService _supplierInvoiceService;

        // Concurrency management - improved with configurable timeout
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private const int DEFAULT_LOCK_TIMEOUT_MS = 2000; // 2 seconds instead of 0
        private bool _isDisposed;

        // Cancellation support
        private CancellationTokenSource _cts;

        // Direction properties
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;
        public ICommand ForceRefreshCommand { get; private set; }
        // Collection properties
        private ObservableCollection<MainStockDTO> _items;
        private ObservableCollection<MainStockDTO> _filteredItems;
        private ObservableCollection<CategoryDTO> _categories;
        private ObservableCollection<SupplierDTO> _suppliers;
        private ObservableCollection<ProductDTO> _storeProducts;
        private ObservableCollection<SupplierInvoiceDTO> _draftInvoices;

        // Selected item properties
        private MainStockDTO? _selectedItem;
        private ProductDTO? _selectedStoreProduct;
        private SupplierInvoiceDTO? _selectedInvoice;

        // State flags
        private bool _isEditing;
        private bool _isSaving;
        private bool _isItemPopupOpen;
        private bool _isNewItem;
        private bool _isTransferPopupOpen;
        private bool _transferByBoxes;

        // UI elements
        private System.Windows.Media.Imaging.BitmapImage? _barcodeImage;
        private System.Windows.Media.Imaging.BitmapImage? _productImage;
        private string _searchText = string.Empty;
        private string _statusMessage = string.Empty;
        private string _invoiceSearchText = string.Empty;
        private string _selectedItemBoxCount = "0";

        // Stock and quantity values
        private int _stockIncrement;
        private int _labelsPerProduct = 1;
        private decimal _transferQuantity = 1;
        private decimal _totalProfit;

        // Validation
        private Dictionary<int, List<string>> _validationErrors;

        // Event handlers
        private Action<EntityChangedEvent<MainStockDTO>> _mainStockChangedHandler;
        private readonly Action<EntityChangedEvent<SupplierDTO>> _supplierChangedHandler;
        private readonly Action<EntityChangedEvent<CategoryDTO>> _categoryChangedHandler;
        private readonly Action<EntityChangedEvent<ProductDTO>> _productChangedHandler;
        private readonly Action<GlobalDataRefreshEvent> _globalRefreshHandler;

        // Pagination properties
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages;
        private ObservableCollection<int> _pageNumbers;
        private List<int> _visiblePageNumbers = new List<int>();
        private int _totalItems;

        public bool? DialogResultBackup { get; set; }

        #region Properties

        public FlowDirection CurrentFlowDirection
        {
            get => _flowDirection;
            set => SetProperty(ref _flowDirection, value);
        }

        public ObservableCollection<MainStockDTO> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public ObservableCollection<MainStockDTO> FilteredItems
        {
            get => _filteredItems;
            set => SetProperty(ref _filteredItems, value);
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

        public ObservableCollection<ProductDTO> StoreProducts
        {
            get => _storeProducts;
            set => SetProperty(ref _storeProducts, value);
        }

        public MainStockDTO? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != null)
                {
                    // Unsubscribe from property changes on the old selected item
                    _selectedItem.PropertyChanged -= SelectedItem_PropertyChanged;
                }

                SetProperty(ref _selectedItem, value);
                IsEditing = value != null;

                if (value != null)
                {
                    // Subscribe to property changes on the new selected item
                    value.PropertyChanged += SelectedItem_PropertyChanged;

                    if (value.BarcodeImage != null)
                    {
                        LoadBarcodeImage(value.BarcodeImage);
                    }
                    else
                    {
                        BarcodeImage = null;
                    }

                    // Load image from path
                    ProductImage = value.ImagePath != null ? LoadImageFromPath(value.ImagePath) : null;
                }
                else
                {
                    BarcodeImage = null;
                    ProductImage = null;
                }

                OnPropertyChanged(nameof(SelectedItemBoxCount));
            }
        }

        public ProductDTO? SelectedStoreProduct
        {
            get => _selectedStoreProduct;
            set => SetProperty(ref _selectedStoreProduct, value);
        }

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public System.Windows.Media.Imaging.BitmapImage? BarcodeImage
        {
            get => _barcodeImage;
            set
            {
                if (_barcodeImage != value)
                {
                    // Dispose old image if it exists
                    if (_barcodeImage != null && !_barcodeImage.IsFrozen)
                    {
                        try
                        {
                            // BitmapImage doesn't implement IDisposable, but we can reduce references
                            _barcodeImage = null;
                            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error cleaning up barcode image: {ex.Message}");
                        }
                    }

                    _barcodeImage = value;
                    OnPropertyChanged(nameof(BarcodeImage));
                }
            }
        }

        public System.Windows.Media.Imaging.BitmapImage? ProductImage
        {
            get => _productImage;
            set
            {
                // Clean up previous image
                if (_productImage != value && _productImage != null && !_productImage.IsFrozen)
                {
                    try
                    {
                        // BitmapImage doesn't implement IDisposable, but we can reduce references
                        _productImage = null;
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error cleaning up product image: {ex.Message}");
                    }
                }

                SetProperty(ref _productImage, value);
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _currentPage = 1; // Reset to first page when searching
                    OnPropertyChanged(nameof(CurrentPage));
                    FilterItems();
                }
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                SetProperty(ref _isSaving, value);
                OnPropertyChanged(nameof(IsNotSaving));
            }
        }

        public bool IsNotSaving => !IsSaving;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public int StockIncrement
        {
            get => _stockIncrement;
            set => SetProperty(ref _stockIncrement, value);
        }

        public decimal TransferQuantity
        {
            get => _transferQuantity;
            set => SetProperty(ref _transferQuantity, value);
        }

        public Dictionary<int, List<string>> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value);
        }

        public int LabelsPerProduct
        {
            get => _labelsPerProduct;
            set => SetProperty(ref _labelsPerProduct, Math.Max(1, value));
        }

        public bool IsItemPopupOpen
        {
            get => _isItemPopupOpen;
            set => SetProperty(ref _isItemPopupOpen, value);
        }

        public bool IsTransferPopupOpen
        {
            get => _isTransferPopupOpen;
            set => SetProperty(ref _isTransferPopupOpen, value);
        }

        public bool IsNewItem
        {
            get => _isNewItem;
            set => SetProperty(ref _isNewItem, value);
        }

        // Pagination properties
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (value < 1 || value > TotalPages) return;
                if (SetProperty(ref _currentPage, value))
                {
                    _ = SafeLoadDataAsync();
                    UpdateVisiblePageNumbers();
                    OnPropertyChanged(nameof(IsFirstPage));
                    OnPropertyChanged(nameof(IsLastPage));
                }
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    _currentPage = 1; // Reset to first page when changing page size
                    OnPropertyChanged(nameof(CurrentPage));
                    _ = SafeLoadDataAsync();
                }
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                if (SetProperty(ref _totalPages, value))
                {
                    UpdateVisiblePageNumbers();
                    OnPropertyChanged(nameof(IsFirstPage));
                    OnPropertyChanged(nameof(IsLastPage));
                }
            }
        }

        public int TotalItems
        {
            get => _totalItems;
            private set => SetProperty(ref _totalItems, value);
        }

        public ObservableCollection<int> PageNumbers
        {
            get => _pageNumbers;
            private set => SetProperty(ref _pageNumbers, value);
        }

        public List<int> VisiblePageNumbers
        {
            get => _visiblePageNumbers;
            private set => SetProperty(ref _visiblePageNumbers, value);
        }

        public bool IsFirstPage => CurrentPage <= 1;
        public bool IsLastPage => CurrentPage >= TotalPages;

        public ObservableCollection<int> AvailablePageSizes { get; } = new ObservableCollection<int> { 10, 25, 50, 100 };

        public bool TransferByBoxes
        {
            get => _transferByBoxes;
            set => SetProperty(ref _transferByBoxes, value);
        }

        public bool TransferByItems
        {
            get => !_transferByBoxes;
            set => TransferByBoxes = !value;
        }

        public string SelectedItemBoxCount
        {
            get
            {
                if (SelectedItem == null || SelectedItem.ItemsPerBox <= 0)
                    return "0";

                return Math.Floor(SelectedItem.CurrentStock / SelectedItem.ItemsPerBox).ToString("N0");
            }
        }

        public ObservableCollection<SupplierInvoiceDTO> DraftInvoices
        {
            get => _draftInvoices;
            set => SetProperty(ref _draftInvoices, value);
        }

        public SupplierInvoiceDTO? SelectedInvoice
        {
            get => _selectedInvoice;
            set => SetProperty(ref _selectedInvoice, value);
        }

        public string InvoiceSearchText
        {
            get => _invoiceSearchText;
            set
            {
                if (SetProperty(ref _invoiceSearchText, value))
                {
                    _ = LoadDraftInvoicesAsync();
                }
            }
        }
        #endregion

        #region Command Properties
        public ICommand BulkAddCommand { get; private set; }
        public ICommand LoadCommand { get; private set; }
        public ICommand AddCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }
        public ICommand GenerateBarcodeCommand { get; private set; }
        public ICommand GenerateAutomaticBarcodeCommand { get; private set; }
        public ICommand UpdateStockCommand { get; private set; }
        public ICommand PrintBarcodeCommand { get; private set; }
        public ICommand GenerateMissingBarcodesCommand { get; private set; }
        public ICommand UploadImageCommand { get; private set; }
        public ICommand ClearImageCommand { get; private set; }
        public ICommand TransferToStoreCommand { get; private set; }
        public ICommand SaveTransferCommand { get; private set; }
        public ICommand BoxToIndividualCommand { get; private set; }
        // Pagination commands
        public ICommand NextPageCommand { get; private set; }
        public ICommand PreviousPageCommand { get; private set; }
        public ICommand GoToPageCommand { get; private set; }
        public ICommand ChangePageSizeCommand { get; private set; }
        #endregion

        /// <summary>
        /// Constructor for MainStockViewModel
        /// </summary>
        public MainStockViewModel(
            IMainStockService mainStockService,
            ICategoryService categoryService,
            IBarcodeService barcodeService,
            ISupplierService supplierService,
            ISupplierInvoiceService supplierInvoiceService,
            IImagePathService imagePathService,
            IInventoryTransferService inventoryTransferService,
            IProductService productService,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            Debug.WriteLine("Initializing MainStockViewModel");

            // Initialize services
            _mainStockService = mainStockService ?? throw new ArgumentNullException(nameof(mainStockService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _barcodeService = barcodeService ?? throw new ArgumentNullException(nameof(barcodeService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _imagePathService = imagePathService ?? throw new ArgumentNullException(nameof(imagePathService));
            _supplierInvoiceService = supplierInvoiceService ?? throw new ArgumentNullException(nameof(supplierInvoiceService));
            _inventoryTransferService = inventoryTransferService ?? throw new ArgumentNullException(nameof(inventoryTransferService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            ForceRefreshCommand = new AsyncRelayCommand(async _ => await RefreshFromDatabaseDirectly());
            // Initialize collections
            _items = new ObservableCollection<MainStockDTO>();
            _filteredItems = new ObservableCollection<MainStockDTO>();
            _categories = new ObservableCollection<CategoryDTO>();
            _suppliers = new ObservableCollection<SupplierDTO>();
            _storeProducts = new ObservableCollection<ProductDTO>();
            _draftInvoices = new ObservableCollection<SupplierInvoiceDTO>();
            _pageNumbers = new ObservableCollection<int>();

            // Initialize other properties
            _validationErrors = new Dictionary<int, List<string>>();
            _cts = new CancellationTokenSource();

            // Initialize event handlers
            _globalRefreshHandler = HandleGlobalRefresh;
            _mainStockChangedHandler = HandleMainStockChanged;
            _categoryChangedHandler = HandleCategoryChanged;
            _supplierChangedHandler = HandleSupplierChanged;
            _productChangedHandler = HandleProductChanged;

            // Initialize commands, subscribe to events, and load data
            SubscribeToEvents();
            InitializeCommands();
            _ = LoadDataAsync();

            Debug.WriteLine("MainStockViewModel initialized");
        }

        /// <summary>
        /// Updates visible page numbers for pagination
        /// </summary>
        private void UpdateVisiblePageNumbers()
        {
            var visiblePages = new List<int>();
            int startPage = Math.Max(1, CurrentPage - 2);
            int endPage = Math.Min(TotalPages, CurrentPage + 2);

            // Always show first page
            if (startPage > 1)
            {
                visiblePages.Add(1);
                if (startPage > 2) visiblePages.Add(-1); // -1 represents ellipsis
            }

            // Add current range
            for (int i = startPage; i <= endPage; i++)
            {
                visiblePages.Add(i);
            }

            // Always show last page
            if (endPage < TotalPages)
            {
                if (endPage < TotalPages - 1) visiblePages.Add(-1); // -1 represents ellipsis
                visiblePages.Add(TotalPages);
            }

            VisiblePageNumbers = visiblePages;
            OnPropertyChanged(nameof(VisiblePageNumbers));
        }

        /// <summary>
        /// Handles property changes for the selected item
        /// </summary>
        private void SelectedItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When properties that affect SelectedItemBoxCount change, raise property change notification
            if (e.PropertyName == nameof(MainStockDTO.CurrentStock) ||
                e.PropertyName == nameof(MainStockDTO.ItemsPerBox))
            {
                OnPropertyChanged(nameof(SelectedItemBoxCount));
            }
        }

        // Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.cs
        // Add this method to the class

        /// <summary>
        /// Directly refreshes data from the database, bypassing caching
        /// </summary>
    

        /// <summary>
        /// Clean up resources and unsubscribe from events
        /// </summary>
        public override void Dispose()
        {
            if (!_isDisposed)
            {
                // Cancel any pending operations
                try
                {
                    var cts = Interlocked.Exchange(ref _cts, null);
                    if (cts != null)
                    {
                        try
                        {
                            cts.Cancel();
                            cts.Dispose();
                        }
                        catch (ObjectDisposedException ex)
                        {
                            Debug.WriteLine($"Error disposing CTS: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing CTS: {ex.Message}");
                }

                // Unsubscribe from the property changed event of the selected product
                if (SelectedItem != null)
                {
                    SelectedItem.PropertyChanged -= SelectedItem_PropertyChanged;
                }

                // Clean up images
                BarcodeImage = null;
                ProductImage = null;

                // Dispose the operation lock
                _operationLock?.Dispose();

                // Unsubscribe from events
                UnsubscribeFromEvents();

                _isDisposed = true;
            }

            base.Dispose();
        }
    }
}