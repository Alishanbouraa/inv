// Path: QuickTechSystems.WPF.ViewModels/ProductViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using System.Threading;
using System.Text;
using QuickTechSystems.Application.Interfaces;
using System.IO;
using System.Windows.Documents;
using System.Printing;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Controls;

namespace QuickTechSystems.WPF.ViewModels
{
    public class ProductViewModel : ViewModelBase
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly IMainStockService _mainStockService;
        private readonly IDbContextScopeService _dbContextScopeService;
        private readonly IBarcodeService _barcodeService;
        private readonly IPrinterService _printerService; // ADD THIS LINE
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;
        private ObservableCollection<ProductDTO> _products;
        private ObservableCollection<ProductDTO> _filteredProducts;
        private ObservableCollection<CategoryDTO> _categories;
        private ObservableCollection<MainStockDTO> _mainStockItems;
        private ProductDTO? _selectedProduct;
        private string _searchText = string.Empty;
        private bool _isSaving;
        private string _statusMessage = string.Empty;
        private int _stockIncrement;
        private Dictionary<int, List<string>> _validationErrors;
        private Action<EntityChangedEvent<ProductDTO>> _productChangedHandler;
        private readonly Action<EntityChangedEvent<CategoryDTO>> _categoryChangedHandler;
        private readonly Action<EntityChangedEvent<MainStockDTO>> _mainStockChangedHandler;
        private int _labelsPerProduct = 1;
        private decimal _totalProfit;
        private CancellationTokenSource _cts;
        private readonly Action<ProductStockUpdatedEvent> _productStockUpdatedHandler;
        private int _totalBoxes;
        private readonly ISupplierService _supplierService;
        private ObservableCollection<SupplierDTO> _suppliers;

        // Pagination properties
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalPages;
        private ObservableCollection<int> _pageNumbers;
        private List<int> _visiblePageNumbers = new List<int>();
        private int _totalProducts;

        // Cache for generated barcodes to improve performance
        private Dictionary<string, byte[]> _barcodeCache = new Dictionary<string, byte[]>();

        public FlowDirection FlowDirection
        {
            get => _flowDirection;
            set => SetProperty(ref _flowDirection, value);
        }

        public decimal TotalProfit
        {
            get => _totalProfit;
            set => SetProperty(ref _totalProfit, value);
        }

        // New properties for aggregated values
        private decimal _totalPurchaseValue;
        private decimal _totalSaleValue;
        private decimal _selectedProductTotalCost;
        private decimal _selectedProductTotalValue;
        private decimal _selectedProductProfitMargin;
        private decimal _selectedProductProfitPercentage;

        public decimal TotalPurchaseValue
        {
            get => _totalPurchaseValue;
            set => SetProperty(ref _totalPurchaseValue, value);
        }

        public void RefreshProductCalculations(ProductDTO product)
        {
            if (product == null) return;

            // Recalculate totals for the specific product
            decimal totalCost = product.PurchasePrice * product.CurrentStock;
            decimal totalValue = product.SalePrice * product.CurrentStock;

            // If this is the selected product, update the calculations
            if (SelectedProduct != null && SelectedProduct.ProductId == product.ProductId)
            {
                CalculateSelectedProductValues();
            }

            // Update the global calculations
            CalculateAggregatedValues();
        }

        public decimal TotalSaleValue
        {
            get => _totalSaleValue;
            set => SetProperty(ref _totalSaleValue, value);
        }

        public decimal SelectedProductTotalCost
        {
            get => _selectedProductTotalCost;
            set => SetProperty(ref _selectedProductTotalCost, value);
        }

        public int TotalBoxes
        {
            get => _totalBoxes;
            set => SetProperty(ref _totalBoxes, value);
        }

        public decimal SelectedProductTotalValue
        {
            get => _selectedProductTotalValue;
            set => SetProperty(ref _selectedProductTotalValue, value);
        }

        public ObservableCollection<SupplierDTO> Suppliers
        {
            get => _suppliers;
            set => SetProperty(ref _suppliers, value);
        }

        public decimal SelectedProductProfitMargin
        {
            get => _selectedProductProfitMargin;
            set => SetProperty(ref _selectedProductProfitMargin, value);
        }

        public decimal SelectedProductProfitPercentage
        {
            get => _selectedProductProfitPercentage;
            set => SetProperty(ref _selectedProductProfitPercentage, value);
        }

