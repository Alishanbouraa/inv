using System;
using System.Windows;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class SupplierDetailsWindow : Window
    {
        private readonly SupplierViewModel _viewModel;
        private bool _resultSaved = false;

        public SupplierDetailsWindow(SupplierViewModel viewModel, SupplierDTO supplier = null, bool isNew = false)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // Set the supplier if provided
            if (supplier != null)
            {
                _viewModel.SelectedSupplier = supplier;
            }
            _viewModel.IsNewSupplier = isNew;
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
            if (_viewModel.SaveCommand.CanExecute(null))
            {
                _viewModel.SaveCommand.Execute(null);
                _resultSaved = true;
                DialogResult = true;
                Close();
            }
        }
    }
}