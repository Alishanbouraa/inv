using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Services
{
    public static class InputDialogService
    {
        public static bool TryGetDecimalInput(string title, string productName, decimal initialValue, out decimal result, Window owner = null)
        {
            result = 0;

            var window = new Window
            {
                Title = title,
                Width = 300,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            var mainGrid = new Grid { Margin = new Thickness(10) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Product name
            var productNameText = new TextBlock
            {
                Text = productName,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(productNameText, 0);
            mainGrid.Children.Add(productNameText);

            // Input label
            var inputLabel = new TextBlock
            {
                Text = title.Contains("Quantity") ? "Enter quantity:" : "Enter price:",
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(inputLabel, 1);
            mainGrid.Children.Add(inputLabel);

            // Input field
            var inputTextBox = new TextBox
            {
                Text = initialValue.ToString(),
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(5),
                FontSize = 16,
                HorizontalContentAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(inputTextBox, 2);
            mainGrid.Children.Add(inputTextBox);

            // Select all text when focused
            inputTextBox.GotFocus += (s, e) => inputTextBox.SelectAll();

            // Focus on the text box when the dialog is shown
            window.Loaded += (s, e) =>
            {
                inputTextBox.Focus();
                inputTextBox.SelectAll();
            };

            // Action buttons
            var buttonsGrid = new Grid();
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(buttonsGrid, 3);
            mainGrid.Children.Add(buttonsGrid);

            // Cancel button
            var cancelButton = new Button
            {
                Content = "Cancel",
                Margin = new Thickness(3, 0, 3, 0),
                Padding = new Thickness(0, 10, 0, 10),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")),
                Foreground = Brushes.White
            };
            Grid.SetColumn(cancelButton, 0);
            buttonsGrid.Children.Add(cancelButton);
            cancelButton.Click += (s, e) => { window.DialogResult = false; };

            // OK button
            var okButton = new Button
            {
                Content = "OK",
                Margin = new Thickness(3, 0, 3, 0),
                Padding = new Thickness(0, 10, 0, 10),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                Foreground = Brushes.White
            };
            Grid.SetColumn(okButton, 1);
            buttonsGrid.Children.Add(okButton);
            okButton.Click += (s, e) =>
            {
                if (decimal.TryParse(inputTextBox.Text, out decimal value) && value > 0)
                {
                    window.DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Please enter a valid number greater than zero.", "Invalid Input",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            window.Content = mainGrid;

            // Show dialog and process result
            if (window.ShowDialog() == true)
            {
                result = decimal.Parse(inputTextBox.Text);
                return true;
            }

            return false;
        }
    }
}