using BarTenderClone.Models;
using System.Collections.Generic;

namespace BarTenderClone.Services
{
    /// <summary>
    /// Service interface for generating ZPL (Zebra Programming Language) commands
    /// from label designer elements.
    /// </summary>
    public interface IZplGeneratorService
    {
        /// <summary>
        /// Generates ZPL commands from a collection of label elements.
        /// </summary>
        /// <param name="elements">Collection of label elements (text, barcode, etc.)</param>
        /// <param name="dataSource">Optional data source for field binding (e.g., selected product)</param>
        /// <param name="template">Label template containing size information</param>
        /// <param name="config">Printer configuration (DPI, etc.)</param>
        /// <param name="quantity">Number of labels to print</param>
        /// <returns>Complete ZPL command string ready to send to printer</returns>
        string GenerateZpl(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            PrinterConfiguration? config = null,
            int quantity = 1);

        /// <summary>
        /// Generates ZPL commands for multiple labels in a batch.
        /// Each data source gets its own label with the specified quantity.
        /// </summary>
        /// <param name="elements">Collection of label elements (text, barcode, etc.)</param>
        /// <param name="dataSources">Collection of data sources for field binding</param>
        /// <param name="template">Label template containing size information</param>
        /// <param name="config">Printer configuration (DPI, etc.)</param>
        /// <param name="quantityPerItem">Number of copies to print for each item</param>
        /// <returns>Complete ZPL command string containing all labels</returns>
        string GenerateBatchZpl(
            IEnumerable<LabelElement> elements,
            IEnumerable<ResourceItem> dataSources,
            LabelTemplate template,
            PrinterConfiguration? config = null,
            int quantityPerItem = 1);

        /// <summary>
        /// Generates ZPL commands with RFID encoding support
        /// </summary>
        string GenerateZplWithRfid(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            RfidConfiguration rfidConfig,
            PrinterConfiguration? config = null,
            int quantity = 1);

        /// <summary>
        /// Generates batch ZPL with RFID encoding for each label
        /// </summary>
        string GenerateBatchZplWithRfid(
            IEnumerable<LabelElement> elements,
            IEnumerable<ResourceItem> dataSources,
            LabelTemplate template,
            RfidConfiguration rfidConfig,
            PrinterConfiguration? config = null,
            int quantityPerItem = 1);
    }
}
