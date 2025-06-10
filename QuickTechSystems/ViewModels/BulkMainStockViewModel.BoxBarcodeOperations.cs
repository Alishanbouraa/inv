// Path: QuickTechSystems.WPF.ViewModels/BulkMainStockViewModel.BoxBarcodeOperations.cs
using System;
using System.ComponentModel;
using System.Diagnostics;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class BulkMainStockViewModel
    {
        /// <summary>
        /// Validates the barcode of the specified item.
        /// </summary>
        /// <param name="item">The item to validate.</param>
        private void ValidateItem(MainStockDTO item)
        {
            if (item == null) return;

            // This gets called when a field loses focus - perfect time to compare both fields
            ValidateAndUpdateBoxBarcode(item);
        }

        /// <summary>
        /// Subscribes to property changes for the specified item.
        /// </summary>
        /// <param name="item">The item to subscribe to.</param>
        private void SubscribeToItemPropertyChanges(MainStockDTO item)
        {
            if (item != null)
            {
                // Subscribe to property changes
                item.PropertyChanged += Item_PropertyChanged;
            }
        }

        /// <summary>
        /// Unsubscribes from property changes for the specified item.
        /// </summary>
        /// <param name="item">The item to unsubscribe from.</param>
        private void UnsubscribeFromItemPropertyChanges(MainStockDTO item)
        {
            if (item != null)
            {
                // Unsubscribe to prevent memory leaks
                item.PropertyChanged -= Item_PropertyChanged;
            }
        }

        /// <summary>
        /// Handles property changes for an item.
        /// </summary>
        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var item = sender as MainStockDTO;
            if (item == null) return;

            // When barcode changes, check if box barcode should be updated
            if (e.PropertyName == nameof(MainStockDTO.Barcode))
            {
                ValidateAndUpdateBoxBarcode(item);
            }
            // When box barcode changes, check if it matches item barcode
            else if (e.PropertyName == nameof(MainStockDTO.BoxBarcode))
            {
                ValidateAndUpdateBoxBarcode(item);
            }
        }

        /// </summary>
        /// <param name="item">The item to validate.</param>
        private void ValidateAndUpdateBoxBarcode(MainStockDTO item)
        {
            if (item == null) return;

            // Only perform validation when fields have values
            if (!string.IsNullOrWhiteSpace(item.Barcode))
            {
                // Case 1: Box barcode exactly matches item barcode - add prefix
                if (!string.IsNullOrWhiteSpace(item.BoxBarcode) && item.BoxBarcode == item.Barcode)
                {
                    // Apply BX prefix when there's an exact match
                    item.BoxBarcode = $"BX{item.Barcode}";
                    Debug.WriteLine($"Auto-updated box barcode for {item.Name} to {item.BoxBarcode}");
                }
                // Case 2: Box barcode is empty - add prefix to item barcode
                else if (string.IsNullOrWhiteSpace(item.BoxBarcode))
                {
                    item.BoxBarcode = $"BX{item.Barcode}";
                    Debug.WriteLine($"Set default box barcode for {item.Name} to {item.BoxBarcode}");
                }
                // Case 3: Box barcode exists but doesn't match item barcode
                // In this case, we don't make any changes to preserve user customization
            }

            // Allow ItemsPerBox to remain 0 if that's what the user set
            // Only auto-set ItemsPerBox if boxes exist but ItemsPerBox is negative (which shouldn't happen)
            if (item.ItemsPerBox < 0 && item.NumberOfBoxes > 0)
            {
                item.ItemsPerBox = 0; // Reset to 0 instead of negative
            }
        }
    }
}