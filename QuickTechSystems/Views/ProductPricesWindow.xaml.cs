using System;
using System.Windows;
using System.ComponentModel;
using QuickTechSystems.WPF.ViewModels;
using System.Diagnostics;

namespace QuickTechSystems.WPF
{
    /// <summary>
    /// Interaction logic for ProductPricesWindow.xaml
    /// </summary>
    public partial class ProductPricesWindow : Window
    {
        private readonly CustomerViewModel _viewModel;
        private bool _isSaving = false;
        private bool _saveCompleted = false;

        public ProductPricesWindow(CustomerViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel;

            // Subscribe to PropertyChanged to detect when saving is complete
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Handle window closing to ensure we unsubscribe
            this.Closing += ProductPricesWindow_Closing;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Ensure window is maximized when loaded
            this.WindowState = WindowState.Maximized;

            // Set initial focus to the search box
            var searchBox = this.FindName("SearchBox") as UIElement;
            if (searchBox != null)
            {
                searchBox.Focus();
            }

            // Reset any search text from previous sessions
            _viewModel.ProductSearchText = string.Empty;
        }

        private void ProductPricesWindow_Closing(object sender, CancelEventArgs e)
        {
            // Clean up event subscriptions
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Monitor both IsSaving and IsProductPricesDialogOpen properties
            if (e.PropertyName == nameof(_viewModel.IsSaving))
            {
                // If we were saving and now we're not, check if save completed
                if (_isSaving && !_viewModel.IsSaving)
                {
                    _isSaving = false;

                    // If the dialog flag was set to false, consider it a successful save
                    if (!_viewModel.IsProductPricesDialogOpen && !_saveCompleted)
                    {
                        _saveCompleted = true;
                        Dispatcher.InvokeAsync(() =>
                        {
                            DialogResult = true;
                            Close();
                        });
                    }
                }
                else if (!_isSaving && _viewModel.IsSaving)
                {
                    // Keep track that we started saving
                    _isSaving = true;
                }
            }
            else if (e.PropertyName == nameof(_viewModel.IsProductPricesDialogOpen))
            {
                // If dialog flag changes to false while or after saving, consider it a save completion
                if (!_viewModel.IsProductPricesDialogOpen && _isSaving && !_saveCompleted)
                {
                    _saveCompleted = true;
                    Dispatcher.InvokeAsync(() =>
                    {
                        DialogResult = true;
                        Close();
                    });
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Set dialog result and close the window
            DialogResult = false;
            Close();
        }
    }
}