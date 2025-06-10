using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class PaymentHistoryWindow : Window
    {
        public CustomerViewModel ViewModel { get; private set; }

        public PaymentHistoryWindow(CustomerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;

            // Don't set IsPaymentHistoryVisible - we don't want popup
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // We can still apply the filter or refresh data via command if needed
            // But don't use ClosePaymentHistoryCommand as it affects the popup

            // Simply close the window
            this.Close();
        }

        private void EditPayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is TransactionDTO transaction &&
                DataContext is CustomerViewModel viewModel)
            {
                viewModel.EditPayment(transaction);
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);

            // Don't set IsPaymentHistoryVisible
        }
    }
}