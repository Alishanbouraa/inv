using System.Windows;
using System.Windows.Input;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    /// <summary>
    /// Interaction logic for ExpenseWindow.xaml
    /// </summary>
    public partial class ExpenseWindow : Window
    {
        public ExpenseViewModel ViewModel { get; private set; }

        /// <summary>
        /// Initialize a new instance of ExpenseWindow
        /// </summary>
        /// <param name="viewModel">The ExpenseViewModel instance to use as DataContext</param>
        public ExpenseWindow(ExpenseViewModel viewModel)
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
            if (ViewModel.SaveCommand.CanExecute(null))
            {
                ViewModel.SaveCommand.Execute(null);

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

        /// <summary>
        /// Handles the Delete button click event
        /// </summary>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.DeleteCommand.CanExecute(ViewModel.CurrentExpense))
            {
                ViewModel.DeleteCommand.Execute(ViewModel.CurrentExpense);

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