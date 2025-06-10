using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QuickTechSystems.WPF.Views
{
    public partial class ProfitView : UserControl
    {
        // Column indices for responsive visibility
        private const int DATE_COLUMN_INDEX = 0;
        private const int TIME_COLUMN_INDEX = 1;
        private const int SALES_COLUMN_INDEX = 2;
        private const int COST_COLUMN_INDEX = 3;
        private const int GROSS_PROFIT_COLUMN_INDEX = 4;
        private const int NET_PROFIT_COLUMN_INDEX = 5;
        private const int MARGIN_COLUMN_INDEX = 6;
        private const int ITEMS_COLUMN_INDEX = 7;

        public ProfitView()
        {
            InitializeComponent();
            this.Loaded += ProfitView_Loaded;
            this.SizeChanged += OnControlSizeChanged;
        }

        private void ProfitView_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure DataContext is set if using DI
            if (DataContext != null)
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
            SetResponsiveLayout(windowWidth, windowHeight);
        }

        private void SetResponsiveLayout(double width, double height)
        {
            // Adaptive margin based on screen size
            double marginSize = CalculateAdaptiveMargin(width);

            // Find ContentGrid by name if it exists
            var contentGrid = this.FindName("ContentGrid") as Grid;
            if (contentGrid != null)
            {
                contentGrid.Margin = new Thickness(marginSize);
            }

            // Determine which data grid columns to show based on available width
            bool showDateColumn = true; // Always show
            bool showTimeColumn = width >= 600;
            bool showSalesColumn = true; // Always show
            bool showCostColumn = width >= 800;
            bool showGrossProfitColumn = true; // Always show
            bool showNetProfitColumn = width >= 900;
            bool showMarginColumn = width >= 1000;
            bool showItemsColumn = width >= 1100;

            // Apply column visibility
            SetColumnVisibility(showDateColumn, showTimeColumn, showSalesColumn, showCostColumn,
                               showGrossProfitColumn, showNetProfitColumn, showMarginColumn, showItemsColumn);
        }

        private double CalculateAdaptiveMargin(double screenWidth)
        {
            // Calculate margin as a percentage of screen width
            double marginPercentage = 0.02; // 2% of screen width
            double calculatedMargin = screenWidth * marginPercentage;

            // Ensure margin stays within reasonable bounds
            return Math.Max(8, Math.Min(32, calculatedMargin));
        }

        private void SetColumnVisibility(bool showDateColumn, bool showTimeColumn, bool showSalesColumn,
                                      bool showCostColumn, bool showGrossProfitColumn, bool showNetProfitColumn,
                                      bool showMarginColumn, bool showItemsColumn)
        {
            // Find DataGrid by name if reference not available
            var profitDetailsDataGrid = this.FindName("ProfitDetailsDataGrid") as DataGrid;
            if (profitDetailsDataGrid == null || profitDetailsDataGrid.Columns.Count == 0)
                return;

            // Safely check if we have enough columns
            if (profitDetailsDataGrid.Columns.Count > DATE_COLUMN_INDEX)
                profitDetailsDataGrid.Columns[DATE_COLUMN_INDEX].Visibility =
                    showDateColumn ? Visibility.Visible : Visibility.Collapsed;

            if (profitDetailsDataGrid.Columns.Count > TIME_COLUMN_INDEX)
                profitDetailsDataGrid.Columns[TIME_COLUMN_INDEX].Visibility =
                    showTimeColumn ? Visibility.Visible : Visibility.Collapsed;

            if (profitDetailsDataGrid.Columns.Count > SALES_COLUMN_INDEX)
                profitDetailsDataGrid.Columns[SALES_COLUMN_INDEX].Visibility =
                    showSalesColumn ? Visibility.Visible : Visibility.Collapsed;

            if (profitDetailsDataGrid.Columns.Count > COST_COLUMN_INDEX)
                profitDetailsDataGrid.Columns[COST_COLUMN_INDEX].Visibility =
                    showCostColumn ? Visibility.Visible : Visibility.Collapsed;

            if (profitDetailsDataGrid.Columns.Count > GROSS_PROFIT_COLUMN_INDEX)
                profitDetailsDataGrid.Columns[GROSS_PROFIT_COLUMN_INDEX].Visibility =
                    showGrossProfitColumn ? Visibility.Visible : Visibility.Collapsed;

            if (profitDetailsDataGrid.Columns.Count > NET_PROFIT_COLUMN_INDEX)
                profitDetailsDataGrid.Columns[NET_PROFIT_COLUMN_INDEX].Visibility =
                    showNetProfitColumn ? Visibility.Visible : Visibility.Collapsed;

            if (profitDetailsDataGrid.Columns.Count > MARGIN_COLUMN_INDEX)
                profitDetailsDataGrid.Columns[MARGIN_COLUMN_INDEX].Visibility =
                    showMarginColumn ? Visibility.Visible : Visibility.Collapsed;

            if (profitDetailsDataGrid.Columns.Count > ITEMS_COLUMN_INDEX)
                profitDetailsDataGrid.Columns[ITEMS_COLUMN_INDEX].Visibility =
                    showItemsColumn ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SummaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProfitViewModel viewModel)
            {
                var summaryWindow = new ProfitSummaryWindow(viewModel);
                summaryWindow.Owner = Window.GetWindow(this);
                summaryWindow.ShowDialog();
            }
        }

        // Helper method to find a named element in the visual tree
        private T FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            // Create a childCount to prevent infinite recursion if visual tree is malformed
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                // If this is the child we're looking for, return it
                if (child is FrameworkElement element && element.Name == childName && child is T typedChild)
                {
                    return typedChild;
                }

                // Otherwise, recurse into its children
                var result = FindVisualChild<T>(child, childName);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }
}