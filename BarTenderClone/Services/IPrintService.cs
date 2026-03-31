using BarTenderClone.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public interface IPrintService
    {
        List<string> GetInstalledPrinters();

        /// <summary>
        /// Prints a label using dynamically generated ZPL from canvas elements.
        /// </summary>
        Task<PrintResult> PrintLabelAsync(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            string printerName,
            int quantity = 1);

        /// <summary>
        /// Prints multiple labels in a batch using dynamically generated ZPL.
        /// Each data source gets its own label with the specified quantity.
        /// </summary>
        Task<BatchPrintResult> PrintBatchAsync(
            IEnumerable<LabelElement> elements,
            IEnumerable<ResourceItem> dataSources,
            LabelTemplate template,
            string printerName,
            int quantityPerItem = 1);

        /// <summary>
        /// Prints a label with RFID encoding support
        /// </summary>
        Task<PrintResult> PrintLabelWithRfidAsync(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            string printerName,
            RfidConfiguration rfidConfig,
            int quantity = 1,
            PrintOptions? options = null,
            PrinterConfiguration? config = null);

        /// <summary>
        /// Prints multiple labels in a batch with RFID encoding.
        /// Stops on first failure.
        /// </summary>
        Task<BatchPrintResult> PrintBatchWithRfidAsync(
            IEnumerable<LabelElement> elements,
            IEnumerable<ResourceItem> dataSources,
            LabelTemplate template,
            string printerName,
            RfidConfiguration rfidConfig,
            int quantityPerItem = 1,
            PrintOptions? options = null,
            PrinterConfiguration? config = null);

        /// <summary>
        /// Legacy method: Prints a label with hardcoded ZPL template.
        /// Kept for backward compatibility.
        /// </summary>
        Task<PrintResult> PrintLabelAsync(ResourceItem item, string printerName, int quantity = 1);
    }
}
