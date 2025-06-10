using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using QuickTechSystems.ViewModels;
using QuickTechSystems.WPF;

namespace QuickTechSystems.Views
{
    public partial class LoginView : Window
    {
        public LoginView()
        {
            InitializeComponent();
            DataContext = ((App)System.Windows.Application.Current).ServiceProvider.GetService<LoginViewModel>();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}