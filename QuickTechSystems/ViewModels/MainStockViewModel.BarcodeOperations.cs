// Path: QuickTechSystems.WPF.ViewModels/MainStockViewModel.BarcodeOperations.cs
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QuickTechSystems.Application.Services;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class MainStockViewModel
    {
        /// <summary>
        /// Generate a barcode for the selected item
        /// </summary>
        private void GenerateBarcode()
        {
            if (SelectedItem == null || string.IsNullOrWhiteSpace(SelectedItem.Barcode))
            {
                ShowTemporaryErrorMessage("Please enter a barcode value first.");
                return;
            }

            try
            {
                var barcodeData = _barcodeService.GenerateBarcode(SelectedItem.Barcode);
                if (barcodeData != null)
                {
                    SelectedItem.BarcodeImage = barcodeData;
                    BarcodeImage = LoadBarcodeImage(barcodeData);

                    if (BarcodeImage == null)
                    {
                        ShowTemporaryErrorMessage("Failed to load barcode image.");
                    }
                }
                else
                {
                    ShowTemporaryErrorMessage("Failed to generate barcode.");
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error generating barcode: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate an automatic 12-digit barcode for the selected item
        /// </summary>
        private void GenerateAutomaticBarcode()
        {
            if (SelectedItem == null)
            {
                ShowTemporaryErrorMessage("Please select an item first.");
                return;
            }

            try
            {
                // Generate 12-digit barcode
                SelectedItem.Barcode = GenerateBarcode12Digits(SelectedItem.CategoryId);
                var barcodeData = _barcodeService.GenerateBarcode(SelectedItem.Barcode);

                if (barcodeData != null)
                {
                    SelectedItem.BarcodeImage = barcodeData;
                    BarcodeImage = LoadBarcodeImage(barcodeData);

                    if (BarcodeImage == null)
                    {
                        ShowTemporaryErrorMessage("Failed to load barcode image.");
                    }
                }
                else
                {
                    ShowTemporaryErrorMessage("Failed to generate barcode.");
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error generating automatic barcode: {ex.Message}");
            }
        }

        /// <summary>
        /// Print barcode labels for the selected item
        /// </summary>
        private async Task PrintBarcodeAsync()
        {
            // Use reasonable timeout instead of 0
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                ShowTemporaryErrorMessage("A print operation is already in progress. Please wait.");
                return;
            }

            try
            {
                if (SelectedItem == null)
                {
                    ShowTemporaryErrorMessage("Please select an item first.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(SelectedItem.Barcode))
                {
                    ShowTemporaryErrorMessage("This item does not have a barcode assigned.");
                    return;
                }

                StatusMessage = "Preparing barcode...";
                IsSaving = true;

                // Generate barcode image if needed
                if (SelectedItem.BarcodeImage == null)
                {
                    try
                    {
                        var barcodeData = _barcodeService.GenerateBarcode(SelectedItem.Barcode, 600, 200);
                        if (barcodeData == null)
                        {
                            ShowTemporaryErrorMessage("Failed to generate barcode.");
                            return;
                        }

                        SelectedItem.BarcodeImage = barcodeData;
                        BarcodeImage = LoadBarcodeImage(barcodeData);
                        Debug.WriteLine("Successfully generated barcode image for printing");

                        // Save updated item with barcode image
                        var itemCopy = new MainStockDTO
                        {
                            MainStockId = SelectedItem.MainStockId,
                            Name = SelectedItem.Name,
                            Barcode = SelectedItem.Barcode,
                            CategoryId = SelectedItem.CategoryId,
                            CategoryName = SelectedItem.CategoryName,
                            SupplierId = SelectedItem.SupplierId,
                            SupplierName = SelectedItem.SupplierName,
                            Description = SelectedItem.Description,
                            PurchasePrice = SelectedItem.PurchasePrice,
                            SalePrice = SelectedItem.SalePrice,
                            CurrentStock = SelectedItem.CurrentStock,
                            MinimumStock = SelectedItem.MinimumStock,
                            BarcodeImage = barcodeData,
                            Speed = SelectedItem.Speed,
                            IsActive = SelectedItem.IsActive,
                            ImagePath = SelectedItem.ImagePath,
                            CreatedAt = SelectedItem.CreatedAt,
                            UpdatedAt = DateTime.Now
                        };

                        await _mainStockService.UpdateAsync(itemCopy);
                    }
                    catch (Exception ex)
                    {
                        ShowTemporaryErrorMessage($"Error generating barcode: {ex.Message}");
                        return;
                    }
                }

                bool printerCancelled = false;
                await SafeDispatcherOperation(async () =>
                {
                    try
                    {
                        StatusMessage = "Preparing barcode labels...";

                        var printDialog = new PrintDialog();
                        if (printDialog.ShowDialog() != true)
                        {
                            printerCancelled = true;
                            return;
                        }

                        StatusMessage = $"Creating {LabelsPerProduct} labels...";

                        // Calculate how many labels can fit on a page
                        double pageWidth = printDialog.PrintableAreaWidth;
                        double pageHeight = printDialog.PrintableAreaHeight;
                        double labelWidth = 280; // Smaller label width
                        double labelHeight = 140; // Smaller label height

                        // Calculate columns and rows
                        int columns = Math.Max(1, (int)(pageWidth / labelWidth));
                        int labelsPerPage = columns * (int)(pageHeight / labelHeight);

                        // Create a wrapping panel for better label layout
                        var wrapPanel = new WrapPanel
                        {
                            Width = pageWidth,
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };

                        // Create labels
                        for (int i = 0; i < LabelsPerProduct; i++)
                        {
                            var labelVisual = CreatePrintVisual(SelectedItem);
                            wrapPanel.Children.Add(labelVisual);
                        }

                        // Print the grid containing the labels
                        StatusMessage = "Sending to printer...";
                        printDialog.PrintVisual(wrapPanel, $"Barcode - {SelectedItem.Name}");

                        StatusMessage = "Barcode labels printed successfully.";
                        await Task.Delay(2000);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error printing barcodes: {ex.Message}");
                        ShowTemporaryErrorMessage($"Error printing barcodes: {ex.Message}");
                    }
                });

                if (printerCancelled)
                {
                    StatusMessage = "Printing cancelled by user.";
                    await Task.Delay(1000);
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

        /// <summary>
        /// Generate missing barcode images for all items in the list
        /// </summary>
        private async Task GenerateMissingBarcodeImages()
        {
            // Use reasonable timeout instead of 0
            if (!await _operationLock.WaitAsync(DEFAULT_LOCK_TIMEOUT_MS))
            {
                ShowTemporaryErrorMessage("Operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsSaving = true;
                StatusMessage = "Generating missing barcode images...";
                int generatedCount = 0;
                int failedCount = 0;

                // Get a fresh copy of all items to avoid concurrency issues
                var allItems = await _mainStockService.GetAllAsync();

                foreach (var item in allItems)
                {
                    if (!string.IsNullOrWhiteSpace(item.Barcode) && item.BarcodeImage == null)
                    {
                        try
                        {
                            var barcodeData = _barcodeService.GenerateBarcode(item.Barcode);
                            if (barcodeData != null)
                            {
                                item.BarcodeImage = barcodeData;

                                var itemCopy = new MainStockDTO
                                {
                                    MainStockId = item.MainStockId,
                                    Name = item.Name,
                                    Barcode = item.Barcode,
                                    CategoryId = item.CategoryId,
                                    CategoryName = item.CategoryName,
                                    SupplierId = item.SupplierId,
                                    SupplierName = item.SupplierName,
                                    Description = item.Description,
                                    PurchasePrice = item.PurchasePrice,
                                    SalePrice = item.SalePrice,
                                    CurrentStock = item.CurrentStock,
                                    MinimumStock = item.MinimumStock,
                                    BarcodeImage = item.BarcodeImage,
                                    Speed = item.Speed,
                                    IsActive = item.IsActive,
                                    ImagePath = item.ImagePath,
                                    CreatedAt = item.CreatedAt,
                                    UpdatedAt = DateTime.Now
                                };

                                await _mainStockService.UpdateAsync(itemCopy);
                                generatedCount++;

                                // Update status message periodically
                                if (generatedCount % 5 == 0)
                                {
                                    StatusMessage = $"Generated {generatedCount} barcode images...";
                                    await Task.Delay(10); // Allow UI to update
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error generating barcode for item {item.Name}: {ex.Message}");
                            failedCount++;
                            // Continue with next item
                        }
                    }
                }

                string resultMessage = $"Successfully generated {generatedCount} barcode images.";
                if (failedCount > 0)
                {
                    resultMessage += $" Failed to generate {failedCount} images.";
                }

                StatusMessage = resultMessage;
                await Task.Delay(2000);

                // Refresh items to ensure we have the latest data
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error generating barcode images: {ex.Message}");
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