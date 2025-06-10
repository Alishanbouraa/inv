using System;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class SupplierInvoiceCreateWindow : Window
    {
        private readonly SupplierInvoiceViewModel _viewModel;
        private bool _resultSaved = false;

        public SupplierInvoiceCreateWindow(SupplierInvoiceViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            Loaded += SupplierInvoiceCreateWindow_Loaded;
        }

        private void SupplierInvoiceCreateWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Focus the invoice number text box
            InvoiceNumberTextBox.Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = _resultSaved;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = _resultSaved;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // The SaveInvoiceCommand will be executed through binding
            // Just set the result and close when Save is clicked
            _resultSaved = true;
            DialogResult = true;
            Close();
        }
    }
}