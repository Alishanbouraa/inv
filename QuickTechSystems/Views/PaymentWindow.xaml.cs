using System.Windows;
using System.Windows.Input;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class PaymentWindow : Window
    {
        public CustomerViewModel ViewModel { get; private set; }

        public PaymentWindow(CustomerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
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
            if (ViewModel.ClosePaymentDialogCommand.CanExecute(null))
            {
                ViewModel.ClosePaymentDialogCommand.Execute(null);
            }
            this.Close();
        }

        private void ProcessPayment_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ProcessPaymentCommand.CanExecute(null))
            {
                ViewModel.ProcessPaymentCommand.Execute(null);

                // Check if processing is complete after a short delay
                System.Threading.Tasks.Task.Delay(500).ContinueWith(t =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Close the window after processing is complete
                        if (!ViewModel.IsSaving)
                        {
                            this.Close();
                        }
                    });
                });
            }
        }
    }
}