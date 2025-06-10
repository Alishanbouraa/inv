using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class BulkMainStockDialog : Window
    {
        private static readonly Regex _decimalRegex = new Regex(@"^[0-9]*(?:\.[0-9]*)?$");
        private static readonly Regex _integerRegex = new Regex(@"^[0-9]*$");

        public BulkMainStockDialog()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                if (sender is TextBox textBox &&
                    DataContext is BulkMainStockViewModel viewModel &&
                    textBox.DataContext is MainStockDTO item)
                {
                    viewModel.LookupProductCommand.Execute(item);
                    e.Handled = true;
                }
            }
        }

        private void BoxBarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                if (sender is TextBox textBox &&
                    DataContext is BulkMainStockViewModel viewModel &&
                    textBox.DataContext is MainStockDTO item)
                {
                    viewModel.LookupBoxBarcodeCommand.Execute(item);
                    e.Handled = true;
                }
            }
        }

        private void Barcode_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox &&
                DataContext is BulkMainStockViewModel viewModel &&
                textBox.DataContext is MainStockDTO item)
            {
                viewModel.ValidateItemCommand.Execute(item);
            }
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image image && image.Source is BitmapImage bitmapImage)
            {
                try
                {
                    var previewImage = new BitmapImage();
                    previewImage.BeginInit();
                    previewImage.UriSource = bitmapImage.UriSource;
                    previewImage.CacheOption = BitmapCacheOption.OnLoad;
                    previewImage.EndInit();

                    var previewWindow = new Window
                    {
                        Title = "Image Preview",
                        Owner = this,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Width = 500,
                        Height = 500,
                        ResizeMode = ResizeMode.CanResize,
                        Content = new ScrollViewer
                        {
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Content = new Image
                            {
                                Source = previewImage,
                                Stretch = System.Windows.Media.Stretch.None
                            }
                        }
                    };

                    previewWindow.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error showing image preview: {ex.Message}",
                        "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !_decimalRegex.IsMatch(newText);
        }

        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            var newText = textBox.Text.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !_integerRegex.IsMatch(newText);
        }

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

        private void PriceTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Text == "0.00" || textBox.Text == "0,00")
                {
                    textBox.Text = string.Empty;
                }
                textBox.SelectAll();
            }
        }

        private void PriceTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.Text = "0.00";
                        return;
                    }

                    if (decimal.TryParse(textBox.Text.Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal value))
                    {
                        textBox.Text = value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                            .Replace(",", ".");
                    }
                    else
                    {
                        textBox.Text = "0.00";
                    }
                }
                catch
                {
                    textBox.Text = "0.00";
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (DataContext is BulkMainStockViewModel viewModel)
            {
                viewModel.Dispose();
            }
        }
    }
}