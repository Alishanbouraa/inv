using System.Windows;
using System.Windows.Input;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    public partial class CustomerDetailsWindow : Window
    {
        public CustomerViewModel ViewModel { get; private set; }

        public CustomerDetailsWindow(CustomerViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseWindow();
                e.Handled = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SaveCommand.CanExecute(null))
            {
                ViewModel.SaveCommand.Execute(null);
                this.DialogResult = true;
                this.Close();
            }
        }

        private void CloseWindow()
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}