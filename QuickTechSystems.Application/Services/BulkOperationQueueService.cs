// QuickTechSystems.Application/Services/BulkOperationQueueService.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Events;
using QuickTechSystems.Application.Services.Interfaces;

namespace QuickTechSystems.Application.Services
{
    /// <summary>
    /// Implementation of the bulk operation queue service with enhanced error handling and state management
    /// </summary>
    public class BulkOperationQueueService : IBulkOperationQueueService
    {
        #region Private Fields

        // Core data structures
        private readonly ConcurrentDictionary<string, QueueItem> _itemIndex = new();
        private readonly ConcurrentQueue<PrioritizedItem> _processingQueue = new();
        private readonly ConcurrentDictionary<string, ErrorInfo> _errorIndex = new();

        // Dependencies
        private readonly IMainStockService _mainStockService;
        private readonly IEventAggregator _eventAggregator;

        // State control
        private bool _isProcessing;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly int _batchSize = 10;
        private CancellationTokenSource _cancellationTokenSource;

        // Performance tracking
        private readonly Stopwatch _processingTimer = new();
        private int _totalProcessed;
        private int _successCount;
        private int _failureCount;
        private readonly Dictionary<string, int> _errorCategories = new();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="BulkOperationQueueService"/> class
        /// </summary>
        /// <param name="mainStockService">The main stock service</param>
        /// <param name="eventAggregator">The event aggregator</param>
        public BulkOperationQueueService(IMainStockService mainStockService, IEventAggregator eventAggregator)
        {
            _mainStockService = mainStockService ?? throw new ArgumentNullException(nameof(mainStockService));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        }

        #endregion

        #region Public Properties

        /// <inheritdoc/>
        public bool IsProcessing => _isProcessing;

        /// <inheritdoc/>
        public int QueuedItemCount => _processingQueue.Count;

        /// <inheritdoc/>
        public int TotalItemCount => _itemIndex.Count;

        /// <inheritdoc/>
        public int CompletedItemCount => _itemIndex.Count(s => s.Value.State == ProcessingState.Completed);

        /// <inheritdoc/>
        public int FailedItemCount => _itemIndex.Count(s => s.Value.State == ProcessingState.Failed);

        #endregion

        #region Public Methods

        /// <inheritdoc/>
        public void EnqueueItems(List<MainStockDTO> items, int priority = 0)
        {
            if (items == null || !items.Any())
                return;

            foreach (var item in items)
            {
                string itemKey = GetItemKey(item);

                var queueItem = new QueueItem
                {
                    OriginalItem = item,
                    State = ProcessingState.Queued,
                    EnqueueTime = DateTime.UtcNow,
                    Priority = priority
                };

                _itemIndex[itemKey] = queueItem;
                _processingQueue.Enqueue(new PrioritizedItem { Key = itemKey, Priority = priority });
            }

            // Log the enqueuing operation
            Debug.WriteLine($"Enqueued {items.Count} items for processing with priority {priority}");

            StartProcessing();
        }

        /// <inheritdoc/>
        public Dictionary<string, ProcessingState> GetAllStatus()
        {
            return _itemIndex.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.State);
        }

        /// <inheritdoc/>
        public List<MainStockDTO> GetCompletedItems()
        {
            return _itemIndex
                .Where(kvp => kvp.Value.State == ProcessingState.Completed && kvp.Value.ResultItem != null)
                .Select(kvp => kvp.Value.ResultItem)
                .ToList();
        }

        /// <inheritdoc/>
        public List<Tuple<MainStockDTO, string>> GetFailedItems()
        {
            return _itemIndex
                .Where(kvp => kvp.Value.State == ProcessingState.Failed)
                .Select(kvp => new Tuple<MainStockDTO, string>(
                    kvp.Value.OriginalItem,
                    _errorIndex.TryGetValue(kvp.Key, out var error) ? error.Message : "Unknown error"))
                .ToList();
        }

