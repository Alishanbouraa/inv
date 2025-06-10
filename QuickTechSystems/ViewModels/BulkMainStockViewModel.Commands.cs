using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.Views;

namespace QuickTechSystems.WPF.ViewModels
{
    public partial class BulkMainStockViewModel
    {
        private void AddNewRow()
        {
            if (!AreRequiredFieldsFilled)
            {
                StatusMessage = "Please select Category, Supplier, and Invoice before adding items.";
                return;
            }

            var newItem = new MainStockDTO
            {
                IsActive = true,
                CreatedAt = DateTime.Now,
                ItemsPerBox = 0,
                NumberOfBoxes = 0
            };

            if (SelectedBulkCategory != null)
            {
                newItem.CategoryId = SelectedBulkCategory.CategoryId;
                newItem.CategoryName = SelectedBulkCategory.Name;
            }

            if (SelectedBulkSupplier != null)
            {
                newItem.SupplierId = SelectedBulkSupplier.SupplierId;
                newItem.SupplierName = SelectedBulkSupplier.Name;
            }

            if (SelectedBulkInvoice != null)
            {
                newItem.SupplierInvoiceId = SelectedBulkInvoice.SupplierInvoiceId;
            }

            Items.Add(newItem);

            SubscribeToItemPropertyChanges(newItem);
        }

        private void RemoveRow(MainStockDTO item)
        {
            if (item != null)
            {
                UnsubscribeFromItemPropertyChanges(item);
                Items.Remove(item);
            }
        }

        private void ClearAll()
        {
            var result = MessageBox.Show("Are you sure you want to clear all items?",
                "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                foreach (var item in Items)
                {
                    UnsubscribeFromItemPropertyChanges(item);
                }

                Items.Clear();
                AddNewRow();
            }
        }

        private void ApplyBulkCategory()
        {
            if (SelectedBulkCategory == null) return;

            foreach (var item in Items)
            {
                item.CategoryId = SelectedBulkCategory.CategoryId;
                item.CategoryName = SelectedBulkCategory.Name;
            }

            StatusMessage = $"Applied category '{SelectedBulkCategory.Name}' to all items.";

            if (AreRequiredFieldsFilled && Items.Count == 0)
            {
                AddNewRow();
            }
        }

        private void ApplyBulkSupplier()
        {
            if (SelectedBulkSupplier == null) return;

            foreach (var item in Items)
            {
                item.SupplierId = SelectedBulkSupplier.SupplierId;
                item.SupplierName = SelectedBulkSupplier.Name;
            }

            StatusMessage = $"Applied supplier '{SelectedBulkSupplier.Name}' to all items.";

            if (AreRequiredFieldsFilled && Items.Count == 0)
            {
                AddNewRow();
            }
        }

        private void ApplyBulkInvoice()
        {
            if (SelectedBulkInvoice == null) return;

            Debug.WriteLine($"Applying invoice {SelectedBulkInvoice.InvoiceNumber} (ID: {SelectedBulkInvoice.SupplierInvoiceId}) to {Items.Count} items");

            foreach (var item in Items)
            {
                item.SupplierInvoiceId = SelectedBulkInvoice.SupplierInvoiceId;
            }

            StatusMessage = $"All items will be associated with invoice '{SelectedBulkInvoice.InvoiceNumber}' (ID: {SelectedBulkInvoice.SupplierInvoiceId}).";

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"All items will be associated with invoice '{SelectedBulkInvoice.InvoiceNumber}'.\nPlease save the items to complete this association.",
                    "Invoice Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            });

            if (AreRequiredFieldsFilled && Items.Count == 0)
            {
                AddNewRow();
            }
        }

        private async Task AddNewCategoryAsync()
        {
            try
            {
                var dialog = new QuickCategoryDialogWindow
                {
                    Owner = GetOwnerWindow()
                };

                var result = dialog.ShowDialog();
                if (result == true && dialog.NewCategory != null)
                {
                    var newCategory = await _categoryService.CreateAsync(dialog.NewCategory);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Categories.Add(newCategory);
                        SelectedBulkCategory = newCategory;
                    });

                    StatusMessage = $"Category '{newCategory.Name}' added successfully.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding category: {ex.Message}";
                Debug.WriteLine($"Error in AddNewCategoryAsync: {ex}");
            }
        }

        private async Task AddNewSupplierAsync()
        {
            try
            {
                var dialog = new QuickSupplierDialogWindow
                {
                    Owner = GetOwnerWindow()
                };

                var result = dialog.ShowDialog();
                if (result == true && dialog.NewSupplier != null)
                {
                    var newSupplier = await _supplierService.CreateAsync(dialog.NewSupplier);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Suppliers.Add(newSupplier);
                        SelectedBulkSupplier = newSupplier;
                    });

                    StatusMessage = $"Supplier '{newSupplier.Name}' added successfully.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error adding supplier: {ex.Message}";
                Debug.WriteLine($"Error in AddNewSupplierAsync: {ex}");
            }
        }

        private async Task AddNewInvoiceAsync()
        {
            try
            {
                var dialog = new QuickSupplierInvoiceDialog
                {
                    Owner = GetOwnerWindow()
                };

                var result = dialog.ShowDialog();
                if (result == true && dialog.CreatedInvoice != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SupplierInvoices.Add(dialog.CreatedInvoice);
                        SelectedBulkInvoice = dialog.CreatedInvoice;
                    });

                    StatusMessage = $"Invoice '{dialog.CreatedInvoice.InvoiceNumber}' created successfully.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating invoice: {ex.Message}";
                Debug.WriteLine($"Error in AddNewInvoiceAsync: {ex}");
            }
        }

        private void UploadItemImage(MainStockDTO item)
        {
            if (item == null) return;

            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                    Title = "Select an image for the item"
                };

                var ownerWindow = GetOwnerWindow();

                if (dialog.ShowDialog(ownerWindow) == true)
                {
                    string imagePath = _imagePathService.SaveProductImage(dialog.FileName);
                    item.ImagePath = imagePath;

                    if (item is INotifyPropertyChanged notifyItem)
                    {
                        var propertyChanged = item.GetType().GetField("PropertyChanged",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic);

                        if (propertyChanged != null)
                        {
                            var handler = propertyChanged.GetValue(item) as PropertyChangedEventHandler;
                            handler?.Invoke(item, new PropertyChangedEventArgs(nameof(item.ImagePath)));
                        }
                    }

                    StatusMessage = $"Image uploaded for item '{item.Name}'.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error uploading image: {ex.Message}";
                Debug.WriteLine($"Error in UploadItemImage: {ex}");
            }
        }

        private void ClearItemImage(MainStockDTO item)
        {
            if (item == null || string.IsNullOrEmpty(item.ImagePath)) return;

            try
            {
                _imagePathService.DeleteProductImage(item.ImagePath);
                item.ImagePath = null;
                StatusMessage = $"Image cleared for item '{item.Name}'.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error clearing image: {ex.Message}";
                Debug.WriteLine($"Error in ClearItemImage: {ex}");
            }
        }
    }
}