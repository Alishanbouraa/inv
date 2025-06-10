// Path: QuickTechSystems.WPF.ViewModels/DamagedGoodsViewModel.cs
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
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.WPF.ViewModels
{
    public class DamagedGoodsViewModel : ViewModelBase
    {
        private readonly IDamagedGoodsService _damagedGoodsService;
        private readonly IProductService _productService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;

        private ObservableCollection<DamagedGoodsDTO> _damagedGoods;
        private ObservableCollection<ProductDTO> _searchResults;
        private DamagedGoodsDTO? _selectedDamagedItem;
        private ProductDTO? _selectedProduct;
        private bool _isDamagedItemPopupOpen;
        private bool _isProductSearchPopupOpen;
        private bool _isProcessing;
        private string _statusMessage = string.Empty;
        private string _searchText = string.Empty;
        private int _quantity = 1;
        private string _reason = string.Empty;
        private DateTime _startDate = DateTime.Now.AddDays(-30);
        private DateTime _endDate = DateTime.Now;
        private decimal _totalLoss;
        private Action<EntityChangedEvent<DamagedGoodsDTO>> _damagedGoodsChangedHandler;
        private Action<EntityChangedEvent<ProductDTO>> _productChangedHandler;
        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set => SetProperty(ref _isEditMode, value);
        }
        public ObservableCollection<DamagedGoodsDTO> DamagedGoods
        {
            get => _damagedGoods;
            set => SetProperty(ref _damagedGoods, value);
        }

        public ObservableCollection<ProductDTO> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public DamagedGoodsDTO? SelectedDamagedItem
        {
            get => _selectedDamagedItem;
            set => SetProperty(ref _selectedDamagedItem, value);
        }

        public ProductDTO? SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
        }

        public bool IsDamagedItemPopupOpen
        {
            get => _isDamagedItemPopupOpen;
            set => SetProperty(ref _isDamagedItemPopupOpen, value);
        }

        public bool IsProductSearchPopupOpen
        {
            get => _isProductSearchPopupOpen;
            set => SetProperty(ref _isProductSearchPopupOpen, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                OnPropertyChanged(nameof(IsNotProcessing));
            }
        }

        public bool IsNotProcessing => !IsProcessing;

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                if (!string.IsNullOrWhiteSpace(value) && value.Length >= 3)
                {
                    SearchProducts();
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, Math.Max(1, value));
        }

        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                SetProperty(ref _startDate, value);
                LoadDamagedGoodsByDateRangeAsync();
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                SetProperty(ref _endDate, value);
                LoadDamagedGoodsByDateRangeAsync();
            }
        }

        public decimal TotalLoss
        {
            get => _totalLoss;
            set => SetProperty(ref _totalLoss, value);
        }

        // Commands
        public ICommand LoadCommand { get; private set; }
        public ICommand OpenAddDamagedItemCommand { get; private set; }
        public ICommand OpenSearchProductCommand { get; private set; }
        public ICommand SelectProductCommand { get; private set; }
        public ICommand RegisterDamagedGoodsCommand { get; private set; }
        public ICommand EditDamagedItemCommand { get; private set; }
        public ICommand DeleteDamagedItemCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand UpdateDamagedGoodsCommand { get; private set; }

        public DamagedGoodsViewModel(
            IDamagedGoodsService damagedGoodsService,
            IProductService productService,
            IEventAggregator eventAggregator) : base(eventAggregator)
        {
            Debug.WriteLine("Initializing DamagedGoodsViewModel");
            _damagedGoodsService = damagedGoodsService ?? throw new ArgumentNullException(nameof(damagedGoodsService));
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));

            _damagedGoods = new ObservableCollection<DamagedGoodsDTO>();
            _searchResults = new ObservableCollection<ProductDTO>();
            _damagedGoodsChangedHandler = HandleDamagedGoodsChanged;
            _productChangedHandler = HandleProductChanged;

            SubscribeToEvents();
            InitializeCommands();
            _ = LoadDataAsync();
            Debug.WriteLine("DamagedGoodsViewModel initialized");
        }

        private void InitializeCommands()
        {
            LoadCommand = new AsyncRelayCommand(async _ => await LoadDataAsync(), _ => !IsProcessing);
            OpenAddDamagedItemCommand = new RelayCommand(_ => OpenAddDamagedItemPopup(), _ => !IsProcessing);
            OpenSearchProductCommand = new RelayCommand(_ => OpenSearchProductPopup(), _ => !IsProcessing);
            SelectProductCommand = new RelayCommand(SelectProduct, _ => !IsProcessing && SelectedProduct != null);
            RegisterDamagedGoodsCommand = new AsyncRelayCommand(async _ => await RegisterDamagedGoodsAsync(), _ => !IsProcessing && SelectedProduct != null && Quantity > 0 && !string.IsNullOrWhiteSpace(Reason));
            EditDamagedItemCommand = new AsyncRelayCommand(async param => await EditDamagedItemAsync(param as DamagedGoodsDTO), _ => !IsProcessing);
            DeleteDamagedItemCommand = new AsyncRelayCommand(async param => await DeleteDamagedItemAsync(param as DamagedGoodsDTO), _ => !IsProcessing);
            RefreshCommand = new AsyncRelayCommand(async _ => await LoadDamagedGoodsByDateRangeAsync(), _ => !IsProcessing);
            UpdateDamagedGoodsCommand = new AsyncRelayCommand(async _ => await UpdateDamagedGoodsAsync(), _ => !IsProcessing && SelectedDamagedItem != null && SelectedProduct != null && Quantity > 0 && !string.IsNullOrWhiteSpace(Reason));
        }

        protected override void SubscribeToEvents()
        {
            Debug.WriteLine("DamagedGoodsViewModel: Subscribing to events");
            _eventAggregator.Subscribe<EntityChangedEvent<DamagedGoodsDTO>>(_damagedGoodsChangedHandler);
            _eventAggregator.Subscribe<EntityChangedEvent<ProductDTO>>(_productChangedHandler);
            Debug.WriteLine("DamagedGoodsViewModel: Subscribed to all events");
        }

        protected override void UnsubscribeFromEvents()
        {
            _eventAggregator.Unsubscribe<EntityChangedEvent<DamagedGoodsDTO>>(_damagedGoodsChangedHandler);
            _eventAggregator.Unsubscribe<EntityChangedEvent<ProductDTO>>(_productChangedHandler);
        }

        private async void HandleDamagedGoodsChanged(EntityChangedEvent<DamagedGoodsDTO> evt)
        {
            try
            {
                Debug.WriteLine($"Handling {evt.Action} event for damaged goods");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    switch (evt.Action)
                    {
                        case "Create":
                            Debug.WriteLine("Adding new damaged goods record to collection");
                            if (!DamagedGoods.Any(d => d.DamagedGoodsId == evt.Entity.DamagedGoodsId))
                            {
                                DamagedGoods.Insert(0, evt.Entity);
                                Debug.WriteLine("Damaged goods record added to collection");
                            }
                            break;

                        case "Update":
                            Debug.WriteLine("Updating damaged goods record in collection");
                            var existingRecord = DamagedGoods.FirstOrDefault(d => d.DamagedGoodsId == evt.Entity.DamagedGoodsId);
                            if (existingRecord != null)
                            {
                                var index = DamagedGoods.IndexOf(existingRecord);
                                DamagedGoods[index] = evt.Entity;
                                Debug.WriteLine("Damaged goods record updated in collection");
                            }
                            break;

                        case "Delete":
                            Debug.WriteLine("Removing damaged goods record from collection");
                            var recordToRemove = DamagedGoods.FirstOrDefault(d => d.DamagedGoodsId == evt.Entity.DamagedGoodsId);
                            if (recordToRemove != null)
                            {
                                DamagedGoods.Remove(recordToRemove);
                                Debug.WriteLine("Damaged goods record removed from collection");
                            }
                            break;
                    }
                });

                // Update the total loss amount
                await LoadTotalLossAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Damaged goods refresh error: {ex.Message}");
            }
        }

        private async void HandleProductChanged(EntityChangedEvent<ProductDTO> evt)
        {
            try
            {
                Debug.WriteLine($"Handling {evt.Action} event for product related to damaged goods");

                if (SelectedProduct != null && evt.Entity.ProductId == SelectedProduct.ProductId)
                {
                    // Update the SelectedProduct if it's the one that changed
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SelectedProduct = evt.Entity;
                    });
                }

                // Refresh search results if needed
                if (IsProductSearchPopupOpen && !string.IsNullOrWhiteSpace(SearchText))
                {
                    SearchProducts();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Product refresh error in damaged goods: {ex.Message}");
            }
        }

        protected override async Task LoadDataAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadDataAsync skipped - already in progress");
                return;
            }

            try
            {
                IsProcessing = true;
                StatusMessage = "Loading damaged goods...";

                await LoadDamagedGoodsByDateRangeAsync();
                await LoadTotalLossAsync();
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading damaged goods data: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        private async Task LoadDamagedGoodsByDateRangeAsync()
        {
            try
            {
                var damagedGoodsList = await _damagedGoodsService.GetByDateRangeAsync(StartDate, EndDate);
                DamagedGoods = new ObservableCollection<DamagedGoodsDTO>(damagedGoodsList);
                Debug.WriteLine($"Loaded {DamagedGoods.Count} damaged goods records");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading damaged goods by date range: {ex.Message}");
                throw;
            }
        }

        private async Task LoadTotalLossAsync()
        {
            try
            {
                TotalLoss = await _damagedGoodsService.GetTotalLossAmountAsync(StartDate, EndDate);
                Debug.WriteLine($"Total loss amount: {TotalLoss:C2}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading total loss amount: {ex.Message}");
                throw;
            }
        }

        private void OpenAddDamagedItemPopup()
        {
            SelectedDamagedItem = null;
            SelectedProduct = null;
            Quantity = 1;
            Reason = string.Empty;
            IsEditMode = false;
            IsDamagedItemPopupOpen = true;
        }

        private void OpenSearchProductPopup()
        {
            SearchText = string.Empty;
            SearchResults.Clear();
            IsProductSearchPopupOpen = true;
        }

        private void SelectProduct(object parameter)
        {
            if (parameter is ProductDTO product)
            {
                SelectedProduct = product;
                IsProductSearchPopupOpen = false;
            }
        }

        private void SearchProducts()
        {
            if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Length < 3)
            {
                SearchResults.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var allProducts = await _productService.GetAllAsync();
                    var results = allProducts.Where(p =>
                        p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                        p.Barcode.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SearchResults = new ObservableCollection<ProductDTO>(results);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error searching products: {ex.Message}");
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ShowTemporaryErrorMessage($"Error searching products: {ex.Message}");
                    });
                }
            });
        }

        private async Task RegisterDamagedGoodsAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Registration operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedProduct == null)
                {
                    ShowTemporaryErrorMessage("Please select a product first.");
                    return;
                }

                if (Quantity <= 0)
                {
                    ShowTemporaryErrorMessage("Quantity must be greater than zero.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(Reason))
                {
                    ShowTemporaryErrorMessage("Please provide a reason for damage.");
                    return;
                }

                IsProcessing = true;
                StatusMessage = "Registering damaged goods...";

                // Check if product has enough stock
                if (SelectedProduct.CurrentStock < Quantity)
                {
                    ShowTemporaryErrorMessage($"Insufficient stock. Current stock: {SelectedProduct.CurrentStock}, Requested: {Quantity}");
                    return;
                }

                // Create the damaged goods record
                var damagedGoods = new DamagedGoodsDTO
                {
                    ProductId = SelectedProduct.ProductId,
                    ProductName = SelectedProduct.Name,
                    Barcode = SelectedProduct.Barcode,
                    Quantity = Quantity,
                    Reason = Reason,
                    DateRegistered = DateTime.Now,
                    LossAmount = SelectedProduct.PurchasePrice * Quantity,
                    CategoryName = SelectedProduct.CategoryName
                };

                // Register damaged goods and update stock
                bool success = await _damagedGoodsService.RegisterDamagedGoodsAsync(damagedGoods);

                if (success)
                {
                    // Update the stock
                    await _productService.UpdateStockAsync(SelectedProduct.ProductId, -Quantity);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsDamagedItemPopupOpen = false;
                        MessageBox.Show("Damaged goods registered successfully.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });

                    // Refresh the data
                    await LoadDataAsync();
                }
                else
                {
                    ShowTemporaryErrorMessage("Failed to register damaged goods.");
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error registering damaged goods: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        private async Task EditDamagedItemAsync(DamagedGoodsDTO damagedItem)
        {
            if (damagedItem == null) return;

            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Edit operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsProcessing = true;
                StatusMessage = "Loading damaged item details...";

                // Get the current product
                var product = await _productService.GetByIdAsync(damagedItem.ProductId);
                if (product == null)
                {
                    ShowTemporaryErrorMessage($"Product with ID {damagedItem.ProductId} not found.");
                    return;
                }

                // Set up the form for editing
                SelectedDamagedItem = damagedItem;
                SelectedProduct = product;
                Quantity = damagedItem.Quantity;
                Reason = damagedItem.Reason;
                IsEditMode = true;  // Set edit mode to true

                IsDamagedItemPopupOpen = true;
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading damaged item details: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        private async Task UpdateDamagedGoodsAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Update operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedDamagedItem == null)
                {
                    ShowTemporaryErrorMessage("No damaged item selected for update.");
                    return;
                }

                if (SelectedProduct == null)
                {
                    ShowTemporaryErrorMessage("Please select a product first.");
                    return;
                }

                if (Quantity <= 0)
                {
                    ShowTemporaryErrorMessage("Quantity must be greater than zero.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(Reason))
                {
                    ShowTemporaryErrorMessage("Please provide a reason for damage.");
                    return;
                }

                IsProcessing = true;
                StatusMessage = "Updating damaged goods record...";

                // Store the original quantity for stock adjustment calculation
                int originalQuantity = SelectedDamagedItem.Quantity;
                int quantityDifference = Quantity - originalQuantity;

                // Update the damaged goods record
                SelectedDamagedItem.ProductId = SelectedProduct.ProductId;
                SelectedDamagedItem.ProductName = SelectedProduct.Name;
                SelectedDamagedItem.Barcode = SelectedProduct.Barcode;
                SelectedDamagedItem.Quantity = Quantity;
                SelectedDamagedItem.Reason = Reason;
                SelectedDamagedItem.LossAmount = SelectedProduct.PurchasePrice * Quantity;
                SelectedDamagedItem.CategoryName = SelectedProduct.CategoryName;

                // Update the damaged goods record
                await _damagedGoodsService.UpdateAsync(SelectedDamagedItem);

                // If quantity changed, update the stock
                if (quantityDifference != 0)
                {
                    // Adjust stock - negative value means decreasing stock
                    await _productService.UpdateStockAsync(SelectedProduct.ProductId, -quantityDifference);
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsDamagedItemPopupOpen = false;
                    MessageBox.Show("Damaged goods record updated successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });

                // Refresh the data
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error updating damaged goods record: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }
        private async Task DeleteDamagedItemAsync(DamagedGoodsDTO damagedItem)
        {
            if (damagedItem == null) return;

            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Delete operation already in progress. Please wait.");
                return;
            }

            try
            {
                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show("Are you sure you want to delete this damaged goods record?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    IsProcessing = true;
                    StatusMessage = "Deleting damaged goods record...";

                    await _damagedGoodsService.DeleteAsync(damagedItem.DamagedGoodsId);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Damaged goods record deleted successfully.", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });

                    // Refresh the data
                    await LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error deleting damaged goods record: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            StatusMessage = message;

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
                    if (StatusMessage == message) // Only clear if still the same message
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
                _operationLock?.Dispose();
                UnsubscribeFromEvents();

                _isDisposed = true;
            }

            base.Dispose();
        }
    }
}