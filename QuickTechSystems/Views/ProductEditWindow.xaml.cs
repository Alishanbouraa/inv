// Path: QuickTechSystems.WPF.Views/ProductEditWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Services.Interfaces;

namespace QuickTechSystems.WPF.Views
{
    /// <summary>
    /// Interaction logic for ProductEditWindow.xaml
    /// </summary>
    public partial class ProductEditWindow : Window, INotifyPropertyChanged
    {
        private readonly IProductService _productService;
        private readonly ICategoryService _categoryService;
        private readonly ISupplierService _supplierService;
        private readonly ProductDTO _originalProduct;

        private ProductDTO _editableProduct;
        private ObservableCollection<CategoryDTO> _categories;
        private ObservableCollection<SupplierDTO> _suppliers;
        private ObservableCollection<CalculatedValueDto> _calculatedValues;
        private bool _isSaving;
        private string _statusMessage = string.Empty;
        private Dictionary<string, List<string>> _validationErrors = new Dictionary<string, List<string>>();

        // Properties for data binding
        public ProductDTO EditableProduct
        {
            get => _editableProduct;
            set
            {
                _editableProduct = value;
                OnPropertyChanged();
                ValidateProduct();
                UpdateCalculatedValues();
            }
        }

        public ObservableCollection<CategoryDTO> Categories
        {
            get => _categories;
            set
            {
                _categories = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<SupplierDTO> Suppliers
        {
            get => _suppliers;
            set
            {
                _suppliers = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<CalculatedValueDto> CalculatedValues
        {
            get => _calculatedValues;
            set
            {
                _calculatedValues = value;
                OnPropertyChanged();
            }
        }

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                _isSaving = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotSaving));
            }
        }

