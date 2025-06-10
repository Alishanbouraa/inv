using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    /// <summary>
    /// Interaction logic for EmployeeWindow.xaml
    /// </summary>
    public partial class EmployeeWindow : Window
    {
        public EmployeeViewModel ViewModel { get; private set; }

        /// <summary>
        /// Initialize a new instance of EmployeeWindow
        /// </summary>
        /// <param name="viewModel">The EmployeeViewModel instance to use as DataContext</param>
        public EmployeeWindow(EmployeeViewModel viewModel)
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

        /// <summary>
        /// Handles the Save button click event
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SaveCommand.CanExecute(PasswordBox))
            {
                ViewModel.SaveCommand.Execute(PasswordBox);

                // Check if processing is complete after a short delay
                System.Threading.Tasks.Task.Delay(500).ContinueWith(t =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Close the window after processing is complete
                        if (!ViewModel.IsLoading)
                        {
                            this.Close();
                        }
                    });
                });
            }
        }
    }
}