        public int LabelsPerProduct
        {
            get => _labelsPerProduct;
            set => SetProperty(ref _labelsPerProduct, Math.Max(1, value));
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

        public ObservableCollection<CategoryDTO> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public ObservableCollection<MainStockDTO> MainStockItems
        {
            get => _mainStockItems;
            set => SetProperty(ref _mainStockItems, value);
        }

        public ProductDTO? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (_selectedProduct != null)
                {
                    // Unsubscribe from property changes on the old selected product
                    _selectedProduct.PropertyChanged -= SelectedProduct_PropertyChanged;
                }

                SetProperty(ref _selectedProduct, value);

                if (value != null)
                {
                    // Subscribe to property changes on the new selected product
                    value.PropertyChanged += SelectedProduct_PropertyChanged;

                    // Update calculations when selecting a product
                    CalculateSelectedProductValues();
                }
                else
                {
                    SelectedProductTotalCost = 0;
                    SelectedProductTotalValue = 0;
                    SelectedProductProfitMargin = 0;
                    SelectedProductProfitPercentage = 0;
                }
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
                    FilterProducts();
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

        public Dictionary<int, List<string>> ValidationErrors
        {
            get => _validationErrors;
            set => SetProperty(ref _validationErrors, value);
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

        public int TotalProducts
        {
            get => _totalProducts;
            private set => SetProperty(ref _totalProducts, value);
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

        public ICommand LoadCommand { get; private set; }
        public ICommand UpdateStockCommand { get; private set; }
        public ICommand GenerateBarcodeCommand { get; private set; }
        public ICommand PrintBarcodeCommand { get; private set; }
        public ICommand SyncWithMainStockCommand { get; private set; }
        public ICommand EditProductCommand { get; private set; }

        // Pagination commands
        public ICommand NextPageCommand { get; private set; }
        public ICommand PreviousPageCommand { get; private set; }
        public ICommand GoToPageCommand { get; private set; }
        public ICommand ChangePageSizeCommand { get; private set; }

        public ProductViewModel(
            IProductService productService,
            ISupplierService supplierService,
            ICategoryService categoryService,
            IMainStockService mainStockService,
            IDbContextScopeService dbContextScopeService,
            IBarcodeService barcodeService,
            IPrinterService printerService, // ADD THIS LINE
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            Debug.WriteLine("Initializing ProductViewModel");
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _mainStockService = mainStockService ?? throw new ArgumentNullException(nameof(mainStockService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _dbContextScopeService = dbContextScopeService ?? throw new ArgumentNullException(nameof(dbContextScopeService));
            _barcodeService = barcodeService ?? throw new ArgumentNullException(nameof(barcodeService));
            _printerService = printerService ?? throw new ArgumentNullException(nameof(printerService)); // ADD THIS LINE

            _productStockUpdatedHandler = HandleProductStockUpdated;
            _suppliers = new ObservableCollection<SupplierDTO>();
            _products = new ObservableCollection<ProductDTO>();
            _filteredProducts = new ObservableCollection<ProductDTO>();
            _categories = new ObservableCollection<CategoryDTO>();
            _mainStockItems = new ObservableCollection<MainStockDTO>();
            _validationErrors = new Dictionary<int, List<string>>();
            _productChangedHandler = HandleProductChanged;
            _categoryChangedHandler = HandleCategoryChanged;
            _mainStockChangedHandler = HandleMainStockChanged;
            _pageNumbers = new ObservableCollection<int>();
            _cts = new CancellationTokenSource();

            SubscribeToEvents();
            InitializeCommands();

            // Test barcode generation on startup (debug mode only)
#if DEBUG
            try
            {
                TestBarcodeGeneration();
            }
            catch (Exception testEx)
            {
                Debug.WriteLine($"Barcode test failed during initialization: {testEx.Message}");
            }
#endif

            _ = LoadDataAsync();
            Debug.WriteLine("ProductViewModel initialized");
        }

        private void InitializeCommands()
        {
            LoadCommand = new AsyncRelayCommand(async _ => await LoadDataAsync(), _ => !IsSaving);
            UpdateStockCommand = new AsyncRelayCommand(async _ => await UpdateStockAsync(), _ => !IsSaving);
            GenerateBarcodeCommand = new AsyncRelayCommand(async _ => await GenerateBarcodeAsync(), _ => !IsSaving);
            PrintBarcodeCommand = new AsyncRelayCommand(async _ => await PrintBarcodeAsync(), _ => !IsSaving);
            SyncWithMainStockCommand = new AsyncRelayCommand(async _ => await SyncWithMainStockAsync(), _ => !IsSaving);
            EditProductCommand = new AsyncRelayCommand(async _ => await EditProductAsync(), _ => !IsSaving && SelectedProduct != null); // ADD THIS LINE

            // Pagination commands
            NextPageCommand = new RelayCommand(_ => CurrentPage++, _ => !IsLastPage);
            PreviousPageCommand = new RelayCommand(_ => CurrentPage--, _ => !IsFirstPage);
            GoToPageCommand = new RelayCommand<int>(page => CurrentPage = page);
            ChangePageSizeCommand = new RelayCommand<int>(size => PageSize = size);
        }


        // Generate Barcode Method - Separate from Printing
        private async Task GenerateBarcodeAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("A barcode generation operation is already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedProduct == null)
                {
                    ShowTemporaryErrorMessage("Please select a product first.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedProduct.Barcode))
                {
                    ShowTemporaryErrorMessage("This product does not have a barcode assigned.");
                    return;
                }

                IsSaving = true;
                StatusMessage = "Generating barcode...";

                Debug.WriteLine($"Generating barcode for product: {SelectedProduct.Name}, Barcode: {SelectedProduct.Barcode}");

                try
                {
                    // Generate barcode image
                    var barcodeImageData = _barcodeService.GenerateBarcode(SelectedProduct.Barcode, 400, 150);

                    if (barcodeImageData == null || barcodeImageData.Length == 0)
                    {
                        throw new InvalidOperationException("Barcode generation returned null or empty data");
                    }

                    Debug.WriteLine($"Generated barcode image: {barcodeImageData.Length} bytes");

                    // Test loading the image to ensure it's valid
                    var testBitmap = LoadBarcodeImageFromBytes(barcodeImageData);
                    if (testBitmap == null)
                    {
                        throw new InvalidOperationException("Generated barcode image could not be loaded");
                    }

                    Debug.WriteLine($"Barcode image validated: {testBitmap.PixelWidth}x{testBitmap.PixelHeight}");

                    // Update the product with the generated barcode
                    SelectedProduct.BarcodeImage = barcodeImageData;

                    // Save to database
                    var updatedProduct = new ProductDTO
                    {
                        ProductId = SelectedProduct.ProductId,
                        Name = SelectedProduct.Name,
                        Barcode = SelectedProduct.Barcode,
                        BoxBarcode = SelectedProduct.BoxBarcode,
                        CategoryId = SelectedProduct.CategoryId,
                        CategoryName = SelectedProduct.CategoryName,
                        SupplierId = SelectedProduct.SupplierId,
                        SupplierName = SelectedProduct.SupplierName,
                        Description = SelectedProduct.Description,
                        MainStockId = SelectedProduct.MainStockId,
                        CurrentStock = SelectedProduct.CurrentStock,
                        MinimumStock = SelectedProduct.MinimumStock,
                        PurchasePrice = SelectedProduct.PurchasePrice,
                        SalePrice = SelectedProduct.SalePrice,
                        WholesalePrice = SelectedProduct.WholesalePrice,
                        BoxPurchasePrice = SelectedProduct.BoxPurchasePrice,
                        BoxSalePrice = SelectedProduct.BoxSalePrice,
                        BoxWholesalePrice = SelectedProduct.BoxWholesalePrice,
                        ItemsPerBox = SelectedProduct.ItemsPerBox,
                        NumberOfBoxes = SelectedProduct.NumberOfBoxes,
                        MinimumBoxStock = SelectedProduct.MinimumBoxStock,
                        ImagePath = SelectedProduct.ImagePath,
                        Speed = SelectedProduct.Speed,
                        IsActive = SelectedProduct.IsActive,
                        CreatedAt = SelectedProduct.CreatedAt,
                        UpdatedAt = DateTime.Now,
                        BarcodeImage = barcodeImageData // Store the generated barcode
                    };

                    await _productService.UpdateAsync(updatedProduct);

                    // Cache the barcode for quick access
                    _barcodeCache[SelectedProduct.Barcode] = barcodeImageData;

                    StatusMessage = "Barcode generated successfully!";
                    Debug.WriteLine("Barcode generated and saved successfully");

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Barcode generated successfully!", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });

                    await Task.Delay(2000);
                }
                catch (Exception barcodeEx)
                {
                    Debug.WriteLine($"Barcode generation error: {barcodeEx.Message}");
                    ShowTemporaryErrorMessage($"Failed to generate barcode: {barcodeEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error in barcode generation: {ex.Message}");
                ShowTemporaryErrorMessage($"Error generating barcode: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }
        // Update the PrintBarcodeAsync method in ProductViewModel.cs
        private async Task PrintBarcodeAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("A print operation is already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedProduct == null)
                {
                    ShowTemporaryErrorMessage("Please select a product first.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedProduct.Barcode))
                {
                    ShowTemporaryErrorMessage("This product does not have a barcode assigned.");
                    return;
                }

                // Check if barcode image exists
                if (SelectedProduct.BarcodeImage == null || SelectedProduct.BarcodeImage.Length == 0)
                {
                    ShowTemporaryErrorMessage("Please generate the barcode first by clicking 'Generate Barcode' button.");
                    return;
                }

                IsSaving = true;
                StatusMessage = "Preparing to print...";

                Debug.WriteLine($"Using PrinterService to print {LabelsPerProduct} label(s) for product: {SelectedProduct.Name}");

                try
                {
                    // Print each label using YOUR PrinterService with ACTUAL barcode text
                    for (int i = 0; i < LabelsPerProduct; i++)
                    {
                        StatusMessage = $"Printing label {i + 1} of {LabelsPerProduct}...";

                        // Use your existing PrinterService with the ACTUAL barcode text
                        _printerService.PrintBarcode(
                            SelectedProduct.BarcodeImage,
                            SelectedProduct.Name ?? "Unknown Product",
                            $"${SelectedProduct.SalePrice:F2}",
                            SelectedProduct.Barcode // PASS THE ACTUAL BARCODE TEXT
                        );

                        Debug.WriteLine($"Successfully printed label {i + 1} using PrinterService");

                        // Small delay between prints if multiple labels
                        if (i < LabelsPerProduct - 1)
                        {
                            await Task.Delay(500);
                        }
                    }

                    StatusMessage = $"Successfully printed {LabelsPerProduct} label(s)!";
                    Debug.WriteLine($"PrinterService completed - {LabelsPerProduct} labels printed");

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"Successfully printed {LabelsPerProduct} barcode label(s)!",
                            "Print Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception printEx)
                {
                    Debug.WriteLine($"PrinterService error: {printEx.Message}");
                    ShowTemporaryErrorMessage($"Printing failed: {printEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error in barcode printing: {ex.Message}");
                ShowTemporaryErrorMessage($"Error printing barcode: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        private async Task EditProductAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Another operation is in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedProduct == null)
                {
                    ShowTemporaryErrorMessage("Please select a product to edit.");
                    return;
                }

                // Create and show the edit window with the correct constructor parameters
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var editWindow = new Views.ProductEditWindow(
                            SelectedProduct,
                            _productService,      // Pass the service
                            _categoryService,     // Pass the service
                            _supplierService)     // Pass the service
                        {
                            Owner = GetOwnerWindow()
                        };

                        // Show dialog and handle result
                        var result = editWindow.ShowDialog();

                        if (result == true)
                        {
                            // Product was successfully updated
                            // The HandleProductChanged event handler will automatically update the UI
                            // No need to refresh the entire page
                            Debug.WriteLine($"Product {SelectedProduct.Name} was successfully updated");

                            // Recalculate values in case prices or stock changed
                            CalculateSelectedProductValues();
                            CalculateAggregatedValues();

                            // Show success message briefly
                            StatusMessage = "Product updated successfully!";
                            Task.Run(async () =>
                            {
                                await Task.Delay(3000);
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (StatusMessage == "Product updated successfully!")
                                    {
                                        StatusMessage = string.Empty;
                                    }
                                });
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error opening product edit window: {ex.Message}");
                        ShowTemporaryErrorMessage($"Error opening edit window: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in EditProductAsync: {ex.Message}");
                ShowTemporaryErrorMessage($"Error editing product: {ex.Message}");
            }
            finally
            {
                _operationLock.Release();
            }
        }

        // Add these supporting methods to your ProductViewModel.cs class (exactly from old implementation)
        private UIElement CreateHighQualityBarcodeLabel(ProductDTO product, double width, double height)
        {
            // Create a container for the label content with top padding
            var outerCanvas = new Canvas
            {
                Width = width,
                Height = height,
                Background = Brushes.White
            };

            // Create inner canvas for content that will be shifted down
            var canvas = new Canvas
            {
                Width = width,
                Height = height - 15
            };

            // Position the inner canvas with top padding to shift everything down
            Canvas.SetTop(canvas, 15);
            outerCanvas.Children.Add(canvas);

            // Position the barcode image - use most of the available space
            double barcodeWidth = Math.Min(width * 0.9, 600);
            double barcodeHeight = Math.Min(height * 0.5, 200);

            try
            {
                if (product == null)
                {
                    throw new ArgumentNullException("product", "Product cannot be null");
                }

                string displayBarcode = product.Barcode ?? "N/A";
                if (!string.IsNullOrEmpty(displayBarcode) && displayBarcode.Length > 12)
                {
                    Debug.WriteLine($"Warning: Barcode '{displayBarcode}' exceeds 12 digits. It may not scan correctly.");
                }

                // Add product name with improved text quality
                var nameText = product.Name ?? "Unknown Product";
                var nameTextBlock = new TextBlock
                {
                    Text = nameText,
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Width = width * 0.9,
                    MaxHeight = height * 0.15
                };

                // High-quality text rendering
                TextOptions.SetTextRenderingMode(nameTextBlock, TextRenderingMode.ClearType);
                TextOptions.SetTextFormattingMode(nameTextBlock, TextFormattingMode.Display);

                Canvas.SetLeft(nameTextBlock, (width - nameTextBlock.Width) / 2);
                Canvas.SetTop(nameTextBlock, 0);
                canvas.Children.Add(nameTextBlock);

                double barcodeTop = height * 0.15;

                BitmapImage bitmapSource = null;
                if (product.BarcodeImage != null)
                {
                    bitmapSource = LoadHighQualityBarcodeImage(product.BarcodeImage);
                }

                if (bitmapSource == null)
                {
                    // Create a placeholder for missing barcode image
                    var placeholder = new Border
                    {
                        Width = barcodeWidth,
                        Height = barcodeHeight,
                        Background = Brushes.LightGray,
                        BorderBrush = Brushes.Gray,
                        BorderThickness = new Thickness(1)
                    };

                    var placeholderText = new TextBlock
                    {
                        Text = "Barcode Image\nNot Available",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 10,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    };

                    placeholder.Child = placeholderText;

                    Canvas.SetLeft(placeholder, (width - barcodeWidth) / 2);
                    Canvas.SetTop(placeholder, barcodeTop);
                    canvas.Children.Add(placeholder);
                }
                else
                {
                    // Create and position barcode image with enhanced quality settings
                    var barcodeImage = new Image
                    {
                        Source = bitmapSource,
                        Width = barcodeWidth,
                        Height = barcodeHeight,
                        Stretch = Stretch.Uniform,
                        SnapsToDevicePixels = true,
                        UseLayoutRounding = true // Ensures pixel-perfect rendering
                    };

                    // Critical: Use NearestNeighbor for barcodes to prevent smoothing
                    RenderOptions.SetBitmapScalingMode(barcodeImage, BitmapScalingMode.NearestNeighbor);
                    RenderOptions.SetEdgeMode(barcodeImage, EdgeMode.Aliased);
                    RenderOptions.SetClearTypeHint(barcodeImage, ClearTypeHint.Enabled);

                    Canvas.SetLeft(barcodeImage, (width - barcodeWidth) / 2);
                    Canvas.SetTop(barcodeImage, barcodeTop);
                    canvas.Children.Add(barcodeImage);
                }

                // Add barcode text
                var barcodeTextBlock = new TextBlock
                {
                    Text = displayBarcode,
                    FontFamily = new FontFamily("Consolas, Courier New, Monospace"),
                    FontSize = 9,
                    TextAlignment = TextAlignment.Center,
                    Width = width * 0.9
                };

                TextOptions.SetTextRenderingMode(barcodeTextBlock, TextRenderingMode.ClearType);
                TextOptions.SetTextFormattingMode(barcodeTextBlock, TextFormattingMode.Display);

                double barcodeImageBottom = barcodeTop + barcodeHeight;
                Canvas.SetLeft(barcodeTextBlock, (width - barcodeTextBlock.Width) / 2);
                Canvas.SetTop(barcodeTextBlock, barcodeImageBottom + 5);
                canvas.Children.Add(barcodeTextBlock);

                // Add price if needed
                if (product.SalePrice > 0)
                {
                    var priceTextBlock = new TextBlock
                    {
                        Text = $"${product.SalePrice:N2}",
                        FontFamily = new FontFamily("Arial"),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        Width = width * 0.9
                    };

                    TextOptions.SetTextRenderingMode(priceTextBlock, TextRenderingMode.ClearType);
                    TextOptions.SetTextFormattingMode(priceTextBlock, TextFormattingMode.Display);

                    Canvas.SetLeft(priceTextBlock, (width - priceTextBlock.Width) / 2);
                    Canvas.SetTop(priceTextBlock, height * 0.75);
                    canvas.Children.Add(priceTextBlock);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating high-quality barcode label: {ex.Message}");

                var errorTextBlock = new TextBlock
                {
                    Text = $"Error: {ex.Message}",
                    FontFamily = new FontFamily("Arial"),
                    FontSize = 8,
                    TextWrapping = TextWrapping.Wrap,
                    Width = width * 0.9,
                    Foreground = Brushes.Red
                };

                Canvas.SetLeft(errorTextBlock, (width - errorTextBlock.Width) / 2);
                Canvas.SetTop(errorTextBlock, height * 0.7);
                canvas.Children.Add(errorTextBlock);
            }

            return outerCanvas;
        }

        private BitmapImage LoadHighQualityBarcodeImage(byte[] imageData)
        {
            if (imageData == null) return null;

            var image = new BitmapImage();
            try
            {
                using (var ms = new MemoryStream(imageData))
                {
                    image.BeginInit();

                    // High quality loading settings
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat |
                                          BitmapCreateOptions.IgnoreImageCache |
                                          BitmapCreateOptions.IgnoreColorProfile;

                    // Load at full resolution - don't decode at a different size
                    // This preserves the sharp edges of the barcode

                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze(); // Important for cross-thread usage
                }
                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading high-quality barcode image: {ex.Message}");
                return null;
            }
        }
        // Enhanced barcode image loading method
        private BitmapImage LoadBarcodeImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                Debug.WriteLine("LoadBarcodeImageFromBytes: No image data provided");
                return null;
            }

            try
            {
                var image = new BitmapImage();
                using (var ms = new MemoryStream(imageData))
                {
                    ms.Position = 0; // Ensure we're at the beginning
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze(); // Make it thread-safe and improve performance
                }

                Debug.WriteLine($"LoadBarcodeImageFromBytes: Successfully loaded barcode image {image.PixelWidth}x{image.PixelHeight}");
                return image;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadBarcodeImageFromBytes: Error loading barcode image: {ex.Message}");

                // Try alternative loading method
                try
                {
                    Debug.WriteLine("LoadBarcodeImageFromBytes: Trying alternative loading method...");
                    var image = new BitmapImage();
                    using (var ms = new MemoryStream(imageData))
                    {
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.CreateOptions = BitmapCreateOptions.None;
                        image.StreamSource = ms;
                        image.EndInit();
                    }
                    image.Freeze();
                    Debug.WriteLine($"LoadBarcodeImageFromBytes: Alternative method succeeded {image.PixelWidth}x{image.PixelHeight}");
                    return image;
                }
                catch (Exception altEx)
                {
                    Debug.WriteLine($"LoadBarcodeImageFromBytes: Alternative method also failed: {altEx.Message}");
                    return null;
                }
            }
        }

        // Test method for debugging barcode generation
        private void TestBarcodeGeneration(string testBarcode = "123456789012")
        {
            try
            {
                Debug.WriteLine($"Testing barcode generation with: {testBarcode}");

                var barcodeBytes = _barcodeService.GenerateBarcode(testBarcode, 300, 100);

                if (barcodeBytes == null)
                {
                    Debug.WriteLine("ERROR: Barcode generation returned null");
                    return;
                }

                Debug.WriteLine($"SUCCESS: Generated barcode with {barcodeBytes.Length} bytes");

                var bitmapImage = LoadBarcodeImageFromBytes(barcodeBytes);
                if (bitmapImage != null)
                {
                    Debug.WriteLine($"SUCCESS: Loaded bitmap image {bitmapImage.PixelWidth}x{bitmapImage.PixelHeight}");
                }
                else
                {
                    Debug.WriteLine("ERROR: Failed to load bitmap image from bytes");
                }

                // Test with a shorter barcode as well
                if (testBarcode.Length > 12)
                {
                    var shortBarcode = "123456789";
                    Debug.WriteLine($"Testing with shorter barcode: {shortBarcode}");
                    var shortBarcodeBytes = _barcodeService.GenerateBarcode(shortBarcode, 300, 100);
                    if (shortBarcodeBytes != null)
                    {
                        Debug.WriteLine($"SUCCESS: Short barcode generated with {shortBarcodeBytes.Length} bytes");
                        var shortBitmapImage = LoadBarcodeImageFromBytes(shortBarcodeBytes);
                        if (shortBitmapImage != null)
                        {
                            Debug.WriteLine($"SUCCESS: Short barcode loaded as {shortBitmapImage.PixelWidth}x{shortBitmapImage.PixelHeight}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR in TestBarcodeGeneration: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Keep all the existing methods unchanged...
        // [Rest of the methods remain the same - SubscribeToEvents, UnsubscribeFromEvents, etc.]

        protected override void SubscribeToEvents()
        {
            Debug.WriteLine("ProductViewModel: Subscribing to events");
            _eventAggregator.Subscribe<EntityChangedEvent<ProductDTO>>(_productChangedHandler);
            _eventAggregator.Subscribe<EntityChangedEvent<CategoryDTO>>(_categoryChangedHandler);
            _eventAggregator.Subscribe<EntityChangedEvent<MainStockDTO>>(_mainStockChangedHandler);
            _eventAggregator.Subscribe<ProductStockUpdatedEvent>(_productStockUpdatedHandler);
            _eventAggregator.Subscribe<GlobalDataRefreshEvent>(HandleGlobalRefresh);
            Debug.WriteLine("ProductViewModel: Subscribed to all events");
        }

        protected override void UnsubscribeFromEvents()
        {
            _eventAggregator.Unsubscribe<EntityChangedEvent<ProductDTO>>(_productChangedHandler);
            _eventAggregator.Unsubscribe<EntityChangedEvent<CategoryDTO>>(_categoryChangedHandler);
            _eventAggregator.Unsubscribe<EntityChangedEvent<MainStockDTO>>(_mainStockChangedHandler);
            _eventAggregator.Unsubscribe<ProductStockUpdatedEvent>(_productStockUpdatedHandler);
            _eventAggregator.Unsubscribe<GlobalDataRefreshEvent>(HandleGlobalRefresh);
        }

        private async void HandleGlobalRefresh(GlobalDataRefreshEvent evt)
        {
            Debug.WriteLine($"ProductViewModel: Received global refresh event at {evt.Timestamp}");
            await Task.Delay(800);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _currentPage = 1;
                OnPropertyChanged(nameof(CurrentPage));
            });
            await ForceRefreshDataAsync();
        }

        private async void HandleProductStockUpdated(ProductStockUpdatedEvent evt)
        {
            Debug.WriteLine($"ProductViewModel: Product stock updated - ID: {evt.ProductId}, New Stock: {evt.NewStock}");
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var product = Products.FirstOrDefault(p => p.ProductId == evt.ProductId);
                    if (product != null)
                    {
                        product.CurrentStock = (int)Math.Round(evt.NewStock);
                        Debug.WriteLine($"ProductViewModel: Updated product {product.Name} stock to {product.CurrentStock}");

                        if (SelectedProduct != null && SelectedProduct.ProductId == evt.ProductId)
                        {
                            SelectedProduct.CurrentStock = (int)Math.Round(evt.NewStock);
                            CalculateSelectedProductValues();
                        }
                        CalculateAggregatedValues();
                    }
                    else
                    {
                        Debug.WriteLine("ProductViewModel: Product not found in current page, forcing reload");
                        Task.Run(async () => await ForceRefreshDataAsync());
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProductViewModel: Error handling product stock update: {ex.Message}");
            }
        }

        private void SelectedProduct_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProductDTO.PurchasePrice) ||
                e.PropertyName == nameof(ProductDTO.SalePrice) ||
                e.PropertyName == nameof(ProductDTO.CurrentStock))
            {
                CalculateSelectedProductValues();
            }
        }

        private async void HandleMainStockChanged(EntityChangedEvent<MainStockDTO> evt)
        {
            try
            {
                Debug.WriteLine($"ProductViewModel: Handling MainStock change {evt.Action} for ID {evt.Entity.MainStockId}");
                var linkedProducts = Products.Where(p => p.MainStockId.HasValue && p.MainStockId.Value == evt.Entity.MainStockId).ToList();

                if (linkedProducts.Any())
                {
                    bool valuesChanged = false;
                    foreach (var product in linkedProducts)
                    {
                        if (Math.Abs(product.PurchasePrice - evt.Entity.PurchasePrice) > 0.001m)
                        {
                            product.PurchasePrice = evt.Entity.PurchasePrice;
                            valuesChanged = true;
                        }
                        if (Math.Abs(product.SalePrice - evt.Entity.SalePrice) > 0.001m)
                        {
                            product.SalePrice = evt.Entity.SalePrice;
                            valuesChanged = true;
                        }
                        if (Math.Abs(product.BoxPurchasePrice - evt.Entity.BoxPurchasePrice) > 0.001m)
                        {
                            product.BoxPurchasePrice = evt.Entity.BoxPurchasePrice;
                            valuesChanged = true;
                        }
                        if (Math.Abs(product.BoxSalePrice - evt.Entity.BoxSalePrice) > 0.001m)
                        {
                            product.BoxSalePrice = evt.Entity.BoxSalePrice;
                            valuesChanged = true;
                        }
                        if (product.ItemsPerBox != evt.Entity.ItemsPerBox)
                        {
                            product.ItemsPerBox = evt.Entity.ItemsPerBox;
                            valuesChanged = true;
                        }
                    }

                    if (valuesChanged)
                    {
                        CalculateAggregatedValues();
                        if (SelectedProduct != null && linkedProducts.Any(p => p.ProductId == SelectedProduct.ProductId))
                        {
                            CalculateSelectedProductValues();
                        }
                        await Task.Delay(500);
                        await ForceRefreshDataAsync();
                    }
                }
                else
                {
                    await ForceRefreshDataAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProductViewModel: Error handling MainStock change: {ex.Message}");
            }
        }

        private async void HandleCategoryChanged(EntityChangedEvent<CategoryDTO> evt)
        {
            try
            {
                Debug.WriteLine("ProductViewModel: Handling category change");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    switch (evt.Action)
                    {
                        case "Create":
                            if (evt.Entity.IsActive && !Categories.Any(c => c.CategoryId == evt.Entity.CategoryId))
                            {
                                Categories.Add(evt.Entity);
                                Debug.WriteLine($"Added new category {evt.Entity.Name}");
                            }
                            break;
                        case "Update":
                            var existingIndex = Categories.ToList().FindIndex(c => c.CategoryId == evt.Entity.CategoryId);
                            if (existingIndex != -1)
                            {
                                if (evt.Entity.IsActive)
                                {
                                    Categories[existingIndex] = evt.Entity;
                                    Debug.WriteLine($"Updated category {evt.Entity.Name}");
                                }
                                else
                                {
                                    Categories.RemoveAt(existingIndex);
                                    Debug.WriteLine($"Removed inactive category {evt.Entity.Name}");
                                }
                            }
                            else if (evt.Entity.IsActive)
                            {
                                Categories.Add(evt.Entity);
                                Debug.WriteLine($"Added newly active category {evt.Entity.Name}");
                            }
                            break;
                        case "Delete":
                            var categoryToRemove = Categories.FirstOrDefault(c => c.CategoryId == evt.Entity.CategoryId);
                            if (categoryToRemove != null)
                            {
                                Categories.Remove(categoryToRemove);
                                Debug.WriteLine($"Removed category {categoryToRemove.Name}");
                            }
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProductViewModel: Error handling category change: {ex.Message}");
            }
        }

        private async void HandleProductChanged(EntityChangedEvent<ProductDTO> evt)
        {
            try
            {
                Debug.WriteLine($"Handling {evt.Action} event for product {evt.Entity.Name} (ID: {evt.Entity.ProductId})");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    switch (evt.Action)
                    {
                        case "Create":
                            Debug.WriteLine("Adding new product to collection");
                            if (!Products.Any(p => p.ProductId == evt.Entity.ProductId))
                            {
                                Products.Add(evt.Entity);
                                Debug.WriteLine("Product added to collection");
                            }
                            break;

                        case "Update":
                            Debug.WriteLine("Updating product in collection");
                            var existingProduct = Products.FirstOrDefault(p => p.ProductId == evt.Entity.ProductId);
                            if (existingProduct != null)
                            {
                                var index = Products.IndexOf(existingProduct);

                                // Preserve selection if this is the selected product
                                bool wasSelected = SelectedProduct != null && SelectedProduct.ProductId == evt.Entity.ProductId;

                                // Update the product in the collection
                                Products[index] = evt.Entity;

                                // Restore selection if it was selected
                                if (wasSelected)
                                {
                                    SelectedProduct = evt.Entity;
                                }

                                Debug.WriteLine("Product updated in collection without refresh");
                            }
                            else
                            {
                                // Product not in current view, check if it should be added due to search criteria
                                if (string.IsNullOrWhiteSpace(SearchText) ||
                                    evt.Entity.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                    evt.Entity.Barcode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                                    evt.Entity.CategoryName.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                                {
                                    // Only refresh if the product should be visible but isn't
                                    Task.Run(async () => await SafeLoadDataAsync());
                                    Debug.WriteLine("Scheduled reload for updated product not in current view");
                                }
                            }
                            break;

                        case "Delete":
                            Debug.WriteLine("Removing product from collection");
                            var productToRemove = Products.FirstOrDefault(p => p.ProductId == evt.Entity.ProductId);
                            if (productToRemove != null)
                            {
                                Products.Remove(productToRemove);

                                // Clear selection if the deleted product was selected
                                if (SelectedProduct != null && SelectedProduct.ProductId == evt.Entity.ProductId)
                                {
                                    SelectedProduct = null;
                                }

                                Debug.WriteLine("Product removed from collection");
                            }
                            break;
                    }

                    // Clear barcode cache for updated/deleted products
                    if (evt.Action == "Update" || evt.Action == "Delete")
                    {
                        if (!string.IsNullOrWhiteSpace(evt.Entity.Barcode) && _barcodeCache.ContainsKey(evt.Entity.Barcode))
                        {
                            _barcodeCache.Remove(evt.Entity.Barcode);
                            Debug.WriteLine($"Cleared barcode cache for: {evt.Entity.Barcode}");
                        }
                    }

                    CalculateAggregatedValues();

                    // Only filter if search is active
                    if (!string.IsNullOrWhiteSpace(SearchText))
                    {
                        FilterProducts();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Product change handling error: {ex.Message}");
            }
        }

        private void UpdateVisiblePageNumbers()
        {
            var visiblePages = new List<int>();
            int startPage = Math.Max(1, CurrentPage - 2);
            int endPage = Math.Min(TotalPages, CurrentPage + 2);

            if (startPage > 1)
            {
                visiblePages.Add(1);
                if (startPage > 2) visiblePages.Add(-1);
            }

            for (int i = startPage; i <= endPage; i++)
            {
                visiblePages.Add(i);
            }

            if (endPage < TotalPages)
            {
                if (endPage < TotalPages - 1) visiblePages.Add(-1);
                visiblePages.Add(TotalPages);
            }

            VisiblePageNumbers = visiblePages;
            OnPropertyChanged(nameof(VisiblePageNumbers));
        }

        private void CalculateSelectedProductValues()
        {
            try
            {
                if (SelectedProduct == null)
                {
                    SelectedProductTotalCost = 0;
                    SelectedProductTotalValue = 0;
                    SelectedProductProfitMargin = 0;
                    SelectedProductProfitPercentage = 0;
                    return;
                }

                SelectedProductTotalCost = SelectedProduct.PurchasePrice * SelectedProduct.CurrentStock;
                SelectedProductTotalValue = SelectedProduct.SalePrice * SelectedProduct.CurrentStock;
                SelectedProductProfitMargin = SelectedProductTotalValue - SelectedProductTotalCost;

                if (SelectedProductTotalCost > 0)
                {
                    SelectedProductProfitPercentage = (SelectedProductProfitMargin / SelectedProductTotalCost) * 100;
                }
                else
                {
                    SelectedProductProfitPercentage = 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating selected product values: {ex.Message}");
                SelectedProductTotalCost = 0;
                SelectedProductTotalValue = 0;
                SelectedProductProfitMargin = 0;
                SelectedProductProfitPercentage = 0;
            }
        }

        private void CalculateAggregatedValues()
        {
            try
            {
                if (Products == null || !Products.Any())
                {
                    TotalPurchaseValue = 0;
                    TotalSaleValue = 0;
                    TotalProfit = 0;
                    TotalBoxes = 0;
                    return;
                }

                decimal totalPurchase = 0;
                decimal totalSale = 0;
                int totalBoxes = 0;

                foreach (var product in Products)
                {
                    try
                    {
                        totalPurchase += Math.Round(product.PurchasePrice * product.CurrentStock, 2);
                        totalSale += Math.Round(product.SalePrice * product.CurrentStock, 2);
                        totalBoxes += product.NumberOfBoxes;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error calculating values for product {product.Name}: {ex.Message}");
                    }
                }

                TotalPurchaseValue = totalPurchase;
                TotalSaleValue = totalSale;
                TotalProfit = totalSale - totalPurchase;
                TotalBoxes = totalBoxes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating aggregated values: {ex.Message}");
                TotalPurchaseValue = 0;
                TotalSaleValue = 0;
                TotalProfit = 0;
                TotalBoxes = 0;
            }
        }

        public async Task ForceRefreshDataAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("ProductViewModel: ForceRefreshDataAsync - another refresh operation is in progress");
                return;
            }

            try
            {
                Debug.WriteLine("ProductViewModel: Forcing complete data refresh");
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                bool success = false;
                int maxRetries = 3;

                for (int attempt = 0; attempt < maxRetries && !success; attempt++)
                {
                    try
                    {
                        Debug.WriteLine($"ForceRefreshDataAsync attempt {attempt + 1}");
                        await _dbContextScopeService.ExecuteInScopeAsync(async context =>
                        {
                            IsSaving = true;
                            StatusMessage = "Refreshing data...";

                            var products = await _productService.GetAllAsync();
                            var categories = await _categoryService.GetActiveAsync();
                            var suppliers = await _supplierService.GetActiveAsync();
                            var mainStocks = await _mainStockService.GetAllAsync();

                            await Task.Delay(100);

                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                var pagedProducts = products
                                    .Skip((CurrentPage - 1) * PageSize)
                                    .Take(PageSize)
                                    .ToList();

                                Products = new ObservableCollection<ProductDTO>(pagedProducts);
                                Categories = new ObservableCollection<CategoryDTO>(categories);
                                Suppliers = new ObservableCollection<SupplierDTO>(suppliers);
                                MainStockItems = new ObservableCollection<MainStockDTO>(mainStocks);

                                TotalProducts = products.Count();
                                TotalPages = (int)Math.Ceiling(TotalProducts / (double)PageSize);
                                UpdateVisiblePageNumbers();

                                CalculateAggregatedValues();
                                if (SelectedProduct != null)
                                {
                                    CalculateSelectedProductValues();
                                }

                                Debug.WriteLine($"ProductViewModel: Data refresh complete. {Products.Count} products loaded.");
                            });

                            return true;
                        });

                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"ProductViewModel: Error during forced refresh (attempt {attempt + 1}): {ex.Message}");
                        await Task.Delay(500 * (attempt + 1));
                    }
                }

                if (!success)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            "Unable to refresh product data after multiple attempts. Please try again later.",
                            "Refresh Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        private async Task SafeLoadDataAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("SafeLoadDataAsync skipped - already in progress");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                IsSaving = true;
                StatusMessage = "Loading data...";

                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

                    var categoriesTask = _categoryService.GetActiveAsync();
                    var suppliersTask = _supplierService.GetActiveAsync();
                    var mainStockTask = _mainStockService.GetAllAsync();

                    var totalCount = await GetTotalProductCount();
                    if (linkedCts.Token.IsCancellationRequested) return;

                    int calculatedTotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);
                    TotalPages = calculatedTotalPages;
                    TotalProducts = totalCount;

                    var products = await GetPagedProductsWithMainStockSync(CurrentPage, PageSize, SearchText);
                    if (linkedCts.Token.IsCancellationRequested) return;

                    await Task.WhenAll(categoriesTask, suppliersTask, mainStockTask);
                    if (linkedCts.Token.IsCancellationRequested) return;

                    var categories = await categoriesTask;
                    var suppliers = await suppliersTask;
                    var mainStockItems = await mainStockTask;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!linkedCts.Token.IsCancellationRequested)
                        {
                            Products = new ObservableCollection<ProductDTO>(products);
                            FilteredProducts = new ObservableCollection<ProductDTO>(products);
                            Categories = new ObservableCollection<CategoryDTO>(categories);
                            Suppliers = new ObservableCollection<SupplierDTO>(suppliers);
                            MainStockItems = new ObservableCollection<MainStockDTO>(mainStockItems);

                            CalculateAggregatedValues();

                            if (SelectedProduct != null)
                            {
                                CalculateSelectedProductValues();
                            }
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation was canceled");
                }
                catch (Exception ex)
                {
                    ShowTemporaryErrorMessage($"Error loading data: {ex.Message}");
                }
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        private async Task<int> GetTotalProductCount()
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var allProducts = await _productService.GetAllAsync();
                return allProducts.Count(p =>
                    p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Barcode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    p.CategoryName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    p.SupplierName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    (p.Speed?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            else
            {
                var allProducts = await _productService.GetAllAsync();
                return allProducts.Count();
            }
        }

        private async Task<List<ProductDTO>> GetPagedProductsWithMainStockSync(int page, int pageSize, string searchText)
        {
            var allProducts = await _productService.GetAllAsync();
            var mainStockItems = await _mainStockService.GetAllAsync();
            var mainStockLookup = mainStockItems.ToDictionary(m => m.MainStockId);

            foreach (var product in allProducts.Where(p => p.MainStockId.HasValue))
            {
                if (mainStockLookup.TryGetValue(product.MainStockId.Value, out var mainStock))
                {
                    product.PurchasePrice = mainStock.PurchasePrice;
                    product.SalePrice = mainStock.SalePrice;
                    product.BoxPurchasePrice = mainStock.BoxPurchasePrice;
                    product.BoxSalePrice = mainStock.BoxSalePrice;
                    product.ItemsPerBox = mainStock.ItemsPerBox;
                }
            }

            IEnumerable<ProductDTO> filteredProducts = allProducts;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredProducts = allProducts.Where(p =>
                    p.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.Barcode.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.CategoryName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    p.SupplierName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (p.Speed?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return filteredProducts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        protected override async Task LoadDataAsync()
        {
            await SafeLoadDataAsync();
        }

        private void FilterProducts()
        {
            _ = SafeLoadDataAsync();
        }

        private async Task UpdateStockAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Stock update operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedProduct == null || StockIncrement <= 0)
                {
                    ShowTemporaryErrorMessage("Please select a product and enter a valid stock increment.");
                    return;
                }

                IsSaving = true;
                StatusMessage = "Updating stock...";

                var newStock = SelectedProduct.CurrentStock + StockIncrement;
                SelectedProduct.CurrentStock = newStock;

                await _productService.UpdateStockAsync(SelectedProduct.ProductId, StockIncrement);

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Stock updated successfully. New stock: {newStock}",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                });

                StockIncrement = 0;

                CalculateSelectedProductValues();
                CalculateAggregatedValues();
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error updating stock: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        private async Task SyncWithMainStockAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Another operation is in progress. Please wait.");
                return;
            }

            try
            {
                IsSaving = true;
                StatusMessage = "Synchronizing with MainStock...";

                var productsWithMainStock = Products.Where(p => p.MainStockId.HasValue).ToList();

                if (!productsWithMainStock.Any())
                {
                    StatusMessage = "No products linked to MainStock found.";
                    await Task.Delay(2000);
                    return;
                }

                int syncCount = 0;
                int errorCount = 0;

                var mainStockItems = await _mainStockService.GetAllAsync();
                var mainStockLookup = mainStockItems.ToDictionary(m => m.MainStockId);

                foreach (var product in productsWithMainStock)
                {
                    try
                    {
                        StatusMessage = $"Synchronizing product {product.Name}...";

                        if (mainStockLookup.TryGetValue(product.MainStockId.Value, out var mainStock))
                        {
                            bool changes = false;

                            if (Math.Abs(product.PurchasePrice - mainStock.PurchasePrice) > 0.001m)
                            {
                                product.PurchasePrice = mainStock.PurchasePrice;
                                changes = true;
                            }

                            if (Math.Abs(product.SalePrice - mainStock.SalePrice) > 0.001m)
                            {
                                product.SalePrice = mainStock.SalePrice;
                                changes = true;
                            }

                            if (Math.Abs(product.BoxPurchasePrice - mainStock.BoxPurchasePrice) > 0.001m)
                            {
                                product.BoxPurchasePrice = mainStock.BoxPurchasePrice;
                                changes = true;
                            }

                            if (Math.Abs(product.BoxSalePrice - mainStock.BoxSalePrice) > 0.001m)
                            {
                                product.BoxSalePrice = mainStock.BoxSalePrice;
                                changes = true;
                            }

                            if (product.ItemsPerBox != mainStock.ItemsPerBox)
                            {
                                product.ItemsPerBox = mainStock.ItemsPerBox;
                                changes = true;
                            }

                            if (changes)
                            {
                                var updatedProduct = new ProductDTO
                                {
                                    ProductId = product.ProductId,
                                    Name = product.Name,
                                    Barcode = product.Barcode,
                                    BoxBarcode = product.BoxBarcode,
                                    CategoryId = product.CategoryId,
                                    CategoryName = product.CategoryName,
                                    SupplierId = product.SupplierId,
                                    SupplierName = product.SupplierName,
                                    Description = product.Description,
                                    MainStockId = product.MainStockId,
                                    CurrentStock = product.CurrentStock,
                                    MinimumStock = product.MinimumStock,
                                    ImagePath = product.ImagePath,
                                    Speed = product.Speed,
                                    IsActive = product.IsActive,
                                    CreatedAt = product.CreatedAt,
                                    UpdatedAt = DateTime.Now,
                                    PurchasePrice = mainStock.PurchasePrice,
                                    SalePrice = mainStock.SalePrice,
                                    BoxPurchasePrice = mainStock.BoxPurchasePrice,
                                    BoxSalePrice = mainStock.BoxSalePrice,
                                    ItemsPerBox = mainStock.ItemsPerBox,
                                    MinimumBoxStock = mainStock.MinimumBoxStock
                                };

                                await _productService.UpdateAsync(updatedProduct);

                                product.PurchasePrice = mainStock.PurchasePrice;
                                product.SalePrice = mainStock.SalePrice;
                                product.BoxPurchasePrice = mainStock.BoxPurchasePrice;
                                product.BoxSalePrice = mainStock.BoxSalePrice;
                                product.ItemsPerBox = mainStock.ItemsPerBox;
                                product.UpdatedAt = DateTime.Now;

                                syncCount++;

                                if (syncCount % 5 == 0)
                                {
                                    StatusMessage = $"Synchronized {syncCount} products...";
                                    await Task.Delay(10);
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Warning: Referenced MainStock ID {product.MainStockId} not found for product {product.ProductId}");
                            errorCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error syncing product {product.ProductId}: {ex.Message}");
                        errorCount++;
                    }
                }

                CalculateAggregatedValues();
                if (SelectedProduct != null)
                {
                    CalculateSelectedProductValues();
                }

                StatusMessage = $"Synchronized {syncCount} products with MainStock data.";

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (errorCount > 0)
                    {
                        MessageBox.Show($"Successfully synchronized {syncCount} products with MainStock data.\n\n{errorCount} product(s) had errors during synchronization. See application logs for details.",
                            "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Successfully synchronized {syncCount} products with MainStock data.",
                            "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error synchronizing with MainStock: {ex.Message}";
                Debug.WriteLine($"Error in SyncWithMainStockAsync: {ex}");

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Error during synchronization: {ex.Message}",
                        "Synchronization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsSaving = false;
                _operationLock.Release();
            }
        }

        private Window GetOwnerWindow()
        {
            var activeWindow = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (activeWindow != null)
                return activeWindow;

            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.IsLoaded)
                return mainWindow;

            return System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible)
                   ?? System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault();
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            StatusMessage = message;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (StatusMessage == message)
                    {
                        StatusMessage = string.Empty;
                    }
                });
            });
        }

        public override void Dispose()
        {
            if (!_isDisposed)
            {
                if (SelectedProduct != null)
                {
                    SelectedProduct.PropertyChanged -= SelectedProduct_PropertyChanged;
                }

                _cts?.Cancel();
                _cts?.Dispose();
                _operationLock?.Dispose();
                _barcodeCache?.Clear();
                UnsubscribeFromEvents();
                _eventAggregator.Unsubscribe<GlobalDataRefreshEvent>(HandleGlobalRefresh);
                _isDisposed = true;
            }

            base.Dispose();
        }
    }
}