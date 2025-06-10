using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.WPF.Commands;
using System.ComponentModel;

namespace QuickTechSystems.WPF.ViewModels
{
    public class BulkProcessingStatusViewModel : ViewModelBase
    {
        private readonly IBulkOperationQueueService _queueService;
        private int _totalItems;
        private int _completedItems;
        private int _failedItems;
        private int _queuedItems;
        private bool _isProcessing;
        private string _statusMessage;
        private string _timeElapsed;
        private int _progressPercentage;
        private int _successRate;
        private ObservableCollection<string> _logMessages;
        private ObservableCollection<ErrorCategoryViewModel> _errorCategories;
        private bool _canRetry;

        public event EventHandler<DialogResultEventArgs> CloseRequested;

        public int TotalItems
        {
            get => _totalItems;
            set => SetProperty(ref _totalItems, value);
        }

        public int CompletedItems
        {
            get => _completedItems;
            set => SetProperty(ref _completedItems, value);
        }

        public int FailedItems
        {
            get => _failedItems;
            set => SetProperty(ref _failedItems, value);
        }

        public int QueuedItems
        {
            get => _queuedItems;
            set => SetProperty(ref _queuedItems, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(CanRetry));
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string TimeElapsed
        {
            get => _timeElapsed;
            set => SetProperty(ref _timeElapsed, value);
        }

        public int ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        public int SuccessRate
        {
            get => _successRate;
            set => SetProperty(ref _successRate, value);
        }

        public ObservableCollection<string> LogMessages
        {
            get => _logMessages;
            set => SetProperty(ref _logMessages, value);
        }

        public ObservableCollection<ErrorCategoryViewModel> ErrorCategories
        {
            get => _errorCategories;
            set => SetProperty(ref _errorCategories, value);
        }

        public bool CanRetry
        {
            get => _canRetry && !IsProcessing;
            set => SetProperty(ref _canRetry, value);
        }

        public ICommand CancelCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand RetryCommand { get; }

        public BulkProcessingStatusViewModel(IBulkOperationQueueService queueService, IEventAggregator eventAggregator)
            : base(eventAggregator)
        {
            _queueService = queueService;

            CancelCommand = new RelayCommand(_ => CancelProcessing());
            CloseCommand = new RelayCommand(_ => RequestClose());
            RetryCommand = new AsyncRelayCommand(async _ => await RetryFailedItemsAsync());

            LogMessages = new ObservableCollection<string>();
            ErrorCategories = new ObservableCollection<ErrorCategoryViewModel>();

            eventAggregator.Subscribe<BulkProcessingStatusEvent>(HandleStatusUpdate);

            IsProcessing = queueService.IsProcessing;
            TotalItems = queueService.TotalItemCount;
            CompletedItems = queueService.CompletedItemCount;
            FailedItems = queueService.FailedItemCount;
            QueuedItems = queueService.QueuedItemCount;
            CanRetry = FailedItems > 0;

            UpdateStatusMessage();

            AddLogMessage($"Started processing {TotalItems} items...");
        }

        private void UpdateStatusMessage()
        {
            if (IsProcessing)
            {
                StatusMessage = $"Processing items... {CompletedItems + FailedItems} of {TotalItems} completed.";
            }
            else if (TotalItems == 0)
            {
                StatusMessage = "No items to process.";
            }
            else if (FailedItems > 0)
            {
                StatusMessage = $"Processing completed with {FailedItems} failures. You can retry failed items.";
                CanRetry = true;
            }
            else
            {
                StatusMessage = "Processing completed successfully!";
                CanRetry = false;
            }
        }

        private void HandleStatusUpdate(BulkProcessingStatusEvent e)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TotalItems = e.TotalItems;
                    CompletedItems = e.CompletedItems;
                    FailedItems = e.FailedItems;
                    QueuedItems = e.QueuedItems;
                    IsProcessing = !e.IsCompleted;
                    ProgressPercentage = e.ProgressPercentage;
                    SuccessRate = e.SuccessPercentage;

                    TimeElapsed = FormatTimeSpan(e.ElapsedTime);

                    UpdateErrorCategories(e.ErrorCategories);

                    UpdateStatusMessage();

                    if ((CompletedItems > 0 && CompletedItems % 10 == 0) ||
                        (CompletedItems == TotalItems && !e.IsCompletionMessage))
                    {
                        AddLogMessage($"Processed {CompletedItems} of {TotalItems} items ({ProgressPercentage}% complete).");
                    }

                    if (FailedItems > 0 && (FailedItems % 5 == 0 || FailedItems == 1))
                    {
                        AddLogMessage($"Warning: {FailedItems} items have failed.");
                    }

                    if (e.IsCompleted && e.IsCompletionMessage)
                    {
                        string completionMessage = FailedItems > 0
                            ? $"Processing completed with {CompletedItems} successes ({SuccessRate}%) and {FailedItems} failures."
                            : $"Processing completed successfully! All {CompletedItems} items saved.";

                        AddLogMessage(completionMessage);
                        AddLogMessage($"Total processing time: {TimeElapsed}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling status update: {ex.Message}");
            }
        }

