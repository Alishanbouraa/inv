using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class CategoryView : UserControl
    {
        public CategoryView()
        {
            InitializeComponent();

            // Register to the Loaded event to adjust layout based on container size
            this.Loaded += OnControlLoaded;
            this.SizeChanged += OnControlSizeChanged;

            // Add handler for DataContext changes to bind to property changes
            this.DataContextChanged += CategoryView_DataContextChanged;
        }

        private void CategoryView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CategoryViewModel oldViewModel)
            {
                // Unsubscribe from old view model
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is CategoryViewModel newViewModel)
            {
                // Subscribe to new view model
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Handle any remaining property change notifications if needed
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            AdjustLayoutForSize();

            // Subscribe to view model property changes
            if (DataContext is CategoryViewModel viewModel)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
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

            // Set margins and paddings based on window size
            var rootGrid = this.Content as Grid;
            if (rootGrid == null) return;

            var scrollViewer = rootGrid.Children[1] as ScrollViewer;
            if (scrollViewer == null) return;

            var contentGrid = scrollViewer.Content as Grid;
            if (contentGrid == null) return;

            if (windowWidth >= 1920) // Large screens
            {
                contentGrid.Margin = new Thickness(32);
            }
            else if (windowWidth >= 1366) // Medium screens
            {
                contentGrid.Margin = new Thickness(24);
            }
            else if (windowWidth >= 800) // Small screens
            {
                contentGrid.Margin = new Thickness(16);
            }
            else // Very small screens
            {
                contentGrid.Margin = new Thickness(8);
            }
        }

        // Event handlers for DataGrid
        private void ProductDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is CategoryDTO category &&
                DataContext is CategoryViewModel viewModel)
            {
                viewModel.EditProductCategory(category);
            }
        }

        private void ExpenseDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is CategoryDTO category &&
                DataContext is CategoryViewModel viewModel)
            {
                viewModel.EditExpenseCategory(category);
            }
        }

        private void EditProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is CategoryDTO category &&
                DataContext is CategoryViewModel viewModel)
            {
                viewModel.EditProductCategory(category);
            }
        }

        private void EditExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is CategoryDTO category &&
                DataContext is CategoryViewModel viewModel)
            {
                viewModel.EditExpenseCategory(category);
            }
        }
    }
}