using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.ViewModels;
using QuickTechSystems.WPF.ViewModels;
using QuickTechSystems.WPF;

namespace QuickTechSystems.WPF.Views
{
    public partial class SupplierView : UserControl
    {
        private SupplierInvoiceViewModel _supplierInvoiceViewModel;
        private SupplierViewModel _supplierViewModel => DataContext as SupplierViewModel;

        public SupplierView()
        {
            InitializeComponent();
            this.Loaded += OnControlLoaded;
            this.SizeChanged += OnControlSizeChanged;
            this.Unloaded += OnControlUnloaded;

            // Get the SupplierInvoiceViewModel from the service provider
            _supplierInvoiceViewModel = ((App)System.Windows.Application.Current).ServiceProvider.GetRequiredService<SupplierInvoiceViewModel>();

            // Set the DataContext for the SupplierInvoiceView
            SupplierInvoiceView.DataContext = _supplierInvoiceViewModel;
        }

        private async void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            AdjustLayoutForSize();

            // Initialize data when the view is loaded
            if (_supplierViewModel != null)
            {
                await _supplierViewModel.InitializeAsync();
            }
        }

        private void OnControlUnloaded(object sender, RoutedEventArgs e)
        {
            // Clean up resources when the view is unloaded
            if (_supplierViewModel != null && _supplierViewModel is IDisposable disposable)
            {
                disposable.Dispose();
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
            var scrollViewer = MainGrid.Children[0] as ScrollViewer;
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
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is SupplierDTO supplier &&
                DataContext is SupplierViewModel viewModel)
            {
                var window = new SupplierDetailsWindow(viewModel, supplier, false);
                window.ShowDialog();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is SupplierDTO supplier &&
                DataContext is SupplierViewModel viewModel)
            {
                var window = new SupplierDetailsWindow(viewModel, supplier, false);
                window.ShowDialog();
            }
        }
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is SupplierDTO supplier &&
                DataContext is SupplierViewModel viewModel)
            {
                viewModel.SelectedSupplier = supplier;
                if (viewModel.DeleteCommand.CanExecute(null))
                {
                    viewModel.DeleteCommand.Execute(null);
                }
            }
        }

        private void AddTransactionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is SupplierDTO supplier &&
                DataContext is SupplierViewModel viewModel)
            {
                viewModel.SelectedSupplier = supplier;
                var window = new SupplierPaymentWindow(viewModel, supplier);
                window.ShowDialog();
            }
        }

        private void ViewHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is SupplierDTO supplier &&
                DataContext is SupplierViewModel viewModel)
            {
                viewModel.SelectedSupplier = supplier;

                // Load transactions first, then show history window
                Task.Run(async () => {
                    await viewModel.LoadSupplierTransactionsAsync();

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                        var window = new SupplierTransactionsHistoryWindow(viewModel, supplier);
                        window.ShowDialog();
                    });
                });
            }
        }

        // Context menu handlers
        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SupplierViewModel viewModel &&
                viewModel.SelectedSupplier != null)
            {
                var window = new SupplierDetailsWindow(viewModel, viewModel.SelectedSupplier, false);
                window.ShowDialog();
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SupplierViewModel viewModel &&
                viewModel.SelectedSupplier != null &&
                viewModel.DeleteCommand.CanExecute(null))
            {
                viewModel.DeleteCommand.Execute(null);
            }
        }

        private void AddTransactionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SupplierViewModel viewModel &&
                viewModel.SelectedSupplier != null)
            {
                var window = new SupplierPaymentWindow(viewModel, viewModel.SelectedSupplier);
                window.ShowDialog();
            }
        }

        private void ViewTransactionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SupplierViewModel viewModel &&
                viewModel.SelectedSupplier != null)
            {
                // Load transactions first, then show history window
                Task.Run(async () => {
                    await viewModel.LoadSupplierTransactionsAsync();

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                        var window = new SupplierTransactionsHistoryWindow(viewModel, viewModel.SelectedSupplier);
                        window.ShowDialog();
                    });
                });
            }
        }
    }
}