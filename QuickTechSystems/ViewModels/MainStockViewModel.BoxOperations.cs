// Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.BoxOperations.cs
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.Events;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class MainStockViewModel
    {
        /// <summary>
        /// Converts a box to individual items for the selected product.
        /// Decreases box count by 1 and increases individual items by ItemsPerBox.
        /// </summary>
        /// <summary>
        /// Converts a box to individual items for the selected product.
        /// </summary>
        /// <summary>
        /// Converts a box to individual items for the selected product.
        /// </summary>
        private async Task ConvertBoxToIndividualAsync()
        {
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                ShowTemporaryErrorMessage("Operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedItem == null)
                {
                    ShowTemporaryErrorMessage("Please select an item first.");
                    return;
                }

                if (SelectedItem.NumberOfBoxes <= 0)
                {
                    ShowTemporaryErrorMessage("No boxes available to convert.");
                    return;
                }

                if (SelectedItem.ItemsPerBox <= 0)
                {
                    ShowTemporaryErrorMessage("This item has no items per box defined.");
                    return;
                }

                IsSaving = true;
                StatusMessage = "Converting box to individual items...";

                // Store original values
                int mainStockId = SelectedItem.MainStockId;
                int originalBoxCount = SelectedItem.NumberOfBoxes;
                decimal originalCurrentStock = SelectedItem.CurrentStock;
                int itemsPerBox = SelectedItem.ItemsPerBox;

                // Get current item data
                var currentItem = await _mainStockService.GetByIdAsync(mainStockId);
                if (currentItem == null || currentItem.NumberOfBoxes <= 0)
                {
                    throw new InvalidOperationException("Item not found or no boxes available");
                }

                // Create updated item
                var updatedItem = new Application.DTOs.MainStockDTO
                {
                    MainStockId = currentItem.MainStockId,
                    Name = currentItem.Name,
                    Barcode = currentItem.Barcode,
                    BoxBarcode = currentItem.BoxBarcode,
                    CategoryId = currentItem.CategoryId,
                    CategoryName = currentItem.CategoryName,
                    SupplierId = currentItem.SupplierId,
                    SupplierName = currentItem.SupplierName,
                    Description = currentItem.Description,
                    PurchasePrice = currentItem.PurchasePrice,
                    WholesalePrice = currentItem.WholesalePrice,
                    SalePrice = currentItem.SalePrice,
                    BoxPurchasePrice = currentItem.BoxPurchasePrice,
                    BoxWholesalePrice = currentItem.BoxWholesalePrice,
                    BoxSalePrice = currentItem.BoxSalePrice,
                    NumberOfBoxes = currentItem.NumberOfBoxes - 1,
                    CurrentStock = currentItem.CurrentStock + currentItem.ItemsPerBox,
                    ItemsPerBox = currentItem.ItemsPerBox,
                    MinimumStock = currentItem.MinimumStock,
                    MinimumBoxStock = currentItem.MinimumBoxStock,
                    BarcodeImage = currentItem.BarcodeImage,
                    Speed = currentItem.Speed,
                    IsActive = currentItem.IsActive,
                    ImagePath = currentItem.ImagePath,
                    CreatedAt = currentItem.CreatedAt,
                    UpdatedAt = DateTime.Now
                };

                // Update in database
                var result = await _mainStockService.UpdateAsync(updatedItem);
                if (result == null)
                {
                    throw new InvalidOperationException("Failed to update stock.");
                }

                // IMPROVED: Update UI immediately with proper null checks
                await SafeDispatcherOperation(() =>
                {
                    try
                    {
                        if (SelectedItem != null)
                        {
                            SelectedItem.NumberOfBoxes = updatedItem.NumberOfBoxes;
                            SelectedItem.CurrentStock = updatedItem.CurrentStock;
                            OnPropertyChanged(nameof(SelectedItemBoxCount));
                        }

                        // FIXED: Add proper null checks for collections
                        if (Items != null)
                        {
                            var itemInCollection = Items.FirstOrDefault(i => i != null && i.MainStockId == mainStockId);
                            if (itemInCollection != null)
                            {
                                itemInCollection.NumberOfBoxes = updatedItem.NumberOfBoxes;
                                itemInCollection.CurrentStock = updatedItem.CurrentStock;
                            }
                        }

                        if (FilteredItems != null)
                        {
                            var itemInFilteredCollection = FilteredItems.FirstOrDefault(i => i != null && i.MainStockId == mainStockId);
                            if (itemInFilteredCollection != null)
                            {
                                itemInFilteredCollection.NumberOfBoxes = updatedItem.NumberOfBoxes;
                                itemInFilteredCollection.CurrentStock = updatedItem.CurrentStock;
                            }
                        }
                    }
                    catch (Exception uiEx)
                    {
                        Debug.WriteLine($"Error updating UI after box conversion: {uiEx.Message}");
                        // Don't throw here, the conversion was successful
                    }
                });

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        MessageBox.Show($"Successfully converted 1 box to {itemsPerBox} individual items.\nNew stock: {updatedItem.CurrentStock} items, {updatedItem.NumberOfBoxes} boxes.",
                            "Conversion Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception msgEx)
                    {
                        Debug.WriteLine($"Error showing success message: {msgEx.Message}");
                    }
                });

                // Background refresh for consistency
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(300);
                        await RefreshCurrentViewAsync();
                        _eventAggregator.Publish(new GlobalDataRefreshEvent());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Background refresh error after box conversion: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error converting box to individual items: {ex.Message}");
                ShowTemporaryErrorMessage($"Error converting box: {ex.Message}");
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