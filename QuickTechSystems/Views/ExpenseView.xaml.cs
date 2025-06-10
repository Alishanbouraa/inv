using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class ExpenseView : UserControl
    {
        private ExpenseWindow? _expenseWindow;

        public ExpenseView()
        {
            InitializeComponent();
            this.Loaded += ExpenseView_Loaded;
            this.SizeChanged += OnControlSizeChanged;
        }

        private void ExpenseView_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure DataContext is set properly
            if (DataContext != null)
            {
                // Any initialization needed
                if (DataContext is ExpenseViewModel viewModel)
                {
                    // Subscribe to the property changed event to handle window opening
                    viewModel.PropertyChanged += ViewModel_PropertyChanged;
                }
            }

            // Adjust layout based on size
            AdjustLayoutForSize();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExpenseViewModel.IsExpensePopupOpen) &&
                DataContext is ExpenseViewModel viewModel)
            {
                if (viewModel.IsExpensePopupOpen)
                {
                    OpenExpenseWindow(viewModel);
                }
            }
        }

        private void OpenExpenseWindow(ExpenseViewModel viewModel)
        {
            // Close existing window if open
            _expenseWindow?.Close();

            // Create and show new window
            _expenseWindow = new ExpenseWindow(viewModel);
            _expenseWindow.Closed += (s, e) =>
            {
                viewModel.IsExpensePopupOpen = false;
                _expenseWindow = null;
            };

            _expenseWindow.Show();
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
            var scrollViewer = this.Content as Grid;
            if (scrollViewer == null) return;

            var rootGrid = scrollViewer.Children[0] as ScrollViewer;
            if (rootGrid == null) return;

            var contentGrid = rootGrid.Content as Grid;
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

        // DataGrid event handlers
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is ExpenseDTO expense &&
                DataContext is ExpenseViewModel viewModel)
            {
                viewModel.EditCommand.Execute(expense);
            }
        }

        // Context menu event handlers
        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ExpenseViewModel viewModel &&
                viewModel.SelectedExpense != null)
            {
                viewModel.EditCommand.Execute(viewModel.SelectedExpense);
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ExpenseViewModel viewModel &&
                viewModel.SelectedExpense != null)
            {
                viewModel.DeleteCommand.Execute(viewModel.SelectedExpense);
            }
        }
    }
}