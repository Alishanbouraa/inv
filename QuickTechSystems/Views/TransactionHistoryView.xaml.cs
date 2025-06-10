using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class TransactionHistoryView : UserControl
    {
        // Column indices for responsive visibility
        private const int TYPE_COLUMN_INDEX = 3;
        private const int ITEMS_COLUMN_INDEX = 4;
        private const int STATUS_COLUMN_INDEX = 6;
        private const int PAYMENT_METHOD_COLUMN_INDEX = 7;
        private const int CASHIER_COLUMN_INDEX = 8;
        private const int ROLE_COLUMN_INDEX = 9;

        public TransactionHistoryView()
        {
            InitializeComponent();
            this.Loaded += TransactionHistoryView_Loaded;
            this.SizeChanged += OnControlSizeChanged;
        }

        private void TransactionHistoryView_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure DataContext is set if using DI
            if (DataContext is TransactionHistoryViewModel viewModel)
            {
                // Any initialization if needed
            }

            AdjustLayoutForSize();
        }

        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustLayoutForSize();
        }

        private void AdjustLayoutForSize()
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow == null) return;

            // Get actual window dimensions
            double windowWidth = parentWindow.ActualWidth;
            double windowHeight = parentWindow.ActualHeight;

            // Automatically adjust the layout based on the window size
            // instead of using fixed breakpoints
            SetResponsiveLayout(windowWidth, windowHeight);
        }

        private void SetResponsiveLayout(double width, double height)
        {
            // Adaptive margin based on screen size
            double marginSize = CalculateAdaptiveMargin(width);
            ContentGrid.Margin = new Thickness(marginSize);

            // Determine which columns to show based on available width
            bool showTypeColumn = width >= 800;
            bool showStatusColumn = width >= 1000;
            bool showPaymentMethodColumn = width >= 1100;
            bool showCashierColumn = width >= 1200;
            bool showRoleColumn = width >= 1400;

            // Apply column visibility
            SetColumnVisibility(showTypeColumn, showStatusColumn, showPaymentMethodColumn, showCashierColumn, showRoleColumn);
        }

        private double CalculateAdaptiveMargin(double screenWidth)
        {
            // Calculate margin as a percentage of screen width
            double marginPercentage = 0.02; // 2% of screen width
            double calculatedMargin = screenWidth * marginPercentage;

            // Ensure margin stays within reasonable bounds
            return Math.Max(8, Math.Min(32, calculatedMargin));
        }

        private void SetColumnVisibility(bool showTypeColumn, bool showStatusColumn, bool showPaymentMethodColumn, bool showCashierColumn, bool showRoleColumn)
        {
            if (TransactionsDataGrid == null || TransactionsDataGrid.Columns.Count == 0)
                return;

            // Safely check if we have enough columns
            if (TransactionsDataGrid.Columns.Count > TYPE_COLUMN_INDEX)
                TransactionsDataGrid.Columns[TYPE_COLUMN_INDEX].Visibility = showTypeColumn ? Visibility.Visible : Visibility.Collapsed;

            if (TransactionsDataGrid.Columns.Count > STATUS_COLUMN_INDEX)
                TransactionsDataGrid.Columns[STATUS_COLUMN_INDEX].Visibility = showStatusColumn ? Visibility.Visible : Visibility.Collapsed;

            if (TransactionsDataGrid.Columns.Count > PAYMENT_METHOD_COLUMN_INDEX)
                TransactionsDataGrid.Columns[PAYMENT_METHOD_COLUMN_INDEX].Visibility = showPaymentMethodColumn ? Visibility.Visible : Visibility.Collapsed;

            if (TransactionsDataGrid.Columns.Count > CASHIER_COLUMN_INDEX)
                TransactionsDataGrid.Columns[CASHIER_COLUMN_INDEX].Visibility = showCashierColumn ? Visibility.Visible : Visibility.Collapsed;

            if (TransactionsDataGrid.Columns.Count > ROLE_COLUMN_INDEX)
                TransactionsDataGrid.Columns[ROLE_COLUMN_INDEX].Visibility = showRoleColumn ? Visibility.Visible : Visibility.Collapsed;

            // Items column visibility based on other columns
            if (TransactionsDataGrid.Columns.Count > ITEMS_COLUMN_INDEX)
                TransactionsDataGrid.Columns[ITEMS_COLUMN_INDEX].Visibility = (showTypeColumn && showStatusColumn)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && DataContext is TransactionHistoryViewModel viewModel)
            {
                viewModel.SearchText = textBox.Text;
            }
        }

        // Corrected pagination converter class
        public class PaginationConverters : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                if (value is int currentPage)
                {
                    // Get the view model directly from the binding source
                    if (parameter is string param)
                    {
                        // This will be a relative binding, so we'll need to get the parent control
                        var element = (parameter as FrameworkElement)?.DataContext as TransactionHistoryViewModel
                                   ?? (value as FrameworkElement)?.DataContext as TransactionHistoryViewModel;

                        // Since we can't reliably get the ViewModel from the control hierarchy,
                        // we'll just handle simple cases without relying on ViewModel properties
                        switch (param)
                        {
                            case "prev":
                                return currentPage > 1; // Enable "Previous" if not on first page

                            case "next":
                                // We don't have access to TotalPages here
                                // Use a simple check - assume we're not on last page if page > 0
                                return currentPage > 0;

                            case "info":
                                // Return simple page info
                                return $"Page {currentPage}";
                        }
                    }
                }
                return value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}