        /// <inheritdoc/>
        public Dictionary<string, List<Tuple<MainStockDTO, string>>> GetErrorsByCategory()
        {
            var result = new Dictionary<string, List<Tuple<MainStockDTO, string>>>();

            foreach (var error in _errorIndex)
            {
                if (!_itemIndex.TryGetValue(error.Key, out var item))
                    continue;

                string category = error.Value.Category ?? "General";

                if (!result.TryGetValue(category, out var list))
                {
                    list = new List<Tuple<MainStockDTO, string>>();
                    result[category] = list;
                }

                list.Add(new Tuple<MainStockDTO, string>(item.OriginalItem, error.Value.Message));
            }

            return result;
        }

        /// <inheritdoc/>
        public void CancelProcessing()
        {
            try
            {
                _cancellationTokenSource?.Cancel();

                // Mark any items still in queue as cancelled
                foreach (var key in _itemIndex.Keys)
                {
                    if (_itemIndex.TryGetValue(key, out var item) &&
                        (item.State == ProcessingState.Queued || item.State == ProcessingState.Processing))
                    {
                        item.State = ProcessingState.Cancelled;
                        _itemIndex[key] = item; // Update the item in the dictionary
                    }
                }

                // Clear the processing queue
                while (_processingQueue.TryDequeue(out _)) { }

                Debug.WriteLine("Bulk processing cancelled by user");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cancelling processing: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task RetryFailedItemsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var failedItems = _itemIndex
                    .Where(kvp => kvp.Value.State == ProcessingState.Failed)
                    .ToList();

                if (!failedItems.Any())
                    return;

                Debug.WriteLine($"Retrying {failedItems.Count} failed items");

                foreach (var kvp in failedItems)
                {
                    var queueItem = kvp.Value;
                    queueItem.State = ProcessingState.PendingRetry;
                    queueItem.RetryCount++;
                    _itemIndex[kvp.Key] = queueItem;

                    _processingQueue.Enqueue(new PrioritizedItem
                    {
                        Key = kvp.Key,
                        Priority = queueItem.Priority + 10 // Boost priority for retries
                    });
                }

                StartProcessing();
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            try
            {
                CancelProcessing();

                _itemIndex.Clear();
                _errorIndex.Clear();
                _errorCategories.Clear();
                _totalProcessed = 0;
                _successCount = 0;
                _failureCount = 0;

                // Ensure processing state is reset
                _isProcessing = false;

                // Clear any disposed cancellation token source
                if (_cancellationTokenSource != null)
                {
                    try
                    {
                        if (!_cancellationTokenSource.IsCancellationRequested)
                        {
                            _cancellationTokenSource.Cancel();
                        }
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = null;
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed, ignore
                    }
                }

                Debug.WriteLine("BulkOperationQueueService has been reset");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting BulkOperationQueueService: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Starts processing the queue if not already processing
        /// </summary>
        private async void StartProcessing()
        {
            if (_isProcessing) return;

            await _lock.WaitAsync();
            try
            {
                if (_isProcessing) return;
                _isProcessing = true;

                _cancellationTokenSource = new CancellationTokenSource();
                _processingTimer.Restart();

                // Start a background task for processing
                Task.Run(ProcessQueueAsync, _cancellationTokenSource.Token);
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Processes the items in the queue asynchronously
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            try
            {
                // Report initial status
                PublishProgressEvent(false);

                while (!_processingQueue.IsEmpty && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Process a batch of items
                    await ProcessBatchAsync();

                    // Publish progress event after each batch
                    PublishProgressEvent(false);

                    // Give the UI thread a chance to update
                    await Task.Delay(50, _cancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Bulk processing was cancelled");
            }
            catch (Exception ex)
            {
                LogProcessingError(ex);

                // Mark remaining items as failed
                MarkRemainingItemsAsFailed(ex);
            }
            finally
            {
                _isProcessing = false;
                _processingTimer.Stop();

                // Publish final completion event
                PublishProgressEvent(true);

                // Log processing summary
                LogProcessingSummary();

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Processes a batch of items from the queue
        /// </summary>
        private async Task ProcessBatchAsync()
        {
            // Get a batch of items from the queue
            var batch = DequeueNextBatch();
            if (batch.Count == 0) return;

            try
            {
                // Update status for items in batch
                foreach (var key in batch)
                {
                    if (_itemIndex.TryGetValue(key, out var item))
                    {
                        item.State = ProcessingState.Processing;
                        item.ProcessStartTime = DateTime.UtcNow;
                        _itemIndex[key] = item;
                    }
                }

                // Create list of MainStockDTO items to process
                var itemsToProcess = new List<MainStockDTO>();
                foreach (var key in batch)
                {
                    if (_itemIndex.TryGetValue(key, out var item))
                    {
                        itemsToProcess.Add(item.OriginalItem);
                    }
                }

                // Process the batch
                if (itemsToProcess.Count > 0)
                {
                    var savedItems = await _mainStockService.CreateBatchAsync(itemsToProcess);

                    // Update status based on results
                    UpdateBatchResults(batch, itemsToProcess, savedItems);
                }
            }
            catch (Exception ex)
            {
                // Handle batch-level error
                HandleBatchError(batch, ex);
            }
        }

        /// <summary>
        /// Dequeues the next batch of items from the queue based on priority
        /// </summary>
        /// <returns>List of item keys in the batch</returns>
        private List<string> DequeueNextBatch()
        {
            var batch = new List<string>();
            var tempQueue = new List<PrioritizedItem>();

            // Dequeue up to batch size items
            while (batch.Count < _batchSize && _processingQueue.TryDequeue(out var item))
            {
                tempQueue.Add(item);
            }

            // Sort by priority (higher first)
            tempQueue = tempQueue.OrderByDescending(i => i.Priority).ToList();

            // Take the highest priority items up to batch size
            foreach (var item in tempQueue.Take(_batchSize))
            {
                batch.Add(item.Key);
            }

            // Re-enqueue any items we didn't use
            foreach (var item in tempQueue.Skip(_batchSize))
            {
                _processingQueue.Enqueue(item);
            }

            return batch;
        }

        /// <summary>
        /// Updates the results for a batch of processed items
        /// </summary>
        /// <param name="batchKeys">The keys of the items in the batch</param>
        /// <param name="originalItems">The original items that were processed</param>
        /// <param name="savedItems">The saved items returned from processing</param>
        private void UpdateBatchResults(List<string> batchKeys, List<MainStockDTO> originalItems, List<MainStockDTO> savedItems)
        {
            foreach (var key in batchKeys)
            {
                if (!_itemIndex.TryGetValue(key, out var queueItem))
                    continue;

                var originalItem = queueItem.OriginalItem;
                var savedItem = savedItems.FirstOrDefault(s =>
                    (s.Barcode == originalItem.Barcode && !string.IsNullOrEmpty(originalItem.Barcode)) ||
                    (s.Name == originalItem.Name && string.IsNullOrEmpty(originalItem.Barcode)));

                if (savedItem != null)
                {
                    // Success case
                    queueItem.State = ProcessingState.Completed;
                    queueItem.ResultItem = savedItem;
                    queueItem.ProcessEndTime = DateTime.UtcNow;
                    _itemIndex[key] = queueItem;

                    _successCount++;
                }
                else
                {
                    // Failure case
                    queueItem.State = ProcessingState.Failed;
                    queueItem.ProcessEndTime = DateTime.UtcNow;
                    _itemIndex[key] = queueItem;

                    // Record error
                    _errorIndex[key] = new ErrorInfo
                    {
                        Message = "Item was not saved during batch processing",
                        Category = "DataSaveFailed",
                        Timestamp = DateTime.UtcNow
                    };

                    IncrementErrorCategory("DataSaveFailed");
                    _failureCount++;
                }

                _totalProcessed++;
            }
        }

        /// <summary>
        /// Handles an error that occurred during batch processing
        /// </summary>
        /// <param name="batchKeys">The keys of the items in the batch</param>
        /// <param name="exception">The exception that occurred</param>
        private void HandleBatchError(List<string> batchKeys, Exception exception)
        {
            string errorMessage = GetDetailedErrorMessage(exception);
            string errorCategory = CategorizeError(exception);

            foreach (var key in batchKeys)
            {
                if (!_itemIndex.TryGetValue(key, out var queueItem))
                    continue;

                // Update item status
                queueItem.State = ProcessingState.Failed;
                queueItem.ProcessEndTime = DateTime.UtcNow;
                _itemIndex[key] = queueItem;

                // Record error details
                _errorIndex[key] = new ErrorInfo
                {
                    Message = errorMessage,
                    Category = errorCategory,
                    Timestamp = DateTime.UtcNow,
                    Exception = exception
                };

                _failureCount++;
                _totalProcessed++;
            }

            IncrementErrorCategory(errorCategory);

            Debug.WriteLine($"Batch error ({errorCategory}): {errorMessage}");
        }

        /// <summary>
        /// Gets the key for an item based on its barcode or name
        /// </summary>
        /// <param name="item">The item to get a key for</param>
        /// <returns>A unique key for the item</returns>
        private string GetItemKey(MainStockDTO item)
        {
            if (item == null)
                return $"null:{Guid.NewGuid()}";

            return !string.IsNullOrEmpty(item.Barcode)
                ? $"barcode:{item.Barcode}"
                : $"name:{item.Name}:{Guid.NewGuid()}";
        }

        /// <summary>
        /// Gets a detailed error message from an exception
        /// </summary>
        /// <param name="ex">The exception</param>
        /// <returns>A detailed error message</returns>
        private string GetDetailedErrorMessage(Exception ex)
        {
            if (ex == null)
                return "Unknown error";

            var message = ex.Message;
            var currentEx = ex;

            while (currentEx.InnerException != null)
            {
                currentEx = currentEx.InnerException;
                message = currentEx.Message;
            }

            return message;
        }

        /// <summary>
        /// Categorizes an error based on the exception type and message
        /// </summary>
        /// <param name="ex">The exception to categorize</param>
        /// <returns>The error category</returns>
        private string CategorizeError(Exception ex)
        {
            if (ex == null)
                return "Unknown";

            var message = ex.Message.ToLowerInvariant();

            if (ex is DbUpdateException || message.Contains("database") || message.Contains("sql"))
                return "DatabaseError";

            if (message.Contains("duplicate") || message.Contains("unique") || message.Contains("constraint"))
                return "DuplicateEntry";

            if (message.Contains("validation") || message.Contains("invalid"))
                return "ValidationError";

            if (message.Contains("timeout") || message.Contains("timed out"))
                return "Timeout";

            if (message.Contains("not found") || message.Contains("null") || message.Contains("reference"))
                return "ReferenceError";

            return ex.GetType().Name;
        }

        /// <summary>
        /// Increments the count for an error category
        /// </summary>
        /// <param name="category">The error category</param>
        private void IncrementErrorCategory(string category)
        {
            if (!_errorCategories.TryGetValue(category, out var count))
            {
                count = 0;
            }

            _errorCategories[category] = count + 1;
        }

        /// <summary>
        /// Logs an error that occurred during queue processing
        /// </summary>
        /// <param name="ex">The exception that occurred</param>
        private void LogProcessingError(Exception ex)
        {
            Debug.WriteLine($"Error in bulk operation queue processing: {ex.Message}");

            if (ex.InnerException != null)
            {
                Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }

            Debug.WriteLine(ex.StackTrace);
        }

        /// <summary>
        /// Marks all remaining items in the queue as failed
        /// </summary>
        /// <param name="ex">The exception that caused the failure</param>
        private void MarkRemainingItemsAsFailed(Exception ex)
        {
            string errorMessage = GetDetailedErrorMessage(ex);
            string errorCategory = CategorizeError(ex);

            while (_processingQueue.TryDequeue(out var prioritizedItem))
            {
                string key = prioritizedItem.Key;

                if (!_itemIndex.TryGetValue(key, out var queueItem))
                    continue;

                queueItem.State = ProcessingState.Failed;
                queueItem.ProcessEndTime = DateTime.UtcNow;
                _itemIndex[key] = queueItem;

                _errorIndex[key] = new ErrorInfo
                {
                    Message = $"Queue processing error: {errorMessage}",
                    Category = errorCategory,
                    Timestamp = DateTime.UtcNow,
                    Exception = ex
                };

                _failureCount++;
                _totalProcessed++;
            }

            IncrementErrorCategory(errorCategory);
        }

        /// <summary>
        /// Logs a summary of the processing operation
        /// </summary>
        private void LogProcessingSummary()
        {
            var duration = _processingTimer.Elapsed;
            var itemsPerSecond = duration.TotalSeconds > 0
                ? _totalProcessed / duration.TotalSeconds
                : 0;

            Debug.WriteLine($"Bulk processing completed in {duration.TotalSeconds:F2}s");
            Debug.WriteLine($"Processed {_totalProcessed} items ({itemsPerSecond:F2} items/sec)");
            Debug.WriteLine($"Success: {_successCount}, Failed: {_failureCount}");

            if (_errorCategories.Count > 0)
            {
                Debug.WriteLine("Error categories:");
                foreach (var category in _errorCategories)
                {
                    Debug.WriteLine($"  {category.Key}: {category.Value}");
                }
            }
        }

        /// <summary>
        /// Publishes a progress event to the event aggregator
        /// </summary>
        /// <param name="isComplete">Whether processing is complete</param>
        private void PublishProgressEvent(bool isComplete)
        {
            try
            {
                _eventAggregator.Publish(new BulkProcessingStatusEvent
                {
                    QueuedItems = QueuedItemCount,
                    CompletedItems = CompletedItemCount,
                    FailedItems = FailedItemCount,
                    TotalItems = TotalItemCount,
                    IsCompleted = isComplete,
                    IsCompletionMessage = isComplete,
                    ElapsedTime = _processingTimer.Elapsed,
                    ErrorCategories = _errorCategories.ToDictionary(k => k.Key, v => v.Value)
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error publishing progress event: {ex.Message}");
            }
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents an item in the processing queue
        /// </summary>
        private class QueueItem
        {
            /// <summary>
            /// Gets or sets the original item to process
            /// </summary>
            public MainStockDTO OriginalItem { get; set; }

            /// <summary>
            /// Gets or sets the result item after processing
            /// </summary>
            public MainStockDTO ResultItem { get; set; }

            /// <summary>
            /// Gets or sets the current state of the item
            /// </summary>
            public ProcessingState State { get; set; }

            /// <summary>
            /// Gets or sets the time the item was enqueued
            /// </summary>
            public DateTime EnqueueTime { get; set; }

            /// <summary>
            /// Gets or sets the time processing started for the item
            /// </summary>
            public DateTime? ProcessStartTime { get; set; }

            /// <summary>
            /// Gets or sets the time processing ended for the item
            /// </summary>
            public DateTime? ProcessEndTime { get; set; }

            /// <summary>
            /// Gets or sets the priority of the item (higher values are processed first)
            /// </summary>
            public int Priority { get; set; }

            /// <summary>
            /// Gets or sets the number of retry attempts for the item
            /// </summary>
            public int RetryCount { get; set; }
        }

        /// <summary>
        /// Represents an item in the queue with priority information
        /// </summary>
        private class PrioritizedItem
        {
            /// <summary>
            /// Gets or sets the key of the item
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// Gets or sets the priority of the item (higher values are processed first)
            /// </summary>
            public int Priority { get; set; }
        }

        /// <summary>
        /// Represents error information for an item
        /// </summary>
        private class ErrorInfo
        {
            /// <summary>
            /// Gets or sets the error message
            /// </summary>
            public string Message { get; set; }

            /// <summary>
            /// Gets or sets the error category
            /// </summary>
            public string Category { get; set; }

            /// <summary>
            /// Gets or sets the time the error occurred
            /// </summary>
            public DateTime Timestamp { get; set; }

            /// <summary>
            /// Gets or sets the exception that caused the error
            /// </summary>
            public Exception Exception { get; set; }
        }

        #endregion
    }
}