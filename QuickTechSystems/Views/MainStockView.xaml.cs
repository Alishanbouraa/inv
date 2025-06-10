// Path: QuickTechSystems.WPF.Views/MainStockView.xaml.cs
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Interfaces;
using QuickTechSystems.Application.Services;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class MainStockView : UserControl
    {
        private readonly IMainStockService _mainStockService;
        private readonly ICategoryService _categoryService;
        private readonly ISupplierService _supplierService;
        private readonly ISupplierInvoiceService _supplierInvoiceService;
        private readonly IBarcodeService _barcodeService;
        private readonly IImagePathService _imagePathService;
        private readonly IProductService _productService;
        private readonly IEventAggregator _eventAggregator;

        public MainStockView()
        {
            InitializeComponent();
            this.Loaded += MainStockView_Loaded;
            this.SizeChanged += OnControlSizeChanged;

            // Get services from DI container
            var app = (App)System.Windows.Application.Current;
            _mainStockService = app.ServiceProvider.GetService<IMainStockService>();
            _categoryService = app.ServiceProvider.GetService<ICategoryService>();
            _supplierService = app.ServiceProvider.GetService<ISupplierService>();
            _supplierInvoiceService = app.ServiceProvider.GetService<ISupplierInvoiceService>();
            _barcodeService = app.ServiceProvider.GetService<IBarcodeService>();
            _imagePathService = app.ServiceProvider.GetService<IImagePathService>();
            _eventAggregator = app.ServiceProvider.GetService<IEventAggregator>();
        }

        private void MainStockView_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure DataContext is set properly
            if (DataContext != null)
            {
                // Force a data refresh when the view is loaded
                if (DataContext is MainStockViewModel viewModel)
                {
                    // Use a slight delay to ensure the UI is ready
                    Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        await Task.Delay(100); // Small delay for UI to settle

                        // Publish a global refresh event
                        viewModel.ForceRefreshCommand?.Execute(null);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }

            // Adjust layout based on size
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
            // Get content grid
            if (ContentGrid == null) return;

            // Adaptive margin based on screen size
            double marginSize = CalculateAdaptiveMargin(width);
            ContentGrid.Margin = new Thickness(marginSize);
        }

        private double CalculateAdaptiveMargin(double screenWidth)
        {
            // Calculate margin as a percentage of screen width
            double marginPercentage = 0.02; // 2% of screen width
            double calculatedMargin = screenWidth * marginPercentage;

            // Ensure margin stays within reasonable bounds
            return Math.Max(8, Math.Min(32, calculatedMargin));
        }

        private async void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid &&
                grid.SelectedItem is MainStockDTO item)
            {
                await ShowEditDialog(item);
            }
        }

        private async void NewEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is MainStockDTO item)
            {
                await ShowEditDialog(item);
            }
        }

        private async void NewEditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainStockViewModel viewModel &&
                viewModel.SelectedItem != null)
            {
                await ShowEditDialog(viewModel.SelectedItem);
            }
        }

        // Path: QuickTechSystems.WPF.Views/MainStockView.xaml.cs (updated ShowEditDialog method)
        private async Task ShowEditDialog(MainStockDTO item)
        {
            try
            {
                var productService = ((App)System.Windows.Application.Current).ServiceProvider.GetService<IProductService>();

                var editViewModel = new EditMainStockViewModel(
                    _mainStockService,
                    _categoryService,
                    _supplierService,
                    _supplierInvoiceService,
                    _barcodeService,
                    _imagePathService,
                    productService,
                    _eventAggregator);

                // Await initialization instead of using Wait()
                await editViewModel.InitializeAsync(item);

                var editDialog = new EditMainStockDialog
                {
                    DataContext = editViewModel,
                    Owner = Window.GetWindow(this)
                };

                var result = editDialog.ShowDialog();

                if (result == true)
                {
                    // Refresh the view after successful edit
                    if (DataContext is MainStockViewModel mainViewModel)
                    {
                        mainViewModel.LoadCommand.Execute(null);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening edit dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseTransferPopup_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainStockViewModel viewModel)
            {
                viewModel.IsTransferPopupOpen = false;
            }
        }
    }
}