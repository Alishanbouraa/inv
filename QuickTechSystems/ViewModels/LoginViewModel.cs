using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF;
using System.Windows;
using QuickTechSystems.WPF.Commands;

namespace QuickTechSystems.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly IDrawerService _drawerService;
        private readonly IActivityLogger _activityLogger;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private FlowDirection _flowDirection = FlowDirection.LeftToRight;
        private string _username = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isProcessing;

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }
        public FlowDirection FlowDirection
        {
            get => _flowDirection;
            set => SetProperty(ref _flowDirection, value);
        }
        public ICommand LoginCommand { get; }

        public LoginViewModel(
            IAuthService authService,
            IDrawerService drawerService,
            IActivityLogger activityLogger,
            IEventAggregator eventAggregator)
            : base(eventAggregator)
        {
            _authService = authService;
            _drawerService = drawerService;
            _activityLogger = activityLogger;
            LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, CanExecuteLogin);
        }

        private bool CanExecuteLogin(object? parameter)
        {
            return !IsProcessing && !string.IsNullOrWhiteSpace(Username);
        }

        private async Task ExecuteLoginAsync(object? parameter)
        {
            if (parameter is not PasswordBox passwordBox) return;

            if (!await _operationLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Login operation already in progress. Please wait.");
                return;
            }

            try
            {
                IsProcessing = true;
                ErrorMessage = string.Empty;

                try
                {
                    var employee = await _authService.LoginAsync(Username, passwordBox.Password);

                    if (employee == null)
                    {
                        ShowTemporaryErrorMessage("Invalid username or password");
                        return;
                    }

                    // Ensure the role is valid
                    if (!Enum.TryParse<UserRole>(employee.Role, true, out var role))
                    {
                        ShowTemporaryErrorMessage("Invalid user role configuration");
                        return;
                    }

                    await _activityLogger.LogActivityAsync(
                        employee.Username,
                        "Login",
                        $"User logged in successfully with role {role}"
                    );

                    App.Current.Properties["CurrentUser"] = employee;
                    await CheckDrawerStatusAndProceed(passwordBox, employee);
                }
                catch (Exception ex)
                {
                    await HandleExceptionAsync("Login error", ex);
                }
            }
            finally
            {
                IsProcessing = false;
                _operationLock.Release();
            }
        }

        private async Task CheckDrawerStatusAndProceed(PasswordBox passwordBox, EmployeeDTO employee)
        {
            try
            {
                var currentDrawer = await _drawerService.GetCurrentDrawerAsync();

                if (currentDrawer == null)
                {
                    var window = new CashDrawerPromptWindow();
                    var viewModel = new CashDrawerPromptViewModel(
                        _drawerService,
                        window,
                        _eventAggregator);
                    window.DataContext = viewModel;

                    var result = window.ShowDialog();
                    if (result != true)
                    {
                        ShowTemporaryErrorMessage("Drawer must be opened to continue");
                        await _activityLogger.LogActivityAsync(
                            employee.Username,
                            "Drawer Opening Cancelled",
                            "User cancelled drawer opening process",
                            false
                        );
                        return;
                    }

                    await _activityLogger.LogActivityAsync(
                        employee.Username,
                        "Drawer Opened",
                        "New cash drawer opened"
                    );
                }

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    Window.GetWindow(passwordBox)?.Close();
                });
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Error checking drawer status", ex);
            }
        }

        private async Task HandleExceptionAsync(string context, Exception ex)
        {
            Debug.WriteLine($"{context}: {ex}");

            // Special handling for known database errors
            if (ex.Message.Contains("A second operation was started") ||
                (ex.InnerException != null && ex.InnerException.Message.Contains("A second operation was started")))
            {
                ShowTemporaryErrorMessage("System is busy. Please try again in a moment.");
            }
            else if (ex.Message.Contains("The connection was closed") ||
                    (ex.InnerException != null && ex.InnerException.Message.Contains("The connection was closed")))
            {
                ShowTemporaryErrorMessage("Database connection lost. Please check your connection and try again.");
            }
            else
            {
                ShowTemporaryErrorMessage($"An error occurred during login. Please try again.");

                await _activityLogger.LogActivityAsync(
                    Username,
                    "Login Error",
                    ex.Message,
                    false,
                    ex.Message
                );
            }
        }

        private void ShowTemporaryErrorMessage(string message)
        {
            ErrorMessage = message;

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

        protected override Task LoadDataAsync()
        {
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _operationLock?.Dispose();
            base.Dispose();
        }
    }
}