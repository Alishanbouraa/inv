using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class CustomerView : UserControl
    {
        public CustomerView()
        {
            InitializeComponent();
            this.Loaded += CustomerView_Loaded;
            this.SizeChanged += OnControlSizeChanged;
        }

        private void CustomerView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
            }

            AdjustLayoutForSize();
        }


        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustLayoutForSize();
        }
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var viewModel = DataContext as CustomerViewModel;
                var customer = e.Row.Item as CustomerDTO;

                if (viewModel != null && customer != null)
                {
                    // Start the update process without awaiting to avoid UI freezing
                    _ = viewModel.UpdateCustomerDirectEdit(customer);
                }
            }
        }
        private void AdjustLayoutForSize()
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow == null) return;

            double windowWidth = parentWindow.ActualWidth;

            // Adjust margins based on window width
            if (windowWidth >= 1920)
            {
                ContentGrid.Margin = new Thickness(32);
            }
            else if (windowWidth >= 1366)
            {
                ContentGrid.Margin = new Thickness(24);
            }
            else if (windowWidth >= 800)
            {
                ContentGrid.Margin = new Thickness(16);
            }
            else
            {
                ContentGrid.Margin = new Thickness(8);
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is CustomerDTO customer &&
                DataContext is CustomerViewModel viewModel)
            {
                viewModel.EditCustomer(customer);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is CustomerDTO customer &&
                DataContext is CustomerViewModel viewModel)
            {
                viewModel.EditCustomer(customer);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is CustomerDTO customer &&
                DataContext is CustomerViewModel viewModel)
            {
                viewModel.SelectedCustomer = customer;
                if (viewModel.DeleteCommand.CanExecute(null))
                {
                    viewModel.DeleteCommand.Execute(null);
                }
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CustomerViewModel viewModel &&
                viewModel.SelectedCustomer != null)
            {
                viewModel.EditCustomer(viewModel.SelectedCustomer);
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CustomerViewModel viewModel &&
                viewModel.SelectedCustomer != null &&
                viewModel.DeleteCommand.CanExecute(null))
            {
                viewModel.DeleteCommand.Execute(null);
            }
        }

        private void ResetPrice_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is CustomerProductPriceViewModel priceModel)
            {
                priceModel.CustomPrice = priceModel.DefaultPrice;
            }
        }
    }
}