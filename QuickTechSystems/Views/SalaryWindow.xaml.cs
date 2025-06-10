using System.Windows;
using System.Windows.Input;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    /// <summary>
    /// Interaction logic for SalaryWindow.xaml
    /// </summary>
    public partial class SalaryWindow : Window
    {
        public EmployeeViewModel ViewModel { get; private set; }

        /// <summary>
        /// Initialize a new instance of SalaryWindow
        /// </summary>
        /// <param name="viewModel">The EmployeeViewModel instance to use as DataContext</param>
        public SalaryWindow(EmployeeViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        /// <summary>
        /// Handles the Window KeyDown event for keyboard shortcuts like ESC
        /// </summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseButton_Click(sender, e);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the Close button click event
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}