        private void UpdateErrorCategories(Dictionary<string, int> categories)
        {
            if (categories == null || categories.Count == 0)
                return;

            foreach (var category in categories)
            {
                var existingCategory = ErrorCategories.FirstOrDefault(c => c.Category == category.Key);

                if (existingCategory != null)
                {
                    existingCategory.Count = category.Value;
                }
                else
                {
                    ErrorCategories.Add(new ErrorCategoryViewModel
                    {
                        Category = category.Key,
                        Count = category.Value,
                        Description = GetErrorCategoryDescription(category.Key)
                    });
                }
            }
        }

        private string GetErrorCategoryDescription(string category)
        {
            switch (category)
            {
                case "DatabaseError":
                    return "Database related errors";
                case "DuplicateEntry":
                    return "Duplicate or conflicting data entries";
                case "ValidationError":
                    return "Data validation failures";
                case "Timeout":
                    return "Operation timeout issues";
                case "ReferenceError":
                    return "Missing references or null data";
                case "DataSaveFailed":
                    return "Failed to save item to database";
                default:
                    return $"{category} errors";
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
            }

            if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }

            if (timeSpan.TotalSeconds >= 10)
            {
                return $"{timeSpan.Seconds}.{timeSpan.Milliseconds / 100}s";
            }

            return $"{timeSpan.TotalSeconds:F2}s";
        }

        private void AddLogMessage(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                LogMessages.Add($"[{timestamp}] {message}");

                const int maxLogMessages = 100;
                while (LogMessages.Count > maxLogMessages)
                {
                    LogMessages.RemoveAt(0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding log message: {ex.Message}");
            }
        }

        private void CancelProcessing()
        {
            if (!IsProcessing) return;

            try
            {
                _queueService.CancelProcessing();
                AddLogMessage("Cancellation requested. Waiting for current operations to complete...");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cancelling processing: {ex.Message}");
                AddLogMessage($"Error cancelling processing: {ex.Message}");
            }
        }

        private async Task RetryFailedItemsAsync()
        {
            if (!CanRetry || IsProcessing) return;

            try
            {
                AddLogMessage($"Retrying {FailedItems} failed items...");
                await _queueService.RetryFailedItemsAsync();

                CanRetry = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrying failed items: {ex.Message}");
                AddLogMessage($"Error retrying items: {ex.Message}");
            }
        }

        private void RequestClose()
        {
            bool allowClose = !IsProcessing ||
                              System.Windows.MessageBox.Show(
                                  "Processing is still in progress. Are you sure you want to close this window?",
                                  "Confirm Close",
                                  System.Windows.MessageBoxButton.YesNo,
                                  System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;

            if (allowClose)
            {
                CloseRequested?.Invoke(this, new DialogResultEventArgs(true));
            }
        }

        public override void Dispose()
        {
            try
            {
                _eventAggregator.Unsubscribe<BulkProcessingStatusEvent>(HandleStatusUpdate);

                // Cancel any ongoing processing when disposing
                _queueService?.CancelProcessing();

                // Don't try to set read-only properties to null
                // Instead, clear collections to release references
                LogMessages?.Clear();
                ErrorCategories?.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during BulkProcessingStatusViewModel disposal: {ex.Message}");
            }
            finally
            {
                base.Dispose();
            }
        }

        public class ErrorCategoryViewModel : INotifyPropertyChanged
        {
            private string _category;
            private int _count;
            private string _description;

            public string Category
            {
                get => _category;
                set
                {
                    _category = value;
                    OnPropertyChanged(nameof(Category));
                }
            }

            public int Count
            {
                get => _count;
                set
                {
                    _count = value;
                    OnPropertyChanged(nameof(Count));
                }
            }

            public string Description
            {
                get => _description;
                set
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class DialogResultEventArgs : EventArgs
        {
            public bool DialogResult { get; }

            public DialogResultEventArgs(bool dialogResult)
            {
                DialogResult = dialogResult;
            }
        }
    }
}