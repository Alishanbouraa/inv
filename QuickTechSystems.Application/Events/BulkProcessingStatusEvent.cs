// QuickTechSystems.Application/Events/BulkProcessingStatusEvent.cs
using System;
using System.Collections.Generic;

namespace QuickTechSystems.Application.Events
{
    /// <summary>
    /// Event for reporting bulk processing status updates
    /// </summary>
    public class BulkProcessingStatusEvent
    {
        /// <summary>
        /// Gets or sets the number of queued items
        /// </summary>
        public int QueuedItems { get; set; }

        /// <summary>
        /// Gets or sets the number of completed items
        /// </summary>
        public int CompletedItems { get; set; }

        /// <summary>
        /// Gets or sets the number of failed items
        /// </summary>
        public int FailedItems { get; set; }

        /// <summary>
        /// Gets or sets the total number of items
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Gets or sets whether processing is completed
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets whether this is a completion message (final status update)
        /// </summary>
        public bool IsCompletionMessage { get; set; }

        /// <summary>
        /// Gets or sets the elapsed time for processing
        /// </summary>
        public TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Gets or sets the error categories with counts
        /// </summary>
        public Dictionary<string, int> ErrorCategories { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets the overall progress percentage (0-100)
        /// </summary>
        public int ProgressPercentage => TotalItems > 0
            ? (int)(((CompletedItems + FailedItems) / (double)TotalItems) * 100)
            : 0;

        /// <summary>
        /// Gets the success percentage (0-100)
        /// </summary>
        public int SuccessPercentage => (CompletedItems + FailedItems) > 0
            ? (int)((CompletedItems / (double)(CompletedItems + FailedItems)) * 100)
            : 0;
    }
}