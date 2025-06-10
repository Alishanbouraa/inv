// QuickTechSystems/ViewModels/BulkMainStockViewModel.LookupOperations.cs
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class BulkMainStockViewModel
    {
        /// <summary>
        /// Looks up a product by barcode.
        /// </summary>
        /// <param name="currentItem">The item to populate with product data.</param>
        private async Task LookupProductAsync(MainStockDTO currentItem)
        {
            if (currentItem == null || string.IsNullOrWhiteSpace(currentItem.Barcode))
                return;

            try
            {
                StatusMessage = "Searching for product...";
                IsSaving = true;

                // Look for existing product by barcode using a fresh DbContext
                var existingProduct = await _mainStockService.GetByBarcodeAsync(currentItem.Barcode);

                // If found, populate the current item with existing data
                if (existingProduct != null)
                {
                    // Keep the current box quantities
                    int numberOfBoxes = currentItem.NumberOfBoxes > 0 ? currentItem.NumberOfBoxes : 1;

                    // Copy all properties from existing product
                    currentItem.MainStockId = existingProduct.MainStockId;
                    currentItem.Name = existingProduct.Name;
                    currentItem.Description = existingProduct.Description;
                    currentItem.CategoryId = existingProduct.CategoryId;
                    currentItem.CategoryName = existingProduct.CategoryName;
                    currentItem.SupplierId = existingProduct.SupplierId;
                    currentItem.SupplierName = existingProduct.SupplierName;
                    currentItem.PurchasePrice = existingProduct.PurchasePrice;
                    currentItem.SalePrice = existingProduct.SalePrice;
                    currentItem.WholesalePrice = existingProduct.WholesalePrice;
                    currentItem.BoxBarcode = existingProduct.BoxBarcode;
                    currentItem.BoxPurchasePrice = existingProduct.BoxPurchasePrice;
                    currentItem.BoxSalePrice = existingProduct.BoxSalePrice;
                    currentItem.BoxWholesalePrice = existingProduct.BoxWholesalePrice;
                    currentItem.ItemsPerBox = existingProduct.ItemsPerBox > 0 ? existingProduct.ItemsPerBox : 1;
                    currentItem.MinimumStock = existingProduct.MinimumStock;
                    currentItem.MinimumBoxStock = existingProduct.MinimumBoxStock;
                    currentItem.Speed = existingProduct.Speed;
                    currentItem.IsActive = existingProduct.IsActive;
                    currentItem.ImagePath = existingProduct.ImagePath;
                    currentItem.CurrentStock = existingProduct.CurrentStock;
                    currentItem.IndividualItems = (int)existingProduct.CurrentStock;

                    // Restore the number of boxes as this is likely what the user is adding
                    currentItem.NumberOfBoxes = numberOfBoxes;

                    // Generate barcode image if needed
                    if (currentItem.BarcodeImage == null)
                    {
                        try
                        {
                            currentItem.BarcodeImage = _barcodeService.GenerateBarcode(currentItem.Barcode);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error generating barcode image: {ex.Message}");
                        }
                    }

                    StatusMessage = $"Found existing product: {currentItem.Name}";
                }
                else
                {
                    // Check if box barcode field is empty or the same as item barcode
                    if (string.IsNullOrWhiteSpace(currentItem.BoxBarcode))
                    {
                        // Generate box barcode
                        currentItem.BoxBarcode = $"BX{currentItem.Barcode}";
                    }
                    else if (currentItem.BoxBarcode == currentItem.Barcode)
                    {
                        // Auto-prefix the box barcode with "BX" only when there's an exact match
                        currentItem.BoxBarcode = $"BX{currentItem.Barcode}";
                    }

                    StatusMessage = "New product. Please enter details.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error looking up product: {ex.Message}";
                Debug.WriteLine($"Error in LookupProductAsync: {ex}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>
        /// Looks up a product by box barcode.
        /// </summary>
        /// <param name="currentItem">The item to populate with product data.</param>
        private async Task LookupBoxBarcodeAsync(MainStockDTO currentItem)
        {
            if (currentItem == null || string.IsNullOrWhiteSpace(currentItem.BoxBarcode))
                return;

            try
            {
                StatusMessage = "Searching for product by box barcode...";
                IsSaving = true;

                // Look for existing product by box barcode
                var existingProduct = await _mainStockService.GetByBoxBarcodeAsync(currentItem.BoxBarcode);

                if (existingProduct != null)
                {
                    // Keep the current box quantities
                    int numberOfBoxes = currentItem.NumberOfBoxes > 0 ? currentItem.NumberOfBoxes : 1;

                    // Copy properties from existing product
                    currentItem.MainStockId = existingProduct.MainStockId;
                    currentItem.Name = existingProduct.Name;
                    currentItem.Barcode = existingProduct.Barcode;
                    currentItem.Description = existingProduct.Description;
                    currentItem.CategoryId = existingProduct.CategoryId;
                    currentItem.CategoryName = existingProduct.CategoryName;
                    currentItem.SupplierId = existingProduct.SupplierId;
                    currentItem.SupplierName = existingProduct.SupplierName;
                    currentItem.PurchasePrice = existingProduct.PurchasePrice;
                    currentItem.WholesalePrice = existingProduct.WholesalePrice;
                    currentItem.SalePrice = existingProduct.SalePrice;
                    currentItem.BoxPurchasePrice = existingProduct.BoxPurchasePrice;
                    currentItem.BoxWholesalePrice = existingProduct.BoxWholesalePrice;
                    currentItem.BoxSalePrice = existingProduct.BoxSalePrice;
                    currentItem.ItemsPerBox = existingProduct.ItemsPerBox > 0 ? existingProduct.ItemsPerBox : 1;
                    currentItem.MinimumStock = existingProduct.MinimumStock;
                    currentItem.MinimumBoxStock = existingProduct.MinimumBoxStock;
                    currentItem.Speed = existingProduct.Speed;
                    currentItem.IsActive = existingProduct.IsActive;
                    currentItem.ImagePath = existingProduct.ImagePath;
                    currentItem.CurrentStock = existingProduct.CurrentStock;
                    currentItem.IndividualItems = (int)existingProduct.CurrentStock;

                    // Restore the number of boxes
                    currentItem.NumberOfBoxes = numberOfBoxes;

                    // Generate barcode image if needed
                    if (currentItem.BarcodeImage == null && !string.IsNullOrWhiteSpace(currentItem.Barcode))
                    {
                        try
                        {
                            currentItem.BarcodeImage = _barcodeService.GenerateBarcode(currentItem.Barcode);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error generating barcode image: {ex.Message}");
                        }
                    }

                    StatusMessage = $"Found existing product by box barcode: {currentItem.Name}";
                }
                else
                {
                    // If not found and the box barcode doesn't start with "BX", try with it
                    if (!currentItem.BoxBarcode.StartsWith("BX", StringComparison.OrdinalIgnoreCase))
                    {
                        var modifiedBoxBarcode = $"BX{currentItem.BoxBarcode}";
                        try
                        {
                            var productWithPrefix = await _mainStockService.GetByBoxBarcodeAsync(modifiedBoxBarcode);
                            if (productWithPrefix != null)
                            {
                                // Update the box barcode to the correct format
                                currentItem.BoxBarcode = modifiedBoxBarcode;

                                // Recursively call this method again with the updated barcode
                                await LookupBoxBarcodeAsync(currentItem);
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
                StatusMessage = $"Error looking up box barcode: {ex.Message}";
                Debug.WriteLine($"Error in LookupBoxBarcodeAsync: {ex}");
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}