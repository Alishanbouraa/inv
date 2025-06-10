using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Diagnostics;
using QuickTechSystems.WPF.ViewModels;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.WPF.Views
{
    public partial class ProductDetailsWindow : Window
    {
        public event RoutedEventHandler SaveCompleted;
        private static readonly Regex _decimalRegex = new Regex(@"^[0-9]*(?:\.[0-9]*)?$");
        private static readonly Regex _integerRegex = new Regex(@"^[0-9]*$");

        public ProductDetailsWindow()
        {
            InitializeComponent();
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Invoke the SaveCompleted event 
            SaveCompleted?.Invoke(this, new RoutedEventArgs());

            // Close the window with success result
            this.DialogResult = true;
            this.Close();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the window after delete
            this.DialogResult = false;
            this.Close();
        }

        // Add event handlers for price fields
        private void PriceTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Remove currency formatting and get the raw value
                string rawValue = textBox.Text.Replace("$", "").Replace(",", "").Trim();

                // If the value is 0 or 0.00, clear the text
                if (decimal.TryParse(rawValue, out decimal value) && value == 0)
                {
                    textBox.Text = string.Empty;
                }
            }
        }

        private void PriceTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // If empty, set to 0 to trigger the StringFormat in the binding
                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    textBox.Text = "0";
                }
                // Ensure proper decimal format with dot
                else if (decimal.TryParse(textBox.Text, out decimal value))
                {
                    textBox.Text = value.ToString("0.00").Replace(",", ".");
                }
            }
        }

        // Numeric validation for decimal input
        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !_decimalRegex.IsMatch(newText);
        }

        // Numeric validation for integer input
        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !_integerRegex.IsMatch(newText);
        }

        // Allow paste only for valid numeric input
        private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                var textBox = sender as TextBox;
                var isInteger = (string)textBox.Tag == "integer";
                var regex = isInteger ? _integerRegex : _decimalRegex;

                if (!regex.IsMatch(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        // Add Category Button Click Handler
        private async void AddCategoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the category service from the DI container
                var categoryService = ((App)System.Windows.Application.Current).ServiceProvider.GetService(typeof(ICategoryService)) as ICategoryService;
                if (categoryService == null)
                {
                    MessageBox.Show("Category service not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Show the quick category dialog
                var dialog = new QuickCategoryDialogWindow
                {
                    Owner = this
                };

                var result = dialog.ShowDialog();
                if (result == true && dialog.NewCategory != null)
                {
                    // Save the new category to the database
                    try
                    {
                        var newCategory = await categoryService.CreateAsync(dialog.NewCategory);

                        // Get the ViewModel
                        if (DataContext is ProductViewModel viewModel)
                        {
                            // Add the new category to the Categories collection
                            // The event system should take care of this, but we'll add it directly as a fallback
                            viewModel.Categories.Add(newCategory);

                            // Set it as the selected category
                            if (viewModel.SelectedProduct != null)
                            {
                                viewModel.SelectedProduct.CategoryId = newCategory.CategoryId;
                                viewModel.SelectedProduct.CategoryName = newCategory.Name;
                            }

                            MessageBox.Show($"Category '{newCategory.Name}' added successfully!", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating category: {ex.Message}");
                        MessageBox.Show($"Error creating category: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddCategoryButton_Click: {ex.Message}");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Add Supplier Button Click Handler
        private async void AddSupplierButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the supplier service from the DI container
                var supplierService = ((App)System.Windows.Application.Current).ServiceProvider.GetService(typeof(ISupplierService)) as ISupplierService;
                if (supplierService == null)
                {
                    MessageBox.Show("Supplier service not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Show the quick supplier dialog
                var dialog = new QuickSupplierDialogWindow
                {
                    Owner = this
                };

                var result = dialog.ShowDialog();
                if (result == true && dialog.NewSupplier != null)
                {
                    // Save the new supplier to the database
                    try
                    {
                        var newSupplier = await supplierService.CreateAsync(dialog.NewSupplier);

                        // Get the ViewModel
                        if (DataContext is ProductViewModel viewModel)
                        {
                            // Add the new supplier to the Suppliers collection
                            // The event system should take care of this, but we'll add it directly as a fallback
                            viewModel.Suppliers.Add(newSupplier);

                            // Set it as the selected supplier
                            if (viewModel.SelectedProduct != null)
                            {
                                viewModel.SelectedProduct.SupplierId = newSupplier.SupplierId;
                                viewModel.SelectedProduct.SupplierName = newSupplier.Name;
                            }

                            MessageBox.Show($"Supplier '{newSupplier.Name}' added successfully!", "Success",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error creating supplier: {ex.Message}");
                        MessageBox.Show($"Error creating supplier: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in AddSupplierButton_Click: {ex.Message}");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}