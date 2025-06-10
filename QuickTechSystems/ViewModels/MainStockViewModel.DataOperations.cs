// Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.DataOperations.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class MainStockViewModel
    {
        /// <summary>
        /// Generate a 12-digit barcode with category prefix
        /// </summary>
        private string GenerateBarcode12Digits(int categoryId)
        {
            // Category prefix (2-3 digits)
            string categoryPrefix = categoryId.ToString().PadLeft(2, '0');
            if (categoryPrefix.Length > 3)
                categoryPrefix = categoryPrefix.Substring(categoryPrefix.Length - 3, 3);

            // Timestamp (6 digits) - using seconds since epoch modulo to fit
            var timestamp = ((DateTimeOffset.Now.ToUnixTimeSeconds() % 1000000)).ToString("D6");

            // Random digits to fill remaining space
            var random = new Random();
            int remainingDigits = 12 - categoryPrefix.Length - timestamp.Length;
            int maxRandom = (int)Math.Pow(10, remainingDigits) - 1;
            int minRandom = (int)Math.Pow(10, remainingDigits - 1);

            var randomPart = random.Next(minRandom, maxRandom + 1).ToString();

            string barcode = $"{categoryPrefix}{timestamp}{randomPart}";

            // Ensure exactly 12 digits
            if (barcode.Length > 12)
                barcode = barcode.Substring(0, 12);
            else if (barcode.Length < 12)
                barcode = barcode.PadRight(12, '0');

            return barcode;
        }

        /// <summary>
        /// Load draft invoices from the database
        /// </summary>
        private async Task LoadDraftInvoicesAsync()
        {
            try
            {
                var invoices = await _supplierInvoiceService.GetByStatusAsync("Draft");

                // Filter by search text if provided
                if (!string.IsNullOrWhiteSpace(InvoiceSearchText))
                {
                    var searchText = InvoiceSearchText.ToLower();
                    invoices = invoices.Where(i =>
                        i.InvoiceNumber.ToLower().Contains(searchText) ||
                        i.SupplierName.ToLower().Contains(searchText)
                    ).ToList();
                }

                await SafeDispatcherOperation(() =>
                {
                    DraftInvoices = new ObservableCollection<SupplierInvoiceDTO>(invoices);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading draft invoices: {ex.Message}");
                // Don't throw here as this is a background operation
            }
        }

        /// <summary>
        /// Load store products from the database
        /// </summary>
        /// 



        /// <summary>
        /// Forces an immediate refresh of the current view without changing pagination
        /// </summary>
        /// <summary>
        /// Forces an immediate refresh of the current view without changing pagination
        /// </summary>
        private async Task RefreshCurrentViewAsync()
        {
            try
            {
                Debug.WriteLine("MainStockViewModel: Performing current view refresh");

                // Get fresh data for current page
                var allItems = await _mainStockService.GetAllAsync();
                var categories = await _categoryService.GetActiveAsync();
                var suppliers = await _supplierService.GetActiveAsync();

                // Ensure we have valid collections
                if (allItems == null) allItems = new List<MainStockDTO>();
                if (categories == null) categories = new List<CategoryDTO>();
                if (suppliers == null) suppliers = new List<SupplierDTO>();

                // Update category and supplier names
                foreach (var item in allItems.Where(i => i != null))
                {
                    if (string.IsNullOrEmpty(item.CategoryName) && item.CategoryId > 0)
                    {
                        var category = categories.FirstOrDefault(c => c != null && c.CategoryId == item.CategoryId);
                        if (category != null)
                            item.CategoryName = category.Name;
                    }

                    if (string.IsNullOrEmpty(item.SupplierName) && item.SupplierId > 0)
                    {
                        var supplier = suppliers.FirstOrDefault(s => s != null && s.SupplierId == item.SupplierId);
                        if (supplier != null)
                            item.SupplierName = supplier.Name;
                    }
                }

                // Apply current filters and pagination
                var filteredItems = FilterMainStockItems(allItems, SearchText);
                var pagedItems = filteredItems
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                await SafeDispatcherOperation(() =>
                {
                    try
                    {
                        // Update collections with null checks
                        Items = new ObservableCollection<MainStockDTO>(pagedItems ?? new List<MainStockDTO>());
                        FilteredItems = new ObservableCollection<MainStockDTO>(pagedItems ?? new List<MainStockDTO>());
                        Categories = new ObservableCollection<CategoryDTO>(categories ?? new List<CategoryDTO>());
                        Suppliers = new ObservableCollection<SupplierDTO>(suppliers ?? new List<SupplierDTO>());

                        // Update pagination info
                        TotalItems = filteredItems?.Count() ?? 0;
                        TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);
                        UpdateVisiblePageNumbers();

                        Debug.WriteLine($"Current view refresh complete. Updated {Items?.Count ?? 0} items.");
                    }
                    catch (Exception uiEx)
                    {
                        Debug.WriteLine($"Error updating UI during refresh: {uiEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during current view refresh: {ex.Message}");
            }
        }
        private async Task LoadStoreProductsAsync()
        {
            try
            {
                var products = await _productService.GetAllAsync();
                await SafeDispatcherOperation(() =>
                {
                    StoreProducts = new ObservableCollection<ProductDTO>(products);

                    // If we have a selected item, try to find a matching store product
                    if (SelectedItem != null)
                    {
                        var matchingProduct = StoreProducts.FirstOrDefault(p =>
                            p.Barcode == SelectedItem.Barcode ||
                            p.Name == SelectedItem.Name);

                        if (matchingProduct != null)
                        {
                            SelectedStoreProduct = matchingProduct;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading store products: {ex.Message}");
                // Don't throw here as this is a background operation
            }
        }

        /// <summary>
        /// Retry loading data with exponential backoff
        /// </summary>
        private async Task RetryLoadDataAsync(int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                Debug.WriteLine($"RetryLoadDataAsync: Attempt {i + 1} of {maxRetries}");

                // Calculate backoff time - exponential with some randomness
                int backoffTime = (int)(Math.Pow(2, i) * 500 + new Random().Next(100));

                if (await _operationLock.WaitAsync(backoffTime)) // Use exponential backoff
                {
                    try
                    {
                        // Reset to page 1 if we're refreshing after a bulk add
                        if (i == 0) // Only on first attempt
                        {
                            await SafeDispatcherOperation(() =>
                            {
                                _currentPage = 1;
                                OnPropertyChanged(nameof(CurrentPage));
                            });
                        }

                        await LoadDataAsync();
                        Debug.WriteLine("RetryLoadDataAsync: Data loaded successfully");
                        return; // Successfully loaded data
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"RetryLoadDataAsync: Error - {ex.Message}");
                    }
                    finally
                    {
                        _operationLock.Release();
                    }
                }

                Debug.WriteLine($"RetryLoadDataAsync: Waiting {backoffTime}ms before retry");
                await Task.Delay(backoffTime);
            }

            // If we get here, all retries failed
            await SafeDispatcherOperation(() =>
            {
                StatusMessage = "Data refresh pending - please try refreshing manually.";
            });

            Debug.WriteLine("RetryLoadDataAsync: All retry attempts failed");
        }

        /// <summary>
        /// Safe wrapper for loading data with proper locking
        /// </summary>
        private async Task SafeLoadDataAsync()
        {
            // Use configurable timeout instead of 0
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                Debug.WriteLine("SafeLoadDataAsync skipped - already in progress");
                return;
            }

            CancellationTokenSource localCts = null;

            try
            {
                // Create a new local CancellationTokenSource
                localCts = new CancellationTokenSource();

                // Thread-safe replacement of the _cts field
                var oldCts = Interlocked.Exchange(ref _cts, localCts);

                // Only cancel the old CTS if it's not null and not already disposed
                if (oldCts != null)
                {
                    try
                    {
                        oldCts.Cancel();
                        oldCts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Ignore - already disposed
                        Debug.WriteLine("Old CancellationTokenSource was already disposed");
                    }
                }

                // Get the token from our local CTS
                var token = localCts.Token;

                IsSaving = true;
                StatusMessage = "Loading data...";

                try
                {
                    // Add a timeout for the operation
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                    var combinedToken = linkedCts.Token;

                    // Get categories and suppliers (these are always fetched in full)
                    var categoriesTask = _categoryService.GetActiveAsync();
                    var suppliersTask = _supplierService.GetActiveAsync();
                    var storeProductsTask = _productService.GetAllAsync();

                    // Get total count of items
                    var mainStockItems = await _mainStockService.GetAllAsync();
                    if (combinedToken.IsCancellationRequested) return;

                    // Wait for categories and suppliers to complete
                    await Task.WhenAll(categoriesTask, suppliersTask, storeProductsTask);
                    if (combinedToken.IsCancellationRequested) return;

                    var categories = await categoriesTask;
                    var suppliers = await suppliersTask;
                    var storeProducts = await storeProductsTask;

                    // Update category and supplier names for display
                    foreach (var item in mainStockItems)
                    {
                        // Update category name if not already set
                        if (string.IsNullOrEmpty(item.CategoryName) && item.CategoryId > 0)
                        {
                            var category = categories.FirstOrDefault(c => c.CategoryId == item.CategoryId);
                            if (category != null)
                            {
                                item.CategoryName = category.Name;
                            }
                        }

                        // Update supplier name if not already set
                        if (string.IsNullOrEmpty(item.SupplierName) && item.SupplierId > 0)
                        {
                            var supplier = suppliers.FirstOrDefault(s => s.SupplierId == item.SupplierId);
                            if (supplier != null)
                            {
                                item.SupplierName = supplier.Name;
                            }
                        }
                    }

                    // Calculate total pages
                    var filteredItems = FilterMainStockItems(mainStockItems, SearchText);
                    int totalCount = filteredItems.Count();
                    int calculatedTotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

                    await SafeDispatcherOperation(() => {
                        TotalPages = calculatedTotalPages;
                        TotalItems = totalCount;
                    });

                    // Apply pagination to filtered items
                    var pagedItems = filteredItems
                        .Skip((CurrentPage - 1) * PageSize)
                        .Take(PageSize)
                        .ToList();

                    if (combinedToken.IsCancellationRequested) return;

                    await SafeDispatcherOperation(() =>
                    {
                        if (!combinedToken.IsCancellationRequested)
                        {
                            Items = new ObservableCollection<MainStockDTO>(pagedItems);
                            FilteredItems = new ObservableCollection<MainStockDTO>(pagedItems);
                            Categories = new ObservableCollection<CategoryDTO>(categories);
                            Suppliers = new ObservableCollection<SupplierDTO>(suppliers);
                            StoreProducts = new ObservableCollection<ProductDTO>(storeProducts);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("Operation was canceled");
                }
                catch (Exception ex)
                {
                    await HandleExceptionWithLogging("Error loading data", ex);
                }
            }
            finally
            {
                // Don't dispose the CTS here - it's a shared resource
                // Only clean up if our local CTS is still the current one
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Filter main stock items based on search text
        /// </summary>
        private IEnumerable<MainStockDTO> FilterMainStockItems(IEnumerable<MainStockDTO> items, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return items;

            searchText = searchText.ToLower();
            return items.Where(i =>
                i.Name.ToLower().Contains(searchText) ||
                (i.Barcode?.ToLower().Contains(searchText) ?? false) ||
                (i.CategoryName?.ToLower().Contains(searchText) ?? false) ||
                (i.SupplierName?.ToLower().Contains(searchText) ?? false) ||
                (i.Description?.ToLower().Contains(searchText) ?? false)
            );
        }

        /// <summary>
        /// Main data loading method
        /// </summary>
        protected override async Task LoadDataAsync()
        {
            await SafeLoadDataAsync();
        }

        /// <summary>
        /// Force a data refresh using a new context
        /// </summary>
        private async Task ForceDataRefresh()
        {
            // Wait a moment for all DB operations to complete
            await Task.Delay(500);

            // Force-release the lock if needed
            if (_operationLock.CurrentCount == 0)
            {
                _operationLock.Release();
                await Task.Delay(200);
            }

            // Make multiple attempts to refresh data
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    // Reset to page 1
                    await SafeDispatcherOperation(() =>
                    {
                        _currentPage = 1;
                        OnPropertyChanged(nameof(CurrentPage));
                    });

                    await LoadDataAsync();
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Refresh attempt {i + 1} failed: {ex.Message}");
                    await Task.Delay(500 * (i + 1)); // Increasing delay
                }
            }

            StatusMessage = "Data refreshed successfully";
            await Task.Delay(2000);
            StatusMessage = string.Empty;
        }

        /// <summary>
        /// Add a new item
        /// </summary>
        private void AddNew()
        {
            SelectedItem = new MainStockDTO
            {
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            BarcodeImage = null;
            ValidationErrors.Clear();
            IsNewItem = true;

            // Load draft invoices for selection
            _ = LoadDraftInvoicesAsync();

            ShowItemPopup();
        }

        /// <summary>
        /// Filter items based on search text
        /// </summary>
        private void FilterItems()
        {
            _ = SafeLoadDataAsync();
        }

        /// <summary>
        /// Save current item
        /// </summary>
        private async Task SaveAsync()
        {
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                ShowTemporaryErrorMessage("Save operation already in progress. Please wait.");
                return;
            }

            try
            {
                Debug.WriteLine("Starting save operation");
                if (SelectedItem == null) return;

                IsSaving = true;
                StatusMessage = "Validating item...";

                var itemToUpdate = new MainStockDTO
                {
                    MainStockId = SelectedItem.MainStockId,
                    Name = SelectedItem.Name,
                    Barcode = SelectedItem.Barcode,
                    BoxBarcode = SelectedItem.BoxBarcode,
                    CategoryId = SelectedItem.CategoryId,
                    CategoryName = SelectedItem.CategoryName,
                    SupplierId = SelectedItem.SupplierId,
                    SupplierName = SelectedItem.SupplierName,
                    Description = SelectedItem.Description,
                    PurchasePrice = SelectedItem.PurchasePrice,
                    WholesalePrice = SelectedItem.WholesalePrice,
                    SalePrice = SelectedItem.SalePrice,
                    CurrentStock = SelectedItem.CurrentStock,
                    MinimumStock = SelectedItem.MinimumStock,
                    BarcodeImage = SelectedItem.BarcodeImage,
                    Speed = SelectedItem.Speed,
                    IsActive = SelectedItem.IsActive,
                    ImagePath = SelectedItem.ImagePath,
                    BoxPurchasePrice = SelectedItem.BoxPurchasePrice,
                    BoxWholesalePrice = SelectedItem.BoxWholesalePrice,
                    BoxSalePrice = SelectedItem.BoxSalePrice,
                    ItemsPerBox = SelectedItem.ItemsPerBox,
                    MinimumBoxStock = SelectedItem.MinimumBoxStock,
                    CreatedAt = SelectedItem.CreatedAt,
                    UpdatedAt = DateTime.Now,
                    AutoSyncToProducts = AutoSyncToProducts
                };

                // Generate 12-digit barcode if needed
                if (string.IsNullOrWhiteSpace(itemToUpdate.Barcode))
                {
                    Debug.WriteLine("No barcode provided, generating automatic 12-digit barcode");
                    itemToUpdate.Barcode = GenerateBarcode12Digits(itemToUpdate.CategoryId);
                }

                if (itemToUpdate.BarcodeImage == null && !string.IsNullOrWhiteSpace(itemToUpdate.Barcode))
                {
                    Debug.WriteLine("Generating barcode image for item");
                    try
                    {
                        itemToUpdate.BarcodeImage = _barcodeService.GenerateBarcode(itemToUpdate.Barcode);
                        BarcodeImage = LoadBarcodeImage(itemToUpdate.BarcodeImage);
                        Debug.WriteLine("Barcode image generated successfully");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error generating barcode image: {ex.Message}");
                    }
                }

                if (!ValidateItem(itemToUpdate))
                {
                    return;
                }

                try
                {
                    var existingItem = await _mainStockService.FindProductByBarcodeAsync(
                        itemToUpdate.Barcode,
                        itemToUpdate.MainStockId);

                    if (existingItem != null)
                    {
                        await SafeDispatcherOperation(() =>
                        {
                            MessageBox.Show(
                                $"Cannot save item: An item with barcode '{existingItem.Barcode}' already exists: '{existingItem.Name}'.",
                                "Duplicate Barcode",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error checking for duplicate barcode: {ex.Message}");
                }

                StatusMessage = "Saving item...";

                try
                {
                    MainStockDTO savedItem;

                    if (itemToUpdate.MainStockId == 0)
                    {
                        savedItem = await _mainStockService.CreateAsync(itemToUpdate);
                    }
                    else
                    {
                        savedItem = await _mainStockService.UpdateAsync(itemToUpdate);
                    }

                    await SafeDispatcherOperation(() => {
                        SelectedItem = savedItem;
                    });

                    if (SelectedInvoice != null && savedItem != null)
                    {
                        try
                        {
                            StatusMessage = "Adding product to invoice...";
                            var matchingProduct = await _productService.FindProductByBarcodeAsync(savedItem.Barcode);

                            if (matchingProduct == null && AutoSyncToProducts)
                            {
                                await Task.Delay(500);
                                matchingProduct = await _productService.FindProductByBarcodeAsync(savedItem.Barcode);
                            }

                            if (matchingProduct != null)
                            {
                                decimal quantity = savedItem.CurrentStock;
                                var invoiceDetail = new SupplierInvoiceDetailDTO
                                {
                                    SupplierInvoiceId = SelectedInvoice.SupplierInvoiceId,
                                    ProductId = matchingProduct.ProductId,
                                    ProductName = savedItem.Name,
                                    ProductBarcode = savedItem.Barcode,
                                    Quantity = quantity,
                                    PurchasePrice = savedItem.PurchasePrice,
                                    TotalPrice = savedItem.PurchasePrice * quantity
                                };

                                await _supplierInvoiceService.AddProductToInvoiceAsync(invoiceDetail);

                                await SafeDispatcherOperation(() =>
                                {
                                    MessageBox.Show($"Product saved and added to invoice {SelectedInvoice.InvoiceNumber} with quantity {quantity}.", "Success",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error adding product to invoice: {ex.Message}");
                            await SafeDispatcherOperation(() =>
                            {
                                MessageBox.Show($"Product saved successfully but could not be added to invoice: {ex.Message}",
                                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                        }
                    }
                    else
                    {
                        await SafeDispatcherOperation(() =>
                        {
                            string message = AutoSyncToProducts ?
                                "Product saved and synced to store successfully." :
                                "Product saved successfully.";
                            MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }

                    CloseItemPopup();
                    await SafeLoadDataAsync();
                    Debug.WriteLine("Save completed, item refreshed");
                }
                catch (Exception ex)
                {
                    var errorMessage = GetDetailedErrorMessage(ex);
                    Debug.WriteLine($"Save error: {errorMessage}");
                    ShowTemporaryErrorMessage($"Error saving item: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error in save operation: {ex.Message}");
                ShowTemporaryErrorMessage($"Error saving item: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Delete the selected item
        /// </summary>
        private async Task DeleteAsync()
        {
            // Use reasonable timeout instead of 0
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                ShowTemporaryErrorMessage("Delete operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedItem == null) return;

                var result = await SafeDispatcherOperation(() =>
                {
                    return MessageBox.Show($"Are you sure you want to delete {SelectedItem.Name}?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                });

                if (result == MessageBoxResult.Yes)
                {
                    IsSaving = true;
                    StatusMessage = "Deleting item...";

                    var itemId = SelectedItem.MainStockId;
                    var itemName = SelectedItem.Name;

                    try
                    {
                        await _mainStockService.DeleteAsync(itemId);

                        // Remove from the local collection
                        await SafeDispatcherOperation(() =>
                        {
                            var itemToRemove = Items.FirstOrDefault(p => p.MainStockId == itemId);
                            if (itemToRemove != null)
                            {
                                Items.Remove(itemToRemove);
                            }
                        });

                        // Close popup if it's open
                        if (IsItemPopupOpen)
                        {
                            CloseItemPopup();
                        }

                        await SafeDispatcherOperation(() =>
                        {
                            MessageBox.Show($"Item '{itemName}' has been deleted successfully.",
                                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        });

                        // Clear the selected item
                        SelectedItem = null;

                        // Refresh the data
                        await SafeLoadDataAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error deleting item {itemId}: {ex.Message}");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error deleting item: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Update the stock quantity for the selected item
        /// </summary>
        private async Task UpdateStockAsync()
        {
            // Use reasonable timeout instead of 0
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                ShowTemporaryErrorMessage("Stock update operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedItem == null || StockIncrement <= 0)
                {
                    ShowTemporaryErrorMessage("Please select an item and enter a valid stock increment.");
                    return;
                }

                IsSaving = true;
                StatusMessage = "Updating stock...";

                var newStock = SelectedItem.CurrentStock + StockIncrement;

                // Store the old value for validation
                var oldStock = SelectedItem.CurrentStock;

                // Update the local model first
                SelectedItem.CurrentStock = newStock;

                try
                {
                    // Update in the database
                    bool result = await _mainStockService.UpdateStockAsync(SelectedItem.MainStockId, StockIncrement);

                    if (!result)
                    {
                        // Revert local change if the update failed
                        SelectedItem.CurrentStock = oldStock;
                        ShowTemporaryErrorMessage("Failed to update stock. Please try again.");
                        return;
                    }

                    await SafeDispatcherOperation(() =>
                    {
                        MessageBox.Show($"Stock updated successfully. New stock: {newStock}",
                            "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    });

                    StockIncrement = 0;
                }
                catch (Exception ex)
                {
                    // Revert local change on error
                    SelectedItem.CurrentStock = oldStock;
                    throw new InvalidOperationException("Failed to update stock quantity", ex);
                }
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

        /// <summary>
        /// Get detailed error message from an exception
        /// </summary>
        private string GetDetailedErrorMessage(Exception ex)
        {
            var sb = new StringBuilder();
            sb.Append(ex.Message);

            // Collect inner exception details
            var currentEx = ex;
            while (currentEx.InnerException != null)
            {
                currentEx = currentEx.InnerException;
                sb.Append($"\n→ {currentEx.Message}");
            }

            // Add Entity Framework validation errors if available
            if (ex is DbUpdateException dbEx && dbEx.Entries != null && dbEx.Entries.Any())
            {
                sb.Append("\nValidation errors:");
                foreach (var entry in dbEx.Entries)
                {
                    sb.Append($"\n- {entry.Entity.GetType().Name}");

                    if (entry.State == EntityState.Added)
                        sb.Append(" (Add)");
                    else if (entry.State == EntityState.Modified)
                        sb.Append(" (Update)");
                    else if (entry.State == EntityState.Deleted)
                        sb.Append(" (Delete)");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Validate an item before saving
        /// </summary>
        private bool ValidateItem(MainStockDTO item)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(item.Name))
                errors.Add("Item name is required");

            if (item.CategoryId <= 0)
                errors.Add("Please select a category");

            if (item.SalePrice <= 0)
                errors.Add("Sale price must be greater than zero");

            // Modified validation: allows purchase price of 0 but prevents negative values
            if (item.PurchasePrice < 0)
                errors.Add("Purchase price cannot be negative");

            if (item.MinimumStock < 0)
                errors.Add("Minimum stock cannot be negative");

            if (!string.IsNullOrWhiteSpace(item.Speed))
            {
                if (!decimal.TryParse(item.Speed, out _))
                {
                    errors.Add("Speed must be a valid number");
                }
            }

            if (errors.Any())
            {
                ShowValidationErrors(errors);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Display validation errors to the user
        /// </summary>
        private void ShowValidationErrors(List<string> errors)
        {
            SafeDispatcherOperation(() =>
            {
                MessageBox.Show(string.Join("\n", errors), "Validation Errors",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        /// <summary>
        /// Show a bulk add dialog and refresh data after completion
        /// </summary>
        private async Task<bool> ShowBulkAddDialogAndRefresh()
        {
            try
            {
                await ShowBulkAddDialog();

                // Force wait for any in-progress operations to complete
                if (_operationLock.CurrentCount == 0)
                {
                    await Task.Delay(500); // Brief delay
                }

                await RetryLoadDataAsync(); // Use a new method that retries loading
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in bulk add: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Show bulk add dialog and process results
        /// </summary>
        private async Task ShowBulkAddDialog()
        {
            // Use reasonable timeout instead of 0
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                ShowTemporaryErrorMessage("Bulk add operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsSaving = true;
                StatusMessage = "Preparing bulk add dialog...";

                // Get the BulkOperationQueueService from the service provider
                var bulkOperationQueueService = ((App)System.Windows.Application.Current).ServiceProvider.GetService(typeof(IBulkOperationQueueService)) as IBulkOperationQueueService;

                if (bulkOperationQueueService == null)
                {
                    throw new InvalidOperationException("BulkOperationQueueService not found in service provider.");
                }

                var viewModel = new BulkMainStockViewModel(
                    _mainStockService,
                    _categoryService,
                    _supplierService,
                    _barcodeService,
                    _supplierInvoiceService,
                    _imagePathService,
                    _productService,
                    bulkOperationQueueService,
                    _eventAggregator);

                var ownerWindow = GetOwnerWindow();

                await SafeDispatcherOperation(async () =>
                {
                    var dialog = new BulkMainStockDialog
                    {
                        DataContext = viewModel,
                        Owner = ownerWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };

                    try
                    {
                        var result = dialog.ShowDialog();

                        if (result == true)
                        {
                            // Force a hard refresh of data after bulk add
                            await ForceDataRefresh();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error showing bulk add dialog: {ex}");
                        ShowTemporaryErrorMessage($"Error showing bulk dialog: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error preparing bulk add dialog: {ex}");
                ShowTemporaryErrorMessage($"Error in bulk add: {ex.Message}");
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }

        /// <summary>
        /// Direct database refresh implementation that ensures all data is freshly loaded
        /// </summary>
        private async Task RefreshFromDatabaseDirectly()
        {
            // Use the operation lock to prevent concurrent executions
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                Debug.WriteLine("RefreshFromDatabaseDirectly - another operation in progress");
                return;
            }

            try
            {
                Debug.WriteLine("MainStockViewModel: Performing direct database refresh");
                IsSaving = true;
                StatusMessage = "Refreshing data...";

                // Reset cancellation token to prevent interruptions
                var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
                oldCts?.Cancel();
                oldCts?.Dispose();

                // Get fresh data directly from services
                var mainStockItems = await _mainStockService.GetAllAsync();
                var categories = await _categoryService.GetActiveAsync();
                var suppliers = await _supplierService.GetActiveAsync();
                var storeProducts = await _productService.GetAllAsync();

                // Update category and supplier names for display
                foreach (var item in mainStockItems)
                {
                    // Update category name if not already set
                    if (string.IsNullOrEmpty(item.CategoryName) && item.CategoryId > 0)
                    {
                        var category = categories.FirstOrDefault(c => c.CategoryId == item.CategoryId);
                        if (category != null)
                        {
                            item.CategoryName = category.Name;
                        }
                    }

                    // Update supplier name if not already set
                    if (string.IsNullOrEmpty(item.SupplierName) && item.SupplierId > 0)
                    {
                        var supplier = suppliers.FirstOrDefault(s => s.SupplierId == item.SupplierId);
                        if (supplier != null)
                        {
                            item.SupplierName = supplier.Name;
                        }
                    }
                }

                // Apply filtering
                var filteredItems = FilterMainStockItems(mainStockItems, SearchText);
                int totalCount = filteredItems.Count();
                int calculatedTotalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

                // Apply pagination
                var pagedItems = filteredItems
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                // Update collections on UI thread
                await SafeDispatcherOperation(() =>
                {
                    // Update all collections
                    Items = new ObservableCollection<MainStockDTO>(pagedItems);
                    FilteredItems = new ObservableCollection<MainStockDTO>(pagedItems);
                    Categories = new ObservableCollection<CategoryDTO>(categories);
                    Suppliers = new ObservableCollection<SupplierDTO>(suppliers);
                    StoreProducts = new ObservableCollection<ProductDTO>(storeProducts);

                    // Update pagination info
                    TotalItems = totalCount;
                    TotalPages = calculatedTotalPages;
                    UpdateVisiblePageNumbers();

                    Debug.WriteLine($"Direct refresh complete. Loaded {Items.Count} items of {totalCount} total.");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during direct refresh: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                StatusMessage = "Error refreshing data";
                await Task.Delay(2000);
                StatusMessage = string.Empty;
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }
    }
}