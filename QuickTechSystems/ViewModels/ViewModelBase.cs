// QuickTechSystems.WPF/ViewModels/ViewModelBase.cs
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Collections.Generic;
using QuickTechSystems.Application.Events;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Infrastructure.Data;
using QuickTechSystems.Infrastructure.Services;
using QuickTechSystems.Application.Interfaces;

namespace QuickTechSystems.WPF.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        protected readonly IEventAggregator _eventAggregator;
        private readonly IDbContextScopeService? _dbContextScopeService;
        private bool _disposed;
        private string _errorMessage = string.Empty;
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        protected ViewModelBase(IEventAggregator eventAggregator, IDbContextScopeService? dbContextScopeService = null)
        {
            Debug.WriteLine($"Initializing {GetType().Name}");
            _eventAggregator = eventAggregator;
            _dbContextScopeService = dbContextScopeService;
            SubscribeToEvents();
        }

        protected async Task<T> ExecuteDbOperationAsync<T>(Func<Task<T>> operation, string errorContext = "Database operation")
        {
            try
            {
                if (_dbContextScopeService != null)
                {
                    // Use scope service if available
                    return await _dbContextScopeService.ExecuteInScopeAsync(async context =>
                    {
                        return await operation();
                    });
                }
                else
                {
                    // Fall back to direct execution
                    return await operation();
                }
            }
            catch (ObjectDisposedException)
            {
                // Wait briefly and retry once
                await Task.Delay(100);
                try
                {
                    return await operation();
                }
                catch (Exception retryEx)
                {
                    await HandleExceptionAsync($"{errorContext} - Retry failed", retryEx);
                    throw;
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disposed") || ex.Message.Contains("second operation"))
            {
                // Wait briefly and retry once
                await Task.Delay(100);
                try
                {
                    return await operation();
                }
                catch (Exception retryEx)
                {
                    await HandleExceptionAsync($"{errorContext} - Retry failed", retryEx);
                    throw;
                }
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(errorContext, ex);
                throw;
            }
        }

        protected async Task ShowSuccessMessage(string message)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(
                    message,
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            });
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void SubscribeToEvents()
        {
            // Override in derived classes to subscribe to specific events
        }

        protected virtual void UnsubscribeFromEvents()
        {
            // Override in derived classes to unsubscribe from specific events
        }

        public async Task LoadAsync()
        {
            if (!await _asyncLock.WaitAsync(0))
            {
                // Another load operation is already in progress
                ShowTemporaryErrorMessage("Loading operation already in progress. Please wait.");
                return;
            }

            try
            {
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Error loading data", ex);
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        protected virtual async Task LoadDataAsync()
        {
            try
            {
                await ExecuteDbOperationAsync(async () => {
                    await LoadDataImplementationAsync();
                    return true;
                }, "Error loading data");
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Error loading data", ex);
            }
        }

        protected virtual async Task LoadDataImplementationAsync()
        {
            await Task.CompletedTask;
        }

        protected async Task ShowErrorMessageAsync(string message)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        protected async Task HandleExceptionAsync(string context, Exception ex)
        {
            Debug.WriteLine($"{context}: {ex}");

            // Special handling for known database errors
            if (ex.Message.Contains("A second operation was started") ||
                (ex.InnerException != null && ex.InnerException.Message.Contains("A second operation was started")))
            {
                ShowTemporaryErrorMessage("The system is busy processing another request. Please wait a moment and try again.");
            }
            else if (ex.Message.Contains("Cannot access a disposed context") ||
                    (ex.InnerException != null && ex.InnerException.Message.Contains("Cannot access a disposed context")))
            {
                ShowTemporaryErrorMessage("Database connection issue detected. Please try your action again.");
            }
            else if (ex.Message.Contains("The connection was closed") ||
                    (ex.InnerException != null && ex.InnerException.Message.Contains("The connection was closed")))
            {
                ShowTemporaryErrorMessage("Database connection lost. Please check your connection and try again.");
            }
            else if (ex.Message.Contains("DbContext") || ex.Message.Contains("Entity Framework") ||
                    (ex.InnerException != null && (ex.InnerException.Message.Contains("DbContext") ||
                     ex.InnerException.Message.Contains("Entity Framework"))))
            {
                ShowTemporaryErrorMessage("Database operation error. Please try again or restart the application.");
            }
            else
            {
                ShowTemporaryErrorMessage($"An error occurred: {ex.Message}");
                await ShowErrorMessageAsync(ex.Message);
            }
        }

        protected void ShowTemporaryErrorMessage(string message)
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

        protected async Task RunCommandAsync(Func<Task> action)
        {
            if (!await _asyncLock.WaitAsync(0))
            {
                ShowTemporaryErrorMessage("Another command is already running. Please wait.");
                return;
            }

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync("Command execution error", ex);
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                UnsubscribeFromEvents();
                _asyncLock.Dispose();
            }
            _disposed = true;
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}