using System;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class SupplierPaymentWindow : Window
    {
        private readonly SupplierViewModel _viewModel;
        private bool _resultSaved = false;

        public SupplierPaymentWindow(SupplierViewModel viewModel, SupplierDTO supplier)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // Set the supplier if provided
            if (supplier != null)
            {
                _viewModel.SelectedSupplier = supplier;
            }

            // Reset payment values
            _viewModel.PaymentAmount = 0;
            _viewModel.Notes = string.Empty;

            Loaded += SupplierPaymentWindow_Loaded;
        }

        private void SupplierPaymentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Focus the payment amount text box when loaded
            PaymentAmountTextBox.Focus();
            PaymentAmountTextBox.SelectAll();
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
            DialogResult = _resultSaved;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Execute save command through the view model
            if (_viewModel.AddPaymentCommand.CanExecute(null))
            {
                _viewModel.AddPaymentCommand.Execute(null);
                _resultSaved = true;
                DialogResult = true;
                Close();
            }
        }
    }
}