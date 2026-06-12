using System;

namespace BarTenderClone.Models
{
    public class PrintHistoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime PrintDate { get; set; } = DateTime.Now;
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string RfidData { get; set; } = string.Empty;
        public int QuantityRequested { get; set; }
        public int QuantitySucceeded { get; set; }
        public bool Success => QuantitySucceeded == QuantityRequested && QuantityRequested > 0;
        public string PrinterName { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
