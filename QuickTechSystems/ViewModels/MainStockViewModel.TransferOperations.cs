// Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.TransferOperations.cs
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QuickTechSystems.Application.Events;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class MainStockViewModel
    {
        private void ShowTransferDialog()
        {
            if (SelectedItem == null)
            {
                ShowTemporaryErrorMessage("Please select an item to transfer.");
                return;
            }

            TransferQuantity = 1;
            TransferByBoxes = false;
            _ = LoadStoreProductsAsync();
            OnPropertyChanged(nameof(SelectedItemBoxCount));
            IsTransferPopupOpen = true;
        }
        // Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.TransferOperations.cs
        private async Task TransferToStoreAsync()
        {
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                ShowTemporaryErrorMessage("Transfer operation already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedItem == null)
                {
                    ShowTemporaryErrorMessage("Please select an item to transfer.");
                    return;
                }

                if (SelectedStoreProduct == null)
                {
                    ShowTemporaryErrorMessage("Please select a store product to transfer to.");
                    return;
                }

                if (TransferQuantity <= 0)
                {
                    ShowTemporaryErrorMessage("Transfer quantity must be greater than zero.");
                    return;
                }

                // FIXED: Check against CurrentStock directly for individual items
                if (TransferByBoxes)
                {
                    if (TransferQuantity > SelectedItem.NumberOfBoxes)
                    {
                        ShowTemporaryErrorMessage($"Transfer quantity ({TransferQuantity} boxes) exceeds available box stock ({SelectedItem.NumberOfBoxes} boxes).");
                        return;
                    }
                }
                else
                {
                    // Use CurrentStock directly for individual items check
                    if (TransferQuantity > SelectedItem.CurrentStock)
                    {
                        ShowTemporaryErrorMessage($"Transfer quantity ({TransferQuantity}) exceeds available stock ({SelectedItem.CurrentStock}).");
                        return;
                    }
                }

                // Store item details before the transfer
                int mainStockId = SelectedItem.MainStockId;
                int productId = SelectedStoreProduct.ProductId;
                string itemName = SelectedItem.Name;
                decimal originalCurrentStock = SelectedItem.CurrentStock;
                int originalNumberOfBoxes = SelectedItem.NumberOfBoxes;

                IsSaving = true;
                StatusMessage = "Processing transfer...";

                string transferredBy = "System User";
                bool transferSuccessful = false;

                try
                {
                    transferSuccessful = await _mainStockService.TransferToStoreAsync(
                        mainStockId,
                        productId,
                        TransferQuantity,
                        transferredBy,
                        $"Manual transfer from MainStock to Store",
                        TransferByBoxes
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Transfer service error: {ex.Message}");
                    throw new InvalidOperationException($"Transfer failed: {ex.Message}", ex);
                }

                if (!transferSuccessful)
                {
                    throw new InvalidOperationException("Transfer failed. Please try again.");
                }

                // Close the transfer popup immediately
                IsTransferPopupOpen = false;

                // IMPROVED: Update the UI immediately with calculated values and proper null checks
                await SafeDispatcherOperation(() =>
                {
                    try
                    {
                        if (SelectedItem != null)
                        {
                            if (TransferByBoxes)
                            {
                                SelectedItem.NumberOfBoxes = Math.Max(0, originalNumberOfBoxes - (int)TransferQuantity);
                            }
                            else
                            {
                                SelectedItem.CurrentStock = Math.Max(0, originalCurrentStock - TransferQuantity);
                            }

                            // Update the box count display
                            OnPropertyChanged(nameof(SelectedItemBoxCount));
                        }

                        // FIXED: Add proper null checks for collections
                        if (Items != null)
                        {
                            var itemInCollection = Items.FirstOrDefault(i => i != null && i.MainStockId == mainStockId);
                            if (itemInCollection != null)
                            {
                                if (TransferByBoxes)
                                {
                                    itemInCollection.NumberOfBoxes = SelectedItem?.NumberOfBoxes ?? 0;
                                }
                                else
                                {
                                    itemInCollection.CurrentStock = SelectedItem?.CurrentStock ?? 0;
                                }
                            }
                        }

                        // FIXED: Add proper null checks for filtered items
                        if (FilteredItems != null)
                        {
                            var itemInFilteredCollection = FilteredItems.FirstOrDefault(i => i != null && i.MainStockId == mainStockId);
                            if (itemInFilteredCollection != null)
                            {
                                if (TransferByBoxes)
                                {
                                    itemInFilteredCollection.NumberOfBoxes = SelectedItem?.NumberOfBoxes ?? 0;
                                }
                                else
                                {
                                    itemInFilteredCollection.CurrentStock = SelectedItem?.CurrentStock ?? 0;
                                }
                            }
                        }
                    }
                    catch (Exception uiEx)
                    {
                        Debug.WriteLine($"Error updating UI after transfer: {uiEx.Message}");
                        // Don't throw here, the transfer was successful
                    }
                });

                // Show success message immediately
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        string unitType = TransferByBoxes ? "boxes" : "units";
                        MessageBox.Show(
                            $"Successfully transferred {TransferQuantity} {unitType} of {itemName} from MainStock to Store inventory.",
                            "Transfer Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                    catch (Exception msgEx)
                    {
                        Debug.WriteLine($"Error showing success message: {msgEx.Message}");
                    }
                });

                // IMPROVED: Perform background refresh to ensure database consistency
                // This happens after the UI is already updated, so user sees immediate feedback
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(500); // Small delay to ensure database transaction is complete

                        // Get fresh data from database
                        var updatedItem = await GetUpdatedMainStockItemAsync(mainStockId);
                        if (updatedItem != null)
                        {
                            await SafeDispatcherOperation(() =>
                            {
                                try
                                {
                                    // Update with actual database values (in case of any discrepancies)
                                    if (SelectedItem != null && SelectedItem.MainStockId == mainStockId)
                                    {
                                        SelectedItem.CurrentStock = updatedItem.CurrentStock;
                                        SelectedItem.NumberOfBoxes = updatedItem.NumberOfBoxes;
                                        OnPropertyChanged(nameof(SelectedItemBoxCount));
                                    }

                                    // Update collections with null checks
                                    if (Items != null)
                                    {
                                        var itemInCollection = Items.FirstOrDefault(i => i != null && i.MainStockId == mainStockId);
                                        if (itemInCollection != null)
                                        {
                                            itemInCollection.CurrentStock = updatedItem.CurrentStock;
                                            itemInCollection.NumberOfBoxes = updatedItem.NumberOfBoxes;
                                        }
                                    }

                                    if (FilteredItems != null)
                                    {
                                        var itemInFilteredCollection = FilteredItems.FirstOrDefault(i => i != null && i.MainStockId == mainStockId);
                                        if (itemInFilteredCollection != null)
                                        {
                                            itemInFilteredCollection.CurrentStock = updatedItem.CurrentStock;
                                            itemInFilteredCollection.NumberOfBoxes = updatedItem.NumberOfBoxes;
                                        }
                                    }
                                }
                                catch (Exception backgroundUiEx)
                                {
                                    Debug.WriteLine($"Error in background UI update: {backgroundUiEx.Message}");
                                }
                            });
                        }

                        // Publish events for other parts of the system
                        _eventAggregator.Publish(new GlobalDataRefreshEvent());

                        Debug.WriteLine("Background refresh completed after transfer");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Background refresh error after transfer: {ex.Message}");
                        // Don't show error to user since the transfer was successful
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Transfer error: {ex.Message}");
                string errorDetail = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                string properErrorMessage = errorDetail.Replace("Payment failed:", "Transfer failed:");
                ShowTemporaryErrorMessage(properErrorMessage);
            }
            finally
            {
                IsSaving = false;
                StatusMessage = string.Empty;
                _operationLock.Release();
            }
        }
        private async Task<MainStockDTO> GetUpdatedMainStockItemAsync(int mainStockId)
        {
            try
            {
                return await _mainStockService.GetByIdAsync(mainStockId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching updated MainStock item: {ex.Message}");
                return null;
            }
        }
    }
}