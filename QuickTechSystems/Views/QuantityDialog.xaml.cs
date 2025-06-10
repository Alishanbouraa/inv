using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuickTechSystems.WPF.Views
{
    public partial class QuantityDialog : Window
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public decimal NewQuantity { get; set; }
        private bool _isUpdatingText = false;

        public QuantityDialog(string productName, decimal currentQuantity)
        {
            InitializeComponent();
            ProductName = productName;
            CurrentQuantity = currentQuantity;
            NewQuantity = currentQuantity;
            DataContext = this;

            // Set focus to the quantity textbox and select all text when the dialog loads
            this.Loaded += (s, e) =>
            {
                QuantityTextBox.Focus();
                QuantityTextBox.SelectAll();
            };

            // Handle closing
            this.Closing += (s, e) =>
            {
                if (!DialogResult.HasValue)
                    DialogResult = false;
            };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Make sure we have a valid value before accepting
                if (string.IsNullOrWhiteSpace(QuantityTextBox.Text))
                {
                    MessageBox.Show(this, "Please enter a quantity", "Invalid Quantity",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Parse the current text to a decimal
                if (!decimal.TryParse(QuantityTextBox.Text,
                                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                    CultureInfo.InvariantCulture,
                                    out decimal quantity))
                {
                    MessageBox.Show(this, "Please enter a valid number", "Invalid Quantity",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (quantity <= 0)
                {
                    MessageBox.Show(this, "Quantity must be greater than zero", "Invalid Quantity",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                NewQuantity = quantity;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // Get the current text and selection
            string text = textBox.Text;
            int selStart = textBox.SelectionStart;
            int selLength = textBox.SelectionLength;

            // Calculate what the text would be after this input
            string proposedText = text.Substring(0, selStart) +
                                e.Text +
                                text.Substring(selStart + selLength);

            // Special handling for decimal point/comma
            if (e.Text == "." || e.Text == ",")
            {
                // Don't allow multiple decimal points
                if (text.Contains(".") || text.Contains(","))
                {
                    e.Handled = true;
                    return;
                }

                // If typing decimal as first character, add a leading zero
                if (selStart == 0 && selLength == 0)
                {
                    _isUpdatingText = true;
                    textBox.Text = "0" + e.Text;
                    textBox.SelectionStart = 2;
                    _isUpdatingText = false;
                    e.Handled = true;
                    return;
                }

                // If replacing all text with just a decimal point, add a leading zero
                if (selLength == text.Length && selStart == 0)
                {
                    _isUpdatingText = true;
                    textBox.Text = "0" + e.Text;
                    textBox.SelectionStart = 2;
                    _isUpdatingText = false;
                    e.Handled = true;
                    return;
                }

                // Otherwise allow the decimal point
                return;
            }

            // For other characters, check if the result would be a valid decimal
            e.Handled = !decimal.TryParse(proposedText,
                                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                    CultureInfo.InvariantCulture,
                                    out _);
        }

        private void QuantityTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Allow navigation keys and editing keys
            if (e.Key == Key.Back || e.Key == Key.Delete ||
                e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Home || e.Key == Key.End ||
                e.Key == Key.Tab || e.Key == Key.Enter)
                return;

            // Allow digits from main keyboard and numpad
            if ((e.Key >= Key.D0 && e.Key <= Key.D9 && e.KeyboardDevice.Modifiers != ModifierKeys.Shift) ||
                (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
                return;

            // Allow decimal point/comma
            if (e.Key == Key.OemPeriod || e.Key == Key.OemComma || e.Key == Key.Decimal)
            {
                var textBox = sender as TextBox;
                if (textBox != null && !textBox.Text.Contains(".") && !textBox.Text.Contains(","))
                    return;
            }

            // Block all other keys
            e.Handled = true;
        }

        private void QuantityTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string pastedText = (string)e.DataObject.GetData(typeof(string));
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    string proposedText = textBox.Text.Substring(0, textBox.SelectionStart) +
                                         pastedText +
                                         textBox.Text.Substring(textBox.SelectionStart + textBox.SelectionLength);

                    // Validate the pasted text would result in a valid decimal
                    if (!decimal.TryParse(proposedText,
                                       NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                       CultureInfo.InvariantCulture,
                                       out _))
                    {
                        e.CancelCommand();
                    }
                }
                else
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }

        private void QuantityTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingText) return;

            var textBox = sender as TextBox;
            if (textBox == null || string.IsNullOrWhiteSpace(textBox.Text))
                return;

            try
            {
                if (decimal.TryParse(textBox.Text,
                                  NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                  CultureInfo.InvariantCulture,
                                  out decimal value))
                {
                    _isUpdatingText = true;
                    NewQuantity = value;
                    _isUpdatingText = false;
                }
            }
            catch
            {
                // Silent exception handling - we'll validate on OK button click
            }
        }
    }
}