        public bool IsNotSaving => !IsSaving;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public Dictionary<string, List<string>> ValidationErrors
        {
            get => _validationErrors;
            set
            {
                _validationErrors = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationErrors));
                OnPropertyChanged(nameof(CanSave));
            }
        }

        public bool HasValidationErrors => ValidationErrors.Any(kvp => kvp.Value.Any());

        public bool CanSave => IsNotSaving && !HasValidationErrors && EditableProduct != null;

        // Commands
        public ICommand SaveCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand ResetCommand { get; private set; }

        /// <summary>
        /// Constructor for ProductEditWindow
        /// </summary>
        /// <param name="product">The product to edit</param>
        /// <param name="productService">Product service for data operations</param>
        /// <param name="categoryService">Category service for loading categories</param>
        /// <param name="supplierService">Supplier service for loading suppliers</param>
        public ProductEditWindow(
            ProductDTO product,
            IProductService productService,
            ICategoryService categoryService,
            ISupplierService supplierService)
        {
            _productService = productService ?? throw new ArgumentNullException(nameof(productService));
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
            _originalProduct = product ?? throw new ArgumentNullException(nameof(product));

            InitializeComponent();
            InitializeCommands();
            InitializeData();

            this.DataContext = this;
            this.Loaded += ProductEditWindow_Loaded;
        }

        private void InitializeCommands()
        {
            SaveCommand = new RelayCommand(
                executeAsync: async (parameter) => await SaveProductAsync(),
                canExecute: _ => CanSave);

            CancelCommand = new RelayCommand(
                execute: _ => CancelEdit());

            ResetCommand = new RelayCommand(
                execute: _ => ResetToOriginal());
        }

        private void InitializeData()
        {
            // Create a copy of the original product for editing
            EditableProduct = CreateProductCopy(_originalProduct);

            // Initialize collections
            Categories = new ObservableCollection<CategoryDTO>();
            Suppliers = new ObservableCollection<SupplierDTO>();
            CalculatedValues = new ObservableCollection<CalculatedValueDto>();

            // Set up property change notification for real-time updates
            if (EditableProduct != null)
            {
                EditableProduct.PropertyChanged += EditableProduct_PropertyChanged;
            }
        }

        private void EditableProduct_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Update calculated values when relevant properties change
            if (e.PropertyName == nameof(ProductDTO.PurchasePrice) ||
                e.PropertyName == nameof(ProductDTO.SalePrice) ||
                e.PropertyName == nameof(ProductDTO.WholesalePrice) ||
                e.PropertyName == nameof(ProductDTO.CurrentStock) ||
                e.PropertyName == nameof(ProductDTO.BoxPurchasePrice) ||
                e.PropertyName == nameof(ProductDTO.BoxSalePrice) ||
                e.PropertyName == nameof(ProductDTO.BoxWholesalePrice) ||
                e.PropertyName == nameof(ProductDTO.ItemsPerBox))
            {
                UpdateCalculatedValues();
            }

            // Re-validate when any property changes
            ValidateProduct();
        }

        private void UpdateCalculatedValues()
        {
            if (EditableProduct == null) return;

            var values = new List<CalculatedValueDto>();

            try
            {
                // Total Cost (Purchase Price × Current Stock)
                var totalCost = EditableProduct.PurchasePrice * EditableProduct.CurrentStock;
                values.Add(new CalculatedValueDto
                {
                    PropertyName = "Total Cost",
                    Value = totalCost.ToString("C2"),
                    Description = "Purchase Price × Current Stock"
                });

                // Total Value (Sale Price × Current Stock)
                var totalValue = EditableProduct.SalePrice * EditableProduct.CurrentStock;
                values.Add(new CalculatedValueDto
                {
                    PropertyName = "Total Value",
                    Value = totalValue.ToString("C2"),
                    Description = "Sale Price × Current Stock"
                });

                // Profit Margin
                var profitMargin = totalValue - totalCost;
                values.Add(new CalculatedValueDto
                {
                    PropertyName = "Profit Margin",
                    Value = profitMargin.ToString("C2"),
                    Description = "Total Value - Total Cost"
                });

                // Profit Percentage
                var profitPercentage = totalCost > 0 ? (profitMargin / totalCost) * 100 : 0;
                values.Add(new CalculatedValueDto
                {
                    PropertyName = "Profit %",
                    Value = profitPercentage.ToString("F2") + "%",
                    Description = "Profit Margin / Total Cost × 100"
                });

                // Item Purchase Price (Box Price ÷ Items per Box)
                var itemPurchasePrice = EditableProduct.ItemsPerBox > 0 ? EditableProduct.BoxPurchasePrice / EditableProduct.ItemsPerBox : 0;
                values.Add(new CalculatedValueDto
                {
                    PropertyName = "Item Purchase Price",
                    Value = itemPurchasePrice.ToString("C2"),
                    Description = "Box Purchase Price ÷ Items per Box"
                });

                // Item Wholesale Price
                var itemWholesalePrice = EditableProduct.ItemsPerBox > 0 ? EditableProduct.BoxWholesalePrice / EditableProduct.ItemsPerBox : 0;
                values.Add(new CalculatedValueDto
                {
                    PropertyName = "Item Wholesale Price",
                    Value = itemWholesalePrice.ToString("C2"),
                    Description = "Box Wholesale Price ÷ Items per Box"
                });

                // Complete Boxes
                var completeBoxes = EditableProduct.ItemsPerBox > 0 ? EditableProduct.CurrentStock / EditableProduct.ItemsPerBox : 0;
                values.Add(new CalculatedValueDto
                {
                    PropertyName = "Complete Boxes",
                    Value = completeBoxes.ToString("N0"),
                    Description = "Current Stock ÷ Items per Box"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating calculated values: {ex.Message}");
            }

            CalculatedValues = new ObservableCollection<CalculatedValueDto>(values);
        }

        private async void ProductEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        /// <summary>
        /// Loads categories and suppliers from the services
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {
                IsSaving = true;
                StatusMessage = "Loading data...";

                // Load categories and suppliers concurrently
                var categoriesTask = _categoryService.GetActiveAsync();
                var suppliersTask = _supplierService.GetActiveAsync();

                await Task.WhenAll(categoriesTask, suppliersTask);

                var categories = await categoriesTask;
                var suppliers = await suppliersTask;

                // Update UI on main thread
                await Dispatcher.InvokeAsync(() =>
                {
                    Categories.Clear();
                    foreach (var category in categories)
                    {
                        Categories.Add(category);
                    }

                    Suppliers.Clear();
                    foreach (var supplier in suppliers)
                    {
                        Suppliers.Add(supplier);
                    }

                    SetCurrentSelections();
                });

                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading data: {ex.Message}";
                Debug.WriteLine($"Error in LoadDataAsync: {ex}");
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void SetCurrentSelections()
        {
            // Ensure the current category and supplier are selected
            if (EditableProduct.CategoryId > 0)
            {
                var selectedCategory = Categories.FirstOrDefault(c => c.CategoryId == EditableProduct.CategoryId);
                if (selectedCategory != null)
                {
                    EditableProduct.CategoryName = selectedCategory.Name;
                }
            }

            if (EditableProduct.SupplierId.HasValue && EditableProduct.SupplierId > 0)
            {
                var selectedSupplier = Suppliers.FirstOrDefault(s => s.SupplierId == EditableProduct.SupplierId);
                if (selectedSupplier != null)
                {
                    EditableProduct.SupplierName = selectedSupplier.Name;
                }
            }
        }

        /// <summary>
        /// Creates a deep copy of the product for editing
        /// </summary>
        /// <param name="original">The original product</param>
        /// <returns>A copy of the product</returns>
        private ProductDTO CreateProductCopy(ProductDTO original)
        {
            return new ProductDTO
            {
                ProductId = original.ProductId,
                Name = original.Name,
                Barcode = original.Barcode,
                BoxBarcode = original.BoxBarcode,
                Description = original.Description,
                CategoryId = original.CategoryId,
                CategoryName = original.CategoryName,
                SupplierId = original.SupplierId,
                SupplierName = original.SupplierName,
                MainStockId = original.MainStockId,
                PurchasePrice = original.PurchasePrice,
                SalePrice = original.SalePrice,
                WholesalePrice = original.WholesalePrice,
                CurrentStock = original.CurrentStock,
                MinimumStock = original.MinimumStock,
                BoxPurchasePrice = original.BoxPurchasePrice,
                BoxSalePrice = original.BoxSalePrice,
                BoxWholesalePrice = original.BoxWholesalePrice,
                ItemsPerBox = original.ItemsPerBox,
                NumberOfBoxes = original.NumberOfBoxes,
                MinimumBoxStock = original.MinimumBoxStock,
                ImagePath = original.ImagePath,
                Speed = original.Speed,
                IsActive = original.IsActive,
                CreatedAt = original.CreatedAt,
                UpdatedAt = original.UpdatedAt,
                BarcodeImage = original.BarcodeImage
            };
        }

        /// <summary>
        /// Validates the current product data
        /// </summary>
        private void ValidateProduct()
        {
            var errors = new Dictionary<string, List<string>>();

            if (EditableProduct == null)
            {
                ValidationErrors = errors;
                return;
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(EditableProduct.Name))
            {
                AddValidationError(errors, nameof(EditableProduct.Name), "Product name is required.");
            }

            if (string.IsNullOrWhiteSpace(EditableProduct.Barcode))
            {
                AddValidationError(errors, nameof(EditableProduct.Barcode), "Barcode is required.");
            }

            if (EditableProduct.CategoryId <= 0)
            {
                AddValidationError(errors, nameof(EditableProduct.CategoryId), "Please select a category.");
            }

            // Validate prices
            if (EditableProduct.PurchasePrice < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.PurchasePrice), "Purchase price cannot be negative.");
            }

            if (EditableProduct.SalePrice < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.SalePrice), "Sale price cannot be negative.");
            }

            if (EditableProduct.WholesalePrice < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.WholesalePrice), "Wholesale price cannot be negative.");
            }

            // Validate stock levels
            if (EditableProduct.CurrentStock < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.CurrentStock), "Current stock cannot be negative.");
            }

            if (EditableProduct.MinimumStock < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.MinimumStock), "Minimum stock cannot be negative.");
            }

            // Validate box-related fields
            if (EditableProduct.ItemsPerBox <= 0)
            {
                AddValidationError(errors, nameof(EditableProduct.ItemsPerBox), "Items per box must be greater than zero.");
            }

            if (EditableProduct.BoxPurchasePrice < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.BoxPurchasePrice), "Box purchase price cannot be negative.");
            }

            if (EditableProduct.BoxSalePrice < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.BoxSalePrice), "Box sale price cannot be negative.");
            }

            if (EditableProduct.BoxWholesalePrice < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.BoxWholesalePrice), "Box wholesale price cannot be negative.");
            }

            if (EditableProduct.NumberOfBoxes < 0)
            {
                AddValidationError(errors, nameof(EditableProduct.NumberOfBoxes), "Number of boxes cannot be negative.");
            }

            ValidationErrors = errors;
        }

        /// <summary>
        /// Adds a validation error to the errors dictionary
        /// </summary>
        private void AddValidationError(Dictionary<string, List<string>> errors, string propertyName, string errorMessage)
        {
            if (!errors.ContainsKey(propertyName))
            {
                errors[propertyName] = new List<string>();
            }
            errors[propertyName].Add(errorMessage);
        }

        /// <summary>
        /// Saves the product changes
        /// </summary>
        private async Task SaveProductAsync()
        {
            try
            {
                IsSaving = true;
                StatusMessage = "Saving product...";

                // Validate before saving
                ValidateProduct();
                if (HasValidationErrors)
                {
                    StatusMessage = "Please fix validation errors before saving.";
                    return;
                }

                // Check for barcode uniqueness if it changed
                if (EditableProduct.Barcode != _originalProduct.Barcode)
                {
                    ProductDTO existingProduct = await _productService.FindProductByBarcodeAsync(
                        EditableProduct.Barcode,
                        EditableProduct.ProductId);

                    if (existingProduct != null)
                    {
                        StatusMessage = "A product with this barcode already exists.";
                        MessageBox.Show("A product with this barcode already exists. Please use a different barcode.",
                            "Duplicate Barcode", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Update the editable product's timestamp
                EditableProduct.UpdatedAt = DateTime.Now;

                // Call UpdateAsync without expecting a return value
                await _productService.UpdateAsync(EditableProduct);

                StatusMessage = "Product saved successfully!";

                // Show success message and close the window
                MessageBox.Show("Product updated successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving product: {ex.Message}";
                Debug.WriteLine($"Error in SaveProductAsync: {ex}");

                MessageBox.Show($"An error occurred while saving the product:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
                if (StatusMessage.StartsWith("Product saved"))
                {
                    // Clear success message after a delay
                    await Task.Delay(2000);
                    if (StatusMessage.StartsWith("Product saved"))
                    {
                        StatusMessage = string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Cancels the edit operation
        /// </summary>
        private void CancelEdit()
        {
            if (HasUnsavedChanges())
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to cancel?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Resets the editable product to the original values
        /// </summary>
        private void ResetToOriginal()
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all changes?",
                "Reset Changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                EditableProduct = CreateProductCopy(_originalProduct);
                StatusMessage = "Changes reset to original values.";

                // Clear the status message after a delay
                Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (StatusMessage == "Changes reset to original values.")
                        {
                            StatusMessage = string.Empty;
                        }
                    });
                });
            }
        }

        /// <summary>
        /// Checks if there are unsaved changes
        /// </summary>
        /// <returns>True if there are unsaved changes</returns>
        private bool HasUnsavedChanges()
        {
            if (EditableProduct == null || _originalProduct == null)
                return false;

            return EditableProduct.Name != _originalProduct.Name ||
                   EditableProduct.Barcode != _originalProduct.Barcode ||
                   EditableProduct.BoxBarcode != _originalProduct.BoxBarcode ||
                   EditableProduct.Description != _originalProduct.Description ||
                   EditableProduct.CategoryId != _originalProduct.CategoryId ||
                   EditableProduct.SupplierId != _originalProduct.SupplierId ||
                   EditableProduct.PurchasePrice != _originalProduct.PurchasePrice ||
                   EditableProduct.SalePrice != _originalProduct.SalePrice ||
                   EditableProduct.WholesalePrice != _originalProduct.WholesalePrice ||
                   EditableProduct.CurrentStock != _originalProduct.CurrentStock ||
                   EditableProduct.MinimumStock != _originalProduct.MinimumStock ||
                   EditableProduct.BoxPurchasePrice != _originalProduct.BoxPurchasePrice ||
                   EditableProduct.BoxSalePrice != _originalProduct.BoxSalePrice ||
                   EditableProduct.BoxWholesalePrice != _originalProduct.BoxWholesalePrice ||
                   EditableProduct.ItemsPerBox != _originalProduct.ItemsPerBox ||
                   EditableProduct.NumberOfBoxes != _originalProduct.NumberOfBoxes ||
                   EditableProduct.MinimumBoxStock != _originalProduct.MinimumBoxStock ||
                   EditableProduct.ImagePath != _originalProduct.ImagePath ||
                   EditableProduct.Speed != _originalProduct.Speed ||
                   EditableProduct.IsActive != _originalProduct.IsActive;
        }

        #region Event Handlers

        /// <summary>
        /// Handles the category selection change
        /// </summary>
        private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is CategoryDTO selectedCategory)
            {
                EditableProduct.CategoryId = selectedCategory.CategoryId;
                EditableProduct.CategoryName = selectedCategory.Name;
                ValidateProduct();
            }
        }

        /// <summary>
        /// Handles the supplier selection change
        /// </summary>
        private void OnSupplierSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is SupplierDTO selectedSupplier)
            {
                EditableProduct.SupplierId = selectedSupplier.SupplierId;
                EditableProduct.SupplierName = selectedSupplier.Name;
            }
            else
            {
                EditableProduct.SupplierId = null;
                EditableProduct.SupplierName = string.Empty;
            }
        }

        /// <summary>
        /// Browse for image file
        /// </summary>
        private void BrowseImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Title = "Select Product Image",
                    Filter = "Image Files (*.jpg;*.jpeg;*.png;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.gif;*.bmp|All Files (*.*)|*.*",
                    FilterIndex = 1,
                    RestoreDirectory = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    EditableProduct.ImagePath = openFileDialog.FileName;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting image: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Generate barcode for the product
        /// </summary>
        private void GenerateBarcode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(EditableProduct.Barcode))
                {
                    MessageBox.Show("Please enter a barcode first.", "Barcode Required",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // TODO: Implement barcode generation
                MessageBox.Show($"Barcode generation for '{EditableProduct.Barcode}' would be implemented here.",
                    "Barcode Generation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating barcode: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Preview changes before saving
        /// </summary>
        private void PreviewChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var changes = GetChanges();
                if (changes.Any())
                {
                    var changeText = string.Join("\n", changes);
                    MessageBox.Show($"Changes to be saved:\n\n{changeText}", "Preview Changes",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("No changes detected.", "Preview Changes",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error previewing changes: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Copy calculated value to clipboard
        /// </summary>
        private void CopyValue_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.CommandParameter is string value)
                {
                    Clipboard.SetText(value);
                    MessageBox.Show($"Value '{value}' copied to clipboard.", "Copied",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error copying value: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Recalculate specific value
        /// </summary>
        private void Recalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.CommandParameter is string propertyName)
                {
                    UpdateCalculatedValues();
                    MessageBox.Show($"'{propertyName}' recalculated.", "Recalculated",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error recalculating: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            try
            {
                // Allow only numeric input for numeric fields
                if (sender is TextBox textBox)
                {
                    switch (textBox.Name)
                    {
                        case "CurrentStockTextBox":
                        case "MinimumStockTextBox":
                        case "ItemsPerBoxTextBox":
                        case "NumberOfBoxesTextBox":
                        case "MinimumBoxStockTextBox":
                            // Allow only digits
                            e.Handled = !IsNumeric(e.Text);
                            break;
                        case "PurchasePriceTextBox":
                        case "SalePriceTextBox":
                        case "WholesalePriceTextBox":
                        case "BoxPurchasePriceTextBox":
                        case "BoxSalePriceTextBox":
                        case "BoxWholesalePriceTextBox":
                            // Allow digits and decimal point
                            e.Handled = !IsDecimal(e.Text, textBox.Text);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TextBox_PreviewTextInput: {ex.Message}");
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    textBox.SelectAll();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in TextBox_GotFocus: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        private bool IsNumeric(string text)
        {
            return int.TryParse(text, out _);
        }

        private bool IsDecimal(string input, string currentText)
        {
            // Allow digits
            if (char.IsDigit(input, 0))
                return true;

            // Allow decimal point if there isn't one already
            if (input == "." && !currentText.Contains("."))
                return true;

            return false;
        }

        private List<string> GetChanges()
        {
            var changes = new List<string>();

            if (EditableProduct == null || _originalProduct == null)
                return changes;

            if (EditableProduct.Name != _originalProduct.Name)
                changes.Add($"Name: '{_originalProduct.Name}' → '{EditableProduct.Name}'");

            if (EditableProduct.Barcode != _originalProduct.Barcode)
                changes.Add($"Barcode: '{_originalProduct.Barcode}' → '{EditableProduct.Barcode}'");

            if (EditableProduct.PurchasePrice != _originalProduct.PurchasePrice)
                changes.Add($"Purchase Price: {_originalProduct.PurchasePrice:C2} → {EditableProduct.PurchasePrice:C2}");

            if (EditableProduct.SalePrice != _originalProduct.SalePrice)
                changes.Add($"Sale Price: {_originalProduct.SalePrice:C2} → {EditableProduct.SalePrice:C2}");

            if (EditableProduct.CurrentStock != _originalProduct.CurrentStock)
                changes.Add($"Current Stock: {_originalProduct.CurrentStock} → {EditableProduct.CurrentStock}");

            // Add more change comparisons as needed...

            return changes;
        }

        #endregion

        /// <summary>
        /// Handles property changed events for data binding
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Handles the window closing event
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            if (DialogResult != true && HasUnsavedChanges())
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Clean up event subscriptions
            if (EditableProduct != null)
            {
                EditableProduct.PropertyChanged -= EditableProduct_PropertyChanged;
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// Enhanced RelayCommand implementation for this window
        /// </summary>
        private class RelayCommand : ICommand
        {
            private readonly Func<object, Task> _executeAsync;
            private readonly Action<object> _execute;
            private readonly Predicate<object> _canExecute;

            // Constructor for synchronous commands
            public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            // Constructor for asynchronous commands with explicit parameter names
            public RelayCommand(Func<object, Task> executeAsync, Predicate<object> canExecute = null)
            {
                _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute?.Invoke(parameter) ?? true;
            }

            public async void Execute(object parameter)
            {
                try
                {
                    if (_executeAsync != null)
                    {
                        await _executeAsync(parameter);
                    }
                    else if (_execute != null)
                    {
                        _execute(parameter);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error executing command: {ex.Message}");
                    MessageBox.Show($"An error occurred: {ex.Message}", "Command Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    /// <summary>
    /// DTO for calculated values display in DataGrid
    /// </summary>
    public class CalculatedValueDto
    {
        public string PropertyName { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
    }
}