using System;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class SupplierInvoicePaymentHistoryWindow : Window
    {
        private readonly SupplierInvoiceViewModel _viewModel;

        public SupplierInvoicePaymentHistoryWindow(SupplierInvoiceViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;
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
    }
}