using BarTenderClone.Helpers;
using BarTenderClone.Models;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;

namespace BarTenderClone.Services
{
    public class PrintService : IPrintService
    {
        private readonly IZplGeneratorService _zplGenerator;
        private readonly ILoggingService _logger;
        private readonly IApiService _apiService;

        public PrintService(IZplGeneratorService zplGenerator, ILoggingService logger, IApiService apiService)
        {
            _zplGenerator = zplGenerator;
            _logger = logger;
            _apiService = apiService;
        }

        private PrinterConfiguration GetDefaultPrinterConfiguration()
        {
            return new PrinterConfiguration 
            { 
                Dpi = 203, 
                EnableUtf8 = true,
                Darkness = 15,
                PrintSpeed = 3
            };
        }

        public List<string> GetInstalledPrinters()
        {
            var printers = new List<string>();
            try
            {
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    printers.Add(printer);
                }
                _logger.LogInfo($"Found {printers.Count} installed printers");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to enumerate printers", ex);
            }
            return printers;
        }

        /// <summary>
        /// Generates sequential RFID data for multiple labels to prevent encoding conflicts
        /// </summary>
        private string GenerateSequentialRfidData(string baseRfidData, int labelNumber, int totalLabels)
        {
            if (string.IsNullOrWhiteSpace(baseRfidData))
                return $"{labelNumber:D16}"; // Fallback: use label number as hex

            // For hexadecimal format (most common with RFID):
            // Increment the last 4 hex characters to create unique RFID per label
            if (baseRfidData.All(c => "0123456789ABCDEFabcdef".Contains(c)))
            {
                if (baseRfidData.Length >= 4)
                {
                    string prefix = baseRfidData.Length > 4 ? baseRfidData.Substring(0, baseRfidData.Length - 4) : "";
                    string lastFour = baseRfidData.Substring(Math.Max(0, baseRfidData.Length - 4));

                    if (int.TryParse(lastFour, System.Globalization.NumberStyles.HexNumber, null, out int lastBytes))
                    {
                        // First label keeps original value, subsequent labels increment
                        int newValue = lastBytes + labelNumber - 1;
                        string newLastFour = newValue.ToString("X").PadLeft(4, '0');

                        // If overflow beyond 4 digits, take last 4
                        if (newLastFour.Length > 4)
                            newLastFour = newLastFour.Substring(newLastFour.Length - 4);

                        return $"{prefix}{newLastFour}";
                    }
                }
            }

            // Fallback: append label number as suffix
            return $"{baseRfidData}-{labelNumber}";
        }

        /// <summary>
        /// Creates a shallow clone of ResourceItem with overridden RFID data
        /// Used for sequential RFID encoding when printing multiple labels
        /// </summary>
        private ResourceItem CloneWithRfidOverride(ResourceItem original, string newRfidData)
        {
            var cloned = new ResourceItem
            {
                DocumentJson = original.DocumentJson,
                ParsedDocument = original.ParsedDocument != null ? new ResourceDocument
                {
                    Product = original.ParsedDocument.Product != null ? new ProductDto
                    {
                        Name = original.ParsedDocument.Product.Name,
                        ItemCode = original.ParsedDocument.Product.ItemCode,
                        MeasureUnit = original.ParsedDocument.Product.MeasureUnit,
                        Cost = original.ParsedDocument.Product.Cost,
                        CreationTime = original.ParsedDocument.Product.CreationTime
                    } : new ProductDto(),
                    ProductRfid = original.ParsedDocument.ProductRfid != null ? new ProductRfidDto
                    {
                        Rfid = newRfidData,  // Override with new RFID data
                        Branch = original.ParsedDocument.ProductRfid.Branch,
                        Status = original.ParsedDocument.ProductRfid.Status
                    } : new ProductRfidDto { Rfid = newRfidData }
                } : null
            };

            return cloned;
        }

        /// <summary>
        /// Prints exactly one label (quantity=1) and returns detailed result
        /// Used internally for per-label tracking
        /// </summary>
        private async Task<LabelResult> PrintSingleLabelAsync(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            string printerName,
            RfidConfiguration rfidConfig,
            int labelNumber,
            PrinterConfiguration config,
            string? overrideRfidData = null)
        {
            var labelResult = new LabelResult
            {
                LabelNumber = labelNumber,
                Timestamp = DateTime.Now
            };

            try
            {
                // Create temporary dataSource with overridden RFID if provided
                ResourceItem? effectiveDataSource = dataSource;
                if (overrideRfidData != null && dataSource != null)
                {
                    effectiveDataSource = CloneWithRfidOverride(dataSource, overrideRfidData);
                }

                // Generate ZPL with quantity=1 (single label)
                string zpl = _zplGenerator.GenerateZplWithRfid(
                    elements,
                    effectiveDataSource,
                    template,
                    rfidConfig,
                    config,
                    quantity: 1  // Always 1 for individual label
                );

                _logger.LogDebug($"Printing label {labelNumber}: ZPL generated");

                // Send to printer
                var (success, jobId) = RawPrinterHelper.SendStringToPrinterWithJobTracking(printerName, zpl);

                if (!success)
                {
                    labelResult.Success = false;
                    labelResult.ErrorMessage = "Failed to send label to spooler";
                    labelResult.ErrorType = PrintErrorType.SpoolerError;
                    _logger.LogError($"Label {labelNumber} failed to send to spooler");
                    return labelResult;
                }

                labelResult.JobId = jobId;
                labelResult.RfidEncoded = rfidConfig.EnableRfidEncoding;
                labelResult.RfidData = overrideRfidData ?? effectiveDataSource?.Rfid;

                _logger.LogDebug($"Label {labelNumber} sent to spooler with JobId={jobId}");

                // Wait for completion (Best Effort)
                try 
                {
                    var completionResult = await RawPrinterHelper.WaitForJobCompletionAsync(
                        printerName,
                        jobId,
                        timeoutMs: 30000 // Increased timeout for mobile printers like CP30
                    );

                    if (completionResult.Success)
                    {
                        labelResult.Success = true;
                        labelResult.ErrorMessage = null;
                        _logger.LogInfo($"Label {labelNumber} confirmed finished.");
                    }
                    else if (completionResult.ErrorType == PrintErrorType.SpoolerError)
                    {
                        // Tracking failed but print was sent - treat as success with warning
                        labelResult.Success = true; 
                        labelResult.ErrorMessage = "Print sent. Status tracking unavailable.";
                        _logger.LogWarning($"Label {labelNumber} tracking failed: {completionResult.ErrorMessage}. Print likely successful.");
                    }
                    else
                    {
                        // Hard failure
                        labelResult.Success = false;
                        labelResult.ErrorMessage = completionResult.ErrorMessage;
                        labelResult.ErrorType = completionResult.ErrorType;
                    }
                }
                catch (Exception trackEx)
                {
                    _logger.LogWarning($"Tracking logic crashed: {trackEx.Message}. Print likely successful.");
                    labelResult.Success = true;
                    labelResult.ErrorMessage = "Print sent. Tracking unavailable.";
                }

                if (labelResult.Success)
                {
                    _logger.LogInfo($"Label {labelNumber} completed successfully (JobId={jobId})");
                }
                else
                {
                    _logger.LogError($"Label {labelNumber} failed: {labelResult.ErrorMessage}");
                }

                return labelResult;
            }
            catch (Exception ex)
            {
                labelResult.Success = false;
                labelResult.ErrorMessage = $"Exception printing label {labelNumber}: {ex.Message}";
                labelResult.ErrorType = PrintErrorType.Unknown;
                _logger.LogError($"Label {labelNumber} print exception", ex);
                return labelResult;
            }
        }

        /// <summary>
        /// Prints label without RFID encoding (backward compatible)
        /// </summary>
        public async Task<PrintResult> PrintLabelAsync(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            string printerName,
            int quantity = 1)
        {
            return await Task.Run(() =>
            {
                var result = new PrintResult();

                try
                {
                    _logger.LogInfo($"Starting print job: Printer={printerName}, Quantity={quantity}");

                    // Validate printer exists
                    if (!GetInstalledPrinters().Contains(printerName))
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Printer '{printerName}' not found";
                        result.ErrorType = PrintErrorType.PrinterNotFound;
                        _logger.LogWarning(result.ErrorMessage);
                        return result;
                    }

                    // Generate ZPL
                    string zpl = _zplGenerator.GenerateZpl(elements, dataSource, template, GetDefaultPrinterConfiguration(), quantity);
                    _logger.LogDebug($"Generated ZPL:\n{zpl}");

                    // Send to printer with job tracking
                    var (success, jobId) = RawPrinterHelper.SendStringToPrinterWithJobTracking(printerName, zpl);

                    if (!success)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Failed to send print job to spooler";
                        result.ErrorType = PrintErrorType.SpoolerError;
                        _logger.LogError(result.ErrorMessage);
                        return result;
                    }

                    result.JobId = jobId;
                    _logger.LogInfo($"Print job sent successfully. JobId={jobId}");

                    // OPTIMISTIC STATUS CHECK
                    try 
                    {
                        var completionResult = RawPrinterHelper.WaitForJobCompletionAsync(
                            printerName,
                            jobId,
                            timeoutMs: 30000 // Increased timeout for mobile printers
                        ).GetAwaiter().GetResult();

                        if (completionResult.Success)
                        {
                             result.Success = true;
                             _logger.LogInfo($"Print job completed successfully. JobId={jobId}");
                        }
                        else if (completionResult.ErrorType == PrintErrorType.SpoolerError)
                        {
                             // Tracking failed but print was sent - treat as success with warning
                             result.Success = true;
                             result.ErrorMessage = "Print sent. Status tracking unavailable.";
                             _logger.LogWarning($"Print tracking failed: {completionResult.ErrorMessage}. Print likely successful.");
                        }
                        else
                        {
                             result.Success = false;
                             result.ErrorMessage = completionResult.ErrorMessage;
                             result.ErrorType = completionResult.ErrorType;
                             _logger.LogError($"Print job failed: {result.ErrorMessage}");
                        }
                    }
                    catch (Exception trackEx)
                    {
                        _logger.LogWarning($"Tracking crashed: {trackEx.Message}. Print likely successful.");
                        result.Success = true;
                        result.ErrorMessage = "Print sent. Tracking unavailable.";
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Print exception: {ex.Message}";
                    result.ErrorType = PrintErrorType.Unknown;
                    _logger.LogError("Print operation failed", ex);
                    return result;
                }
            });
        }

        /// <summary>
        /// Prints label WITH RFID encoding support and optional per-label tracking
        /// </summary>
        public async Task<PrintResult> PrintLabelWithRfidAsync(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            string printerName,
            RfidConfiguration rfidConfig,
            int quantity = 1,
            PrintOptions? options = null,
            PrinterConfiguration? config = null)
        {
            // Default options if not provided
            options ??= new PrintOptions();
            config ??= GetDefaultPrinterConfiguration();

            var result = new PrintResult();

            try
            {
                _logger.LogInfo($"Starting RFID print: Printer={printerName}, Quantity={quantity}, DetailedTracking={options.EnableDetailedTracking}, RFID={rfidConfig.EnableRfidEncoding}");

                // Validate printer
                if (!GetInstalledPrinters().Contains(printerName))
                {
                    result.Success = false;
                    result.ErrorMessage = $"Printer '{printerName}' not found";
                    result.ErrorType = PrintErrorType.PrinterNotFound;
                    _logger.LogWarning(result.ErrorMessage);
                    return result;
                }

                // Validate RFID data if encoding enabled
                if (rfidConfig.EnableRfidEncoding && dataSource != null)
                {
                    if (string.IsNullOrWhiteSpace(dataSource.Rfid))
                    {
                        result.Success = false;
                        result.ErrorMessage = "RFID data is empty but RFID encoding is enabled";
                        result.ErrorType = PrintErrorType.InvalidData;
                        _logger.LogWarning(result.ErrorMessage);
                        return result;
                    }
                }

                // DECISION POINT: Detailed tracking vs. legacy batch mode
                if (options.EnableDetailedTracking && quantity > 1)
                {
                    // NEW PATH: Per-label tracking
                    _logger.LogInfo($"Using detailed per-label tracking for {quantity} labels");
                    result.LabelResults = new List<LabelResult>();
                    int successCount = 0;

                    for (int i = 1; i <= quantity; i++)
                    {
                        // Generate sequential RFID data if RFID enabled
                        string? rfidDataForLabel = null;
                        if (rfidConfig.EnableRfidEncoding && dataSource != null)
                        {
                            rfidDataForLabel = GenerateSequentialRfidData(
                                dataSource.Rfid,
                                i,
                                quantity
                            );
                            _logger.LogInfo($"Label {i}/{quantity}: RFID={rfidDataForLabel}");
                        }

                        // Print single label
                        var labelResult = await PrintSingleLabelAsync(
                            elements,
                            dataSource,
                            template,
                            printerName,
                            rfidConfig,
                            i,
                            config,
                            rfidDataForLabel
                        );

                        result.LabelResults.Add(labelResult);

                        if (labelResult.Success)
                        {
                            successCount++;
                            _logger.LogInfo($"Label {i}/{quantity} succeeded (JobId={labelResult.JobId})");
                        }
                        else
                        {
                            _logger.LogError($"Label {i}/{quantity} failed: {labelResult.ErrorMessage}");

                            // Check stop-on-failure option
                            if (options.StopOnFirstFailure)
                            {
                                _logger.LogWarning($"Stopping print due to failure. {quantity - i} labels not attempted.");
                                break;
                            }
                        }

                        // Delay between labels (prevent overwhelming spooler)
                        if (i < quantity && options.DelayBetweenLabelsMs > 0)
                        {
                            await Task.Delay(options.DelayBetweenLabelsMs);
                        }
                    }

                    // Set overall result based on per-label results
                    result.Success = successCount > 0; // Partial success counts as success
                    result.RfidEncoded = rfidConfig.EnableRfidEncoding;
                    result.JobId = result.LabelResults.FirstOrDefault()?.JobId; // First job ID for reference

                    if (successCount == quantity)
                    {
                        _logger.LogInfo($"All {quantity} labels printed successfully");
                    }
                    else if (successCount > 0)
                    {
                        result.ErrorMessage = $"Partial success: {successCount}/{quantity} labels printed";
                        _logger.LogWarning(result.ErrorMessage);
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = $"All {quantity} labels failed";
                        result.ErrorType = result.LabelResults.FirstOrDefault()?.ErrorType ?? PrintErrorType.Unknown;
                        _logger.LogError(result.ErrorMessage);
                    }
                }
                else
                {
                    // LEGACY PATH: Use ^PQ command (original implementation)
                    _logger.LogInfo("Using legacy batch mode (^PQ command)");

                    string zpl = _zplGenerator.GenerateZplWithRfid(
                        elements,
                        dataSource,
                        template,
                        rfidConfig,
                        config,
                        quantity  // Uses ^PQ command internally
                    );
                    _logger.LogDebug($"Generated RFID ZPL:\n{zpl}");

                    var (success, jobId) = RawPrinterHelper.SendStringToPrinterWithJobTracking(printerName, zpl);

                    if (!success)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Failed to send RFID print job to spooler";
                        result.ErrorType = PrintErrorType.SpoolerError;
                        _logger.LogError(result.ErrorMessage);
                        return result;
                    }

                    result.JobId = jobId;
                    result.RfidEncoded = rfidConfig.EnableRfidEncoding;
                    _logger.LogInfo($"RFID print job sent. JobId={jobId}");

                    // OPTIMISTIC STATUS CHECK
                    try
                    {
                        var completionResult = await RawPrinterHelper.WaitForJobCompletionAsync(
                            printerName,
                            jobId,
                            timeoutMs: 30000 // Increased timeout for mobile printers
                        );

                        if (completionResult.Success)
                        {
                            result.Success = true;
                            _logger.LogInfo($"RFID print job completed. JobId={jobId}");
                        }
                        else if (completionResult.ErrorType == PrintErrorType.SpoolerError)
                        {
                            // Tracking failed but print was sent - treat as success with warning
                            result.Success = true;
                            result.ErrorMessage = "Print sent. Status tracking unavailable.";
                            _logger.LogWarning($"RFID print tracking failed: {completionResult.ErrorMessage}. Print likely successful.");
                        }
                        else
                        {
                            // Hard failure
                            result.Success = false;
                            result.ErrorMessage = completionResult.ErrorMessage;
                            result.ErrorType = completionResult.ErrorType;
                            _logger.LogError($"RFID print job failed: {result.ErrorMessage}");
                        }
                    }
                    catch (Exception trackEx)
                    {
                         _logger.LogWarning($"RFID print tracking crashed: {trackEx.Message}. Print likely successful.");
                         result.Success = true;
                         result.ErrorMessage = "Print sent. Tracking unavailable.";
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"RFID print exception: {ex.Message}";
                result.ErrorType = PrintErrorType.Unknown;
                _logger.LogError("RFID print operation failed", ex);
                return result;
            }
        }

        /// <summary>
        /// Prints batch WITHOUT RFID (backward compatible)
        /// </summary>
        public async Task<BatchPrintResult> PrintBatchAsync(
            IEnumerable<LabelElement> elements,
            IEnumerable<ResourceItem> dataSources,
            LabelTemplate template,
            string printerName,
            int quantityPerItem = 1)
        {
            var rfidConfig = new RfidConfiguration { EnableRfidEncoding = false };
            return await PrintBatchWithRfidAsync(elements, dataSources, template, printerName, rfidConfig, quantityPerItem);
        }

        /// <summary>
        /// Prints batch WITH RFID encoding - stops on first failure
        /// CRITICAL: This method processes items one-by-one with verification between each
        /// </summary>
        public async Task<BatchPrintResult> PrintBatchWithRfidAsync(
            IEnumerable<LabelElement> elements,
            IEnumerable<ResourceItem> dataSources,
            LabelTemplate template,
            string printerName,
            RfidConfiguration rfidConfig,
            int quantityPerItem = 1,
            PrintOptions? options = null,
            PrinterConfiguration? config = null)
        {
            options ??= new PrintOptions();
            config ??= GetDefaultPrinterConfiguration();

            var batchResult = new BatchPrintResult
            {
                StartTime = DateTime.Now,
                TotalItems = dataSources.Count()
            };

            try
            {
                _logger.LogInfo($"Starting batch print: {batchResult.TotalItems} items, Quantity={quantityPerItem}, RFID={rfidConfig.EnableRfidEncoding}, DetailedTracking={options.EnableDetailedTracking}");

                // Process each item sequentially
                foreach (var dataSource in dataSources)
                {
                    var itemResult = new ItemPrintResult
                    {
                        Item = dataSource,
                        QuantityRequested = quantityPerItem
                    };

                    // Print item with per-label tracking
                    var printResult = await PrintLabelWithRfidAsync(
                        elements,
                        dataSource,
                        template,
                        printerName,
                        rfidConfig,
                        quantityPerItem,
                        options,  // Pass options through
                        config
                    );

                    itemResult.Result = printResult;

                    // Copy detailed label results if available
                    if (printResult.HasDetailedTracking)
                    {
                        itemResult.LabelResults = printResult.LabelResults;
                    }

                    batchResult.ItemResults.Add(itemResult);

                    // Use computed QuantitySucceeded property for accurate counts
                    if (printResult.Success)
                    {
                        batchResult.SuccessCount++;
                        _logger.LogInfo($"Batch item {batchResult.SuccessCount}/{batchResult.TotalItems} succeeded: {dataSource.ProductName} ({printResult.LabelsSucceeded}/{quantityPerItem} labels)");
                    }
                    else
                    {
                        batchResult.FailureCount++;
                        _logger.LogError($"Batch item failed: {dataSource.ProductName} - {printResult.ErrorMessage}");

                        // CRITICAL: Stop batch on first failure (as per requirements)
                        _logger.LogWarning($"Stopping batch print due to failure. {batchResult.FailureCount} failed, {batchResult.TotalItems - batchResult.SuccessCount - batchResult.FailureCount} not attempted");
                        break;
                    }

                    // Small delay between items to avoid overwhelming printer
                    if (rfidConfig.EnableRfidEncoding && options.DelayBetweenLabelsMs > 0)
                    {
                        await Task.Delay(options.DelayBetweenLabelsMs);
                    }
                }

                batchResult.EndTime = DateTime.Now;
                _logger.LogInfo($"Batch print completed: Success={batchResult.SuccessCount}, Failed={batchResult.FailureCount}, Duration={(batchResult.EndTime - batchResult.StartTime).TotalSeconds:F1}s");

                return batchResult;
            }
            catch (Exception ex)
            {
                batchResult.EndTime = DateTime.Now;
                _logger.LogError("Batch print operation failed", ex);
                return batchResult;
            }
        }

        /// <summary>
        /// Legacy method with error handling
        /// </summary>
        public async Task<PrintResult> PrintLabelAsync(ResourceItem item, string printerName, int quantity = 1)
        {
            return await Task.Run(() =>
            {
                var result = new PrintResult();

                try
                {
                    _logger.LogInfo($"Starting legacy print: {item.ProductName}");

                    string zpl = GenerateLegacyZpl(item, quantity);

                    var (success, jobId) = RawPrinterHelper.SendStringToPrinterWithJobTracking(printerName, zpl);

                    if (!success)
                    {
                        result.Success = false;
                        result.ErrorMessage = "Failed to send legacy print job";
                        result.ErrorType = PrintErrorType.SpoolerError;
                        return result;
                    }

                    result.JobId = jobId;
                    result.Success = true;
                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    result.ErrorType = PrintErrorType.Unknown;
                    _logger.LogError("Legacy print failed", ex);
                    return result;
                }
            });
        }

        private string GenerateLegacyZpl(ResourceItem item, int quantity)
        {
            string productName = item.ProductName ?? "Unknown Product";
            if (productName.Length > 40) productName = productName.Substring(0, 40);

            string price = item.Price.ToString("N0") + " MNT";
            string code = item.Rfid ?? item.Code ?? "0000";

            return $@"
^XA
^FO50,50^A0N,40,40^FD{productName}^FS
^FO50,100^A0N,30,30^FDPrice: {price}^FS
^FO50,150^BY2,2,80^BCN,80,Y,N,N^FD{code}^FS
^PQ{quantity}
^XZ
";
        }
    }
}
