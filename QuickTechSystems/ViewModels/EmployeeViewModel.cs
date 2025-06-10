using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using QuickTechSystems.WPF.Views;

namespace QuickTechSystems.WPF.ViewModels
{
    public class EmployeeViewModel : ViewModelBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;

        private ObservableCollection<EmployeeDTO> _employees;
        private EmployeeDTO? _selectedEmployee;
        private EmployeeDTO? _currentEmployee;
        private bool _isNewEmployee;
        private ObservableCollection<EmployeeSalaryTransactionDTO> _salaryTransactions;
        private decimal _withdrawalAmount;
        private bool _isLoading;
        private string _errorMessage;

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }
        public async Task RefreshData()
        {
            await LoadDataAsync();
        }
        public ObservableCollection<EmployeeSalaryTransactionDTO> SalaryTransactions
        {
            get => _salaryTransactions;
            set => SetProperty(ref _salaryTransactions, value);
        }

        public decimal WithdrawalAmount
        {
            get => _withdrawalAmount;
            set => SetProperty(ref _withdrawalAmount, value);
        }

        public ObservableCollection<EmployeeDTO> Employees
        {
            get => _employees;
            set => SetProperty(ref _employees, value);
        }

        public EmployeeDTO? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (SetProperty(ref _selectedEmployee, value) && value != null)
                {
                    CurrentEmployee = new EmployeeDTO
                    {
                        EmployeeId = value.EmployeeId,
                        Username = value.Username,
                        FirstName = value.FirstName,
                        LastName = value.LastName,
                        Role = value.Role,
                        IsActive = value.IsActive,
                        MonthlySalary = value.MonthlySalary,
                        CurrentBalance = value.CurrentBalance
                    };
                    IsNewEmployee = false;
                    _ = LoadSalaryHistoryAsync(value.EmployeeId);
                }
            }
        }

        public EmployeeDTO? CurrentEmployee
        {
            get => _currentEmployee;
            set => SetProperty(ref _currentEmployee, value);
        }

        public bool IsNewEmployee
        {
            get => _isNewEmployee;
            set => SetProperty(ref _isNewEmployee, value);
        }

        public ObservableCollection<string> Roles { get; } = new()
        {
            "Admin",
            "Manager",
            "Cashier"
        };

        public ICommand AddCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ResetPasswordCommand { get; }
        public ICommand ProcessSalaryCommand { get; }
        public ICommand WithdrawalCommand { get; }

        public EmployeeViewModel(IEmployeeService employeeService, IEventAggregator eventAggregator)
            : base(eventAggregator)
        {
            _employeeService = employeeService;
            _employees = new ObservableCollection<EmployeeDTO>();
            _salaryTransactions = new ObservableCollection<EmployeeSalaryTransactionDTO>();
            _errorMessage = string.Empty;

            // Initialize commands
            AddCommand = new RelayCommand(_ => AddNew());
            SaveCommand = new AsyncRelayCommand(async param => await SaveAsync(param as PasswordBox));
            ResetPasswordCommand = new AsyncRelayCommand(async _ => await ResetPasswordAsync());
            ProcessSalaryCommand = new AsyncRelayCommand(async _ => await ProcessSalaryAsync());
            WithdrawalCommand = new AsyncRelayCommand(async _ => await ProcessWithdrawalAsync());

            _ = LoadDataAsync();
        }

        private async Task ProcessWithdrawalAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("A withdrawal operation is already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                if (CurrentEmployee == null)
                {
                    ShowTemporaryErrorMessage("No employee selected.");
                    return;
                }

                var dialog = new InputDialog("Salary Withdrawal", "Enter withdrawal amount:");
                if (dialog.ShowDialog() == true && decimal.TryParse(dialog.Input, out decimal amount))
                {
                    if (amount <= 0)
                    {
                        ShowTemporaryErrorMessage("Amount must be greater than zero");
                        return;
                    }

                    if (amount > CurrentEmployee.CurrentBalance)
                    {
                        ShowTemporaryErrorMessage("Withdrawal amount exceeds current balance");
                        return;
                    }

                    var notesDialog = new InputDialog("Withdrawal Notes", "Enter notes for this withdrawal:");
                    string notes = notesDialog.ShowDialog() == true ? notesDialog.Input : string.Empty;

                    await _employeeService.ProcessSalaryWithdrawalAsync(
                        CurrentEmployee.EmployeeId,
                        amount,
                        notes);

                    await LoadSalaryHistoryAsync(CurrentEmployee.EmployeeId);
                    await LoadEmployeeDetailsAsync(CurrentEmployee.EmployeeId);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Withdrawal processed successfully", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error processing withdrawal: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task LoadSalaryHistoryAsync(int employeeId)
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadSalaryHistoryAsync skipped - already in progress");
                return;
            }

            try
            {
                IsLoading = true;
                var transactions = await _employeeService.GetSalaryHistoryAsync(employeeId);
                SalaryTransactions = new ObservableCollection<EmployeeSalaryTransactionDTO>(transactions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading salary history: {ex.Message}");
                // Don't show a message here as this is a secondary operation
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task LoadEmployeeDetailsAsync(int employeeId)
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadEmployeeDetailsAsync skipped - already in progress");
                return;
            }

            try
            {
                IsLoading = true;
                var employee = await _employeeService.GetByIdAsync(employeeId);
                if (employee != null)
                {
                    CurrentEmployee = employee;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading employee details: {ex.Message}");
                // Don't show a message here as this is a secondary operation
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private async Task ProcessSalaryAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("A salary operation is already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                if (CurrentEmployee == null)
                {
                    ShowTemporaryErrorMessage("No employee selected.");
                    return;
                }

                var result = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    return MessageBox.Show(
                        $"Process monthly salary of {CurrentEmployee.MonthlySalary:C2} for {CurrentEmployee.FullName}?",
                        "Confirm Salary Processing",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                });

                if (result == MessageBoxResult.Yes)
                {
                    await _employeeService.ProcessMonthlySalaryAsync(CurrentEmployee.EmployeeId);
                    await LoadSalaryHistoryAsync(CurrentEmployee.EmployeeId);
                    await LoadEmployeeDetailsAsync(CurrentEmployee.EmployeeId);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Monthly salary processed successfully", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error processing salary: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private void AddNew()
        {
            CurrentEmployee = new EmployeeDTO
            {
                IsActive = true,
                Role = "Cashier",
                MonthlySalary = 0,
                CurrentBalance = 0
            };
            IsNewEmployee = true;
            SalaryTransactions.Clear();
        }

        private async Task SaveAsync(PasswordBox? passwordBox)
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("A save operation is already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                if (CurrentEmployee == null)
                {
                    ShowTemporaryErrorMessage("No employee to save.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(CurrentEmployee.Username) ||
                    string.IsNullOrWhiteSpace(CurrentEmployee.FirstName) ||
                    string.IsNullOrWhiteSpace(CurrentEmployee.LastName))
                {
                    ShowTemporaryErrorMessage("Please fill in all required fields");
                    return;
                }

                if (IsNewEmployee)
                {
                    if (passwordBox == null || string.IsNullOrWhiteSpace(passwordBox.Password))
                    {
                        ShowTemporaryErrorMessage("Please enter a password for the new employee");
                        return;
                    }

                    CurrentEmployee.PasswordHash = HashPassword(passwordBox.Password);
                    await _employeeService.CreateAsync(CurrentEmployee);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Employee created successfully", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                else
                {
                    await _employeeService.UpdateAsync(CurrentEmployee.EmployeeId, CurrentEmployee);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Employee updated successfully", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }

                await LoadDataAsync();
                IsNewEmployee = false;
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error saving employee: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        public async Task UpdateEmployeeDirectEdit(EmployeeDTO employee)
        {
            if (employee == null) return;

            // Don't wait if an operation is already in progress
            if (!await _operationLock.WaitAsync(0))
            {
                // For in-grid editing, just log rather than show error to avoid disrupting UX
                Debug.WriteLine("UpdateEmployeeDirectEdit skipped - another operation in progress");
                return;
            }

            try
            {
                // Don't set IsLoading for in-grid edits to avoid UI flickering
                ErrorMessage = string.Empty;

                // Create a copy to avoid issues during async operation
                var employeeToSave = new EmployeeDTO
                {
                    EmployeeId = employee.EmployeeId,
                    Username = employee.Username,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    Role = employee.Role,
                    IsActive = employee.IsActive,
                    MonthlySalary = employee.MonthlySalary,
                    CurrentBalance = employee.CurrentBalance,
                    // Preserve password hash
                    PasswordHash = employee.PasswordHash
                };

                await _employeeService.UpdateAsync(employeeToSave.EmployeeId, employeeToSave);

                // Update the local collection with the new values
                var existingEmployee = Employees.FirstOrDefault(e => e.EmployeeId == employee.EmployeeId);
                if (existingEmployee != null)
                {
                    int index = Employees.IndexOf(existingEmployee);
                    if (index >= 0)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // Update in-place to avoid collection refresh
                            Employees[index] = employeeToSave;
                        });
                    }
                }

                // Subtle feedback - don't show message box for routine edits
                Debug.WriteLine($"Employee {employee.Username} updated via direct edit");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating employee via direct edit: {ex.Message}");

                // Use a less intrusive error notification for in-grid edits
                ErrorMessage = $"Update failed: {ex.Message}";

                // Refresh to revert changes if there was an error
                await LoadDataAsync();
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task ResetPasswordAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("An operation is already in progress. Please wait.");
                return;
            }

            try
            {
                IsLoading = true;

                if (CurrentEmployee == null)
                {
                    ShowTemporaryErrorMessage("No employee selected.");
                    return;
                }

                var dialog = new InputDialog("Reset Password", "Enter new password:")
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    await _employeeService.ResetPasswordAsync(CurrentEmployee.EmployeeId, dialog.Input);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("Password has been reset successfully", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error resetting password: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        protected override async Task LoadDataAsync()
        {
            if (!await _operationLock.WaitAsync(0))
            {
                Debug.WriteLine("LoadDataAsync skipped - already in progress");
                return;
            }

            try
            {
                IsLoading = true;
                ErrorMessage = string.Empty;

                var employees = await _employeeService.GetAllAsync();
                Employees = new ObservableCollection<EmployeeDTO>(employees);
            }
            catch (Exception ex)
            {
                ShowTemporaryErrorMessage($"Error loading employees: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                _operationLock.Release();
            }
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            ErrorMessage = message;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });

            // Automatically clear error after delay
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (ErrorMessage == message) // Only clear if still the same message
                    {
                        ErrorMessage = string.Empty;
                    }
                });
            });
        }

        public override void Dispose()
        {
            if (!_isDisposed)
            {
                _operationLock?.Dispose();

                _isDisposed = true;
            }

            base.Dispose();
        }
    }
}