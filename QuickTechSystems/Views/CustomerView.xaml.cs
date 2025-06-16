using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class CustomerView : UserControl
    {
        private CustomerViewModel ViewModel => DataContext as CustomerViewModel;

        public CustomerView()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
            this.SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AdjustLayoutForWindowSize();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustLayoutForWindowSize();
        }

        private void AdjustLayoutForWindowSize()
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow?.ActualWidth == null) return;

            var windowWidth = parentWindow.ActualWidth;
            var margin = windowWidth switch
            {
                >= 1920 => new Thickness(32),
                >= 1366 => new Thickness(24),
                >= 800 => new Thickness(16),
                _ => new Thickness(8)
            };

            ContentPanel.Margin = margin;
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit &&
                e.Row.Item is CustomerDTO customer &&
                ViewModel != null)
            {
                _ = ViewModel.UpdateCustomerDirectEdit(customer);
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid &&
                grid.SelectedItem is CustomerDTO customer &&
                ViewModel != null)
            {
                ViewModel.EditCustomer(customer);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (GetCustomerFromSender(sender) is CustomerDTO customer)
            {
                ViewModel?.EditCustomer(customer);
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedCustomer != null)
            {
                ViewModel.EditCustomer(ViewModel.SelectedCustomer);
            }
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedCustomer != null && ViewModel.DeleteCommand.CanExecute(null))
            {
                ViewModel.DeleteCommand.Execute(null);
            }
        }

        private CustomerDTO GetCustomerFromSender(object sender)
        {
            return sender is Button button ? button.DataContext as CustomerDTO : null;
        }
    }
}