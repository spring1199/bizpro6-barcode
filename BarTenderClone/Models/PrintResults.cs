using System;
using System.Collections.Generic;
using System.Linq;

namespace BarTenderClone.Models
{
    /// <summary>
    /// Represents the result of a print operation
    /// </summary>
    public class PrintResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public PrintErrorType ErrorType { get; set; }
        public int? JobId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // RFID-specific
        public bool? RfidEncoded { get; set; }
        public string? RfidVerificationData { get; set; }

        /// <summary>
        /// Detailed results for each label when quantity > 1
        /// NULL for legacy single-label prints or when detailed tracking is disabled
        /// </summary>
        public List<LabelResult>? LabelResults { get; set; }

        /// <summary>
        /// Indicates if this result includes per-label tracking
        /// </summary>
        public bool HasDetailedTracking => LabelResults != null && LabelResults.Any();

        /// <summary>
        /// Count of successfully printed labels (for detailed tracking)
        /// </summary>
        public int LabelsSucceeded => LabelResults?.Count(l => l.Success) ?? (Success ? 1 : 0);

        /// <summary>
        /// Count of failed labels (for detailed tracking)
        /// </summary>
        public int LabelsFailed => LabelResults?.Count(l => !l.Success) ?? (Success ? 0 : 1);
    }

    /// <summary>
    /// Types of print errors that can occur
    /// </summary>
    public enum PrintErrorType
    {
        None,
        PrinterNotFound,
        SpoolerError,
        RfidEncodingFailed,
        RfidVerificationFailed,
        CommunicationError,
        InvalidData,
        Unknown
    }

    /// <summary>
    /// Batch print result with per-item tracking
    /// </summary>
    public class BatchPrintResult
    {
        public int TotalItems { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<ItemPrintResult> ItemResults { get; set; } = new();
        public bool AllSucceeded => FailureCount == 0;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }

    /// <summary>
    /// Result for individual item within a batch
    /// </summary>
    public class ItemPrintResult
    {
        public ResourceItem Item { get; set; } = new ResourceItem();
        public int QuantityRequested { get; set; }

        /// <summary>
        /// Number of labels successfully printed for this item
        /// Uses detailed tracking if available, otherwise falls back to Result.Success
        /// </summary>
        public int QuantitySucceeded =>
            LabelResults?.Count(l => l.Success) ?? (Result.Success ? QuantityRequested : 0);

        public PrintResult Result { get; set; } = new PrintResult();

        /// <summary>
        /// Detailed results for each label of this item
        /// NULL if detailed tracking is not enabled
        /// </summary>
        public List<LabelResult>? LabelResults { get; set; }
    }

    /// <summary>
    /// Result for a single label within a quantity (e.g., Label 1 of 2)
    /// </summary>
    public class LabelResult
    {
        /// <summary>
        /// Label number within the quantity (1-based: 1, 2, 3...)
        /// </summary>
        public int LabelNumber { get; set; }

        /// <summary>
        /// Success status for this specific label
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if this label failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error type if this label failed
        /// </summary>
        public PrintErrorType ErrorType { get; set; }

        /// <summary>
        /// Windows spooler Job ID for this specific label
        /// </summary>
        public int? JobId { get; set; }

        /// <summary>
        /// RFID encoding status for this label
        /// </summary>
        public bool? RfidEncoded { get; set; }

        /// <summary>
        /// RFID data that was encoded on this label
        /// </summary>
        public string? RfidData { get; set; }

        /// <summary>
        /// Timestamp when this label was processed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Options to control print behavior and tracking granularity
    /// </summary>
    public class PrintOptions
    {
        /// <summary>
        /// Enable detailed per-label tracking (recommended for quantities > 1)
        /// When false, uses legacy batch mode with ^PQ command (faster but no per-label status)
        /// </summary>
        public bool EnableDetailedTracking { get; set; } = true;

        /// <summary>
        /// Stop printing remaining labels on first failure
        /// When false, continues printing all labels even if some fail
        /// </summary>
        public bool StopOnFirstFailure { get; set; } = false;

        /// <summary>
        /// Delay between individual label print jobs (milliseconds)
        /// Prevents overwhelming printer/spooler with rapid submissions
        /// Recommended: 100-500ms for RFID encoding, 0-100ms for barcode-only
        /// </summary>
        public int DelayBetweenLabelsMs { get; set; } = 200;

        /// <summary>
        /// Maximum parallel label submissions
        /// 1 = sequential (safest for RFID), 2-4 = parallel (faster for barcode-only)
        /// </summary>
        public int MaxParallelLabels { get; set; } = 1;
    }
}
