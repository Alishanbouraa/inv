using System;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class SupplierTransactionsHistoryWindow : Window
    {
        private readonly SupplierViewModel _viewModel;

        public SupplierTransactionsHistoryWindow(SupplierViewModel viewModel, SupplierDTO supplier)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // Set the supplier if provided
            if (supplier != null)
            {
                _viewModel.SelectedSupplier = supplier;
                // Load transactions for this supplier
                _viewModel.LoadSupplierTransactionsAsync().ConfigureAwait(false);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Remove the AddPaymentButton_Click method
    }
}