// QuickTechSystems.Application/Services/Interfaces/IBulkOperationQueueService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QuickTechSystems.Application.DTOs;

namespace QuickTechSystems.Application.Services.Interfaces
{
    /// <summary>
    /// Service interface for bulk operation processing with enhanced error handling and state management
    /// </summary>
    public interface IBulkOperationQueueService
    {
        /// <summary>
        /// Enqueues a list of items for bulk processing
        /// </summary>
        /// <param name="items">The items to process</param>
        /// <param name="priority">Optional priority level (higher processes first)</param>
        void EnqueueItems(List<MainStockDTO> items, int priority = 0);

        /// <summary>
        /// Gets the processing state for all items
        /// </summary>
        /// <returns>Dictionary mapping item keys to their processing states</returns>
        Dictionary<string, ProcessingState> GetAllStatus();

        /// <summary>
        /// Gets all items that completed processing successfully
        /// </summary>
        /// <returns>List of successfully processed items</returns>
        List<MainStockDTO> GetCompletedItems();

        /// <summary>
        /// Gets all items that failed during processing with their error messages
        /// </summary>
        /// <returns>List of tuples containing failed items and their error messages</returns>
        List<Tuple<MainStockDTO, string>> GetFailedItems();

        /// <summary>
        /// Gets error details grouped by error category for batch resolution
        /// </summary>
        /// <returns>Dictionary mapping error categories to affected items</returns>
        Dictionary<string, List<Tuple<MainStockDTO, string>>> GetErrorsByCategory();

        /// <summary>
        /// Gets whether the service is currently processing items
        /// </summary>
        bool IsProcessing { get; }

        /// <summary>
        /// Gets the number of items waiting to be processed
        /// </summary>
        int QueuedItemCount { get; }

        /// <summary>
        /// Gets the total number of items in the system (queued + processing + completed + failed)
        /// </summary>
        int TotalItemCount { get; }

        /// <summary>
        /// Gets the number of items that completed processing successfully
        /// </summary>
        int CompletedItemCount { get; }

        /// <summary>
        /// Gets the number of items that failed during processing
        /// </summary>
        int FailedItemCount { get; }

        /// <summary>
        /// Cancels all ongoing processing
        /// </summary>
        void CancelProcessing();

        /// <summary>
        /// Retries processing for all failed items
        /// </summary>
        /// <returns>Task representing the retry operation</returns>
        Task RetryFailedItemsAsync();

        /// <summary>
        /// Clears the processing queue and resets all status
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Represents the current state of an item in the processing pipeline
    /// </summary>
    public enum ProcessingState
    {
        /// <summary>
        /// Item is waiting to be processed
        /// </summary>
        Queued,

        /// <summary>
        /// Item is currently being processed
        /// </summary>
        Processing,

        /// <summary>
        /// Item was processed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Item processing failed
        /// </summary>
        Failed,

        /// <summary>
        /// Item is waiting to be retried after a failure
        /// </summary>
        PendingRetry,

        /// <summary>
        /// Item was cancelled before processing completed
        /// </summary>
        Cancelled
    }
}