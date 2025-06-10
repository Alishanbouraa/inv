using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.WPF.ViewModels;

namespace QuickTechSystems.WPF.Views
{
    /// <summary>
    /// Interaction logic for EmployeeView.xaml
    /// </summary>
    public partial class EmployeeView : UserControl
    {
        private EmployeeWindow? _employeeWindow;
        private SalaryWindow? _salaryWindow;

        /// <summary>
        /// Initialize a new instance of EmployeeView
        /// </summary>
        public EmployeeView()
        {
            InitializeComponent();

            // Register to the Loaded event to adjust layout based on container size
            this.Loaded += OnControlLoaded;
            this.SizeChanged += OnControlSizeChanged;
        }

        /// <summary>
        /// Handles the control loaded event
        /// </summary>
        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            AdjustLayoutForSize();
        }

        /// <summary>
        /// Handles the control size changed event
        /// </summary>
        private void OnControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustLayoutForSize();
        }

        /// <summary>
        /// Adjusts the layout based on the window size
        /// </summary>
        private void AdjustLayoutForSize()
        {
            var parentWindow = Window.GetWindow(this);
            if (parentWindow == null) return;

            // Get actual window dimensions
            double windowWidth = parentWindow.ActualWidth;

            // Set margins and paddings based on window size
            var scrollViewer = this.Content as ScrollViewer;
            if (scrollViewer == null) return;

            var rootGrid = scrollViewer.Content as Grid;
            if (rootGrid == null) return;

            if (windowWidth >= 1920) // Large screens
            {
                rootGrid.Margin = new Thickness(32);
            }
            else if (windowWidth >= 1366) // Medium screens
            {
                rootGrid.Margin = new Thickness(24);
            }
            else if (windowWidth >= 800) // Small screens
            {
                rootGrid.Margin = new Thickness(16);
            }
            else // Very small screens
            {
                rootGrid.Margin = new Thickness(8);
            }
        }

        /// <summary>
        /// Handles direct cell editing in the DataGrid
        /// </summary>
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var viewModel = DataContext as EmployeeViewModel;
                var employee = e.Row.Item as EmployeeDTO;

                if (viewModel != null && employee != null)
                {
                    // Update the employee directly in-grid
                    _ = viewModel.UpdateEmployeeDirectEdit(employee);
                }
            }
        }

        /// <summary>
        /// Handles double-click on employee in the DataGrid
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is EmployeeDTO employee)
            {
                OpenEmployeeWindow(employee);
            }
        }

        /// <summary>
        /// Handles Add New Employee button click
        /// </summary>
        private void AddNewEmployee_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is EmployeeViewModel viewModel)
            {
                // Use the existing AddCommand to set up a new employee
                viewModel.AddCommand.Execute(null);

                // Open the employee window with the new employee details
                OpenNewEmployeeWindow();
            }
        }

        /// <summary>
        /// Handles Edit button click in the employee list
        /// </summary>
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is EmployeeDTO employee)
            {
                OpenEmployeeWindow(employee);
            }
        }

        /// <summary>
        /// Handles Salary button click in the employee list
        /// </summary>
        private void SalaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is EmployeeDTO employee)
            {
                OpenSalaryWindow(employee);
            }
        }

        /// <summary>
        /// Handles Reset Password button click in the employee list
        /// </summary>
        private void ResetPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is EmployeeDTO employee &&
                DataContext is EmployeeViewModel viewModel)
            {
                viewModel.SelectedEmployee = employee;
                viewModel.ResetPasswordCommand.Execute(null);
            }
        }

        /// <summary>
        /// Handles Edit menu item click in the context menu
        /// </summary>
        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is EmployeeViewModel viewModel && viewModel.SelectedEmployee != null)
            {
                OpenEmployeeWindow(viewModel.SelectedEmployee);
            }
        }

        /// <summary>
        /// Handles Salary menu item click in the context menu
        /// </summary>
        private void SalaryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is EmployeeViewModel viewModel && viewModel.SelectedEmployee != null)
            {
                OpenSalaryWindow(viewModel.SelectedEmployee);
            }
        }

        /// <summary>
        /// Handles Reset Password menu item click in the context menu
        /// </summary>
        private void ResetPasswordMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is EmployeeViewModel viewModel && viewModel.SelectedEmployee != null)
            {
                viewModel.ResetPasswordCommand.Execute(null);
            }
        }

        /// <summary>
        /// Opens the employee edit window for an existing employee
        /// </summary>
        private void OpenEmployeeWindow(EmployeeDTO employee)
        {
            if (DataContext is EmployeeViewModel viewModel)
            {
                viewModel.SelectedEmployee = employee;

                // Close existing window if open
                _employeeWindow?.Close();

                // Create and show new window
                _employeeWindow = new EmployeeWindow(viewModel);
                _employeeWindow.Closed += (s, e) =>
                {
                    _employeeWindow = null;

                    // Refresh the employee list after the window is closed
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await viewModel.RefreshData();
                    });
                };

                _employeeWindow.Show();
            }
        }

        /// <summary>
        /// Opens the employee edit window for a new employee
        /// </summary>
        private void OpenNewEmployeeWindow()
        {
            if (DataContext is EmployeeViewModel viewModel)
            {
                // Close existing window if open
                _employeeWindow?.Close();

                // Create and show new window
                _employeeWindow = new EmployeeWindow(viewModel);
                _employeeWindow.Closed += (s, e) =>
                {
                    _employeeWindow = null;

                    // Refresh the employee list after the window is closed
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await viewModel.RefreshData();
                    });
                };

                _employeeWindow.Show();
            }
        }

        /// <summary>
        /// Opens the salary management window for an employee
        /// </summary>
        private void OpenSalaryWindow(EmployeeDTO employee)
        {
            if (DataContext is EmployeeViewModel viewModel)
            {
                viewModel.SelectedEmployee = employee;

                // Close existing window if open
                _salaryWindow?.Close();

                // Create and show new window
                _salaryWindow = new SalaryWindow(viewModel);
                _salaryWindow.Closed += (s, e) =>
                {
                    _salaryWindow = null;

                    // Refresh the employee list after the window is closed
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await viewModel.RefreshData();
                    });
                };

                _salaryWindow.Show();
            }
        }
    }
}