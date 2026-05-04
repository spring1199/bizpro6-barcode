using BarTenderClone.Models;
using BarTenderClone.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BarTenderClone.Services
{
    /// <summary>
    /// Production-ready ZPL generator supporting variable DPI and encoding.
    /// Converts canvas elements to ZPL commands with proper coordinate conversion.
    /// </summary>
    public class ZplGeneratorService : IZplGeneratorService
    {

        /// <summary>
        /// Generates ZPL commands from label elements with field binding support.
        /// </summary>
        public string GenerateZpl(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            PrinterConfiguration? config = null,
            int quantity = 1)
        {
            config ??= new PrinterConfiguration(); // Use default if null
            return GenerateZplInternal(elements, dataSource, template, config, quantity, null);
        }

        /// <summary>
        /// Generates ZPL commands for multiple labels in a batch.
        /// </summary>
        public string GenerateBatchZpl(
            IEnumerable<LabelElement> elements,
            IEnumerable<ResourceItem> dataSources,
            LabelTemplate template,
            PrinterConfiguration? config = null,
            int quantityPerItem = 1)
        {
            config ??= new PrinterConfiguration();
            var zpl = new StringBuilder();

            foreach (var dataSource in dataSources)
            {
                zpl.Append(GenerateZplInternal(elements, dataSource, template, config, quantityPerItem, null));
            }

            return zpl.ToString();
        }

        /// <summary>
        /// Generates ZPL with RFID encoding commands
        /// </summary>
        public string GenerateZplWithRfid(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            RfidConfiguration rfidConfig,
            PrinterConfiguration? config = null,
            int quantity = 1)
        {
            config ??= new PrinterConfiguration();
            return GenerateZplInternal(elements, dataSource, template, config, quantity, rfidConfig);
        }

        /// <summary>
        /// Generates batch ZPL with RFID encoding
        /// </summary>
        public string GenerateBatchZplWithRfid(
            IEnumerable<LabelElement> elements,
            IEnumerable<ResourceItem> dataSources,
            LabelTemplate template,
            RfidConfiguration rfidConfig,
            PrinterConfiguration? config = null,
            int quantityPerItem = 1)
        {
            config ??= new PrinterConfiguration();
            var zpl = new StringBuilder();

            foreach (var dataSource in dataSources)
            {
                zpl.Append(GenerateZplInternal(elements, dataSource, template, config, quantityPerItem, rfidConfig));
            }

            return zpl.ToString();
        }

        /// <summary>
        /// Internal implementation to handle common logic
        /// </summary>
        private string GenerateZplInternal(
            IEnumerable<LabelElement> elements,
            ResourceItem? dataSource,
            LabelTemplate template,
            PrinterConfiguration config,
            int quantity,
            RfidConfiguration? rfidConfig)
        {
            var zpl = new StringBuilder();

            // Start ZPL format
            zpl.AppendLine("^XA");

            // Encoding Support
            if (config.EnableUtf8)
            {
                zpl.AppendLine("^CI28"); // Enable UTF-8 encoding
            }

            // 1. RFID Setup (Must be at the very top for some mobile printers)
            if (rfidConfig != null && rfidConfig.EnableRfidEncoding)
            {
                // ^RS: [antenna],[read_pwr],[write_pwr],[retries],[error_action]
                // Setting retries to 1 to prevent paper waste.
                // Error Action 'N' (None/Skip) stops the printer from VOID-ing labels.
                zpl.AppendLine($"^RS1,25,27,1,N");
            }

            // 2. Media & Mode Settings
            zpl.AppendLine("^MMT"); // Media Mode: Tear-off
            zpl.AppendLine("^MNA"); // Media Tracking: Auto
            // Media Type: Use config setting (Direct Thermal or Thermal Transfer)
            string mediaTypeCmd = config.MediaType == MediaType.ThermalTransfer ? "^MTT" : "^MTD";
            zpl.AppendLine(mediaTypeCmd);
            zpl.AppendLine("^PON"); // Print Orientation: Normal

            // Note: Darkness (^MD) and Speed (^PR) are removed to let printer defaults take over.

            // Set label dimensions dynamically based on DPI
            int labelWidthDots = ConvertPositionToDots(template.Width, config.Dpi);
            int labelHeightDots = ConvertPositionToDots(template.Height, config.Dpi);

            // Fallback safety - use default 54mm x 34mm
            if (labelWidthDots == 0)
            {
                double defaultWidth54mm = LabelSizeHelper.InchesToScreenPixels(LabelSizeHelper.MmToInches(54));
                labelWidthDots = ConvertPositionToDots(defaultWidth54mm, config.Dpi);
            }
            if (labelHeightDots == 0)
            {
                double defaultHeight34mm = LabelSizeHelper.InchesToScreenPixels(LabelSizeHelper.MmToInches(34));
                labelHeightDots = ConvertPositionToDots(defaultHeight34mm, config.Dpi);
            }

            zpl.AppendLine($"^PW{labelWidthDots}");
            zpl.AppendLine($"^LL{labelHeightDots}");

            // 3. Label Home (^LH) - Global offset to center labels on printer
            // This shifts ALL content by the specified amount (fixes left-aligned printing)
            int homeX = LabelSizeHelper.MmToDots(LabelSizeHelper.LABEL_HOME_X_MM, config.Dpi);
            int homeY = LabelSizeHelper.MmToDots(LabelSizeHelper.LABEL_HOME_Y_MM, config.Dpi);
            zpl.AppendLine($"^LH{homeX},{homeY}");



            // Process each element
            var margins = LabelSizeHelper.GetSafeMarginsDots(config.Dpi);

            foreach (var element in elements.OrderBy(e => e.Y).ThenBy(e => e.X))
            {
                switch (element.Type)
                {
                    case ElementType.Text:
                        zpl.AppendLine(GenerateTextElement(element, dataSource, config.Dpi, config.DefaultFont, margins));
                        break;

                    case ElementType.Barcode:
                        // ALWAYS handle visual barcode here - even if it's the RFID field
                        zpl.AppendLine(GenerateBarcodeElement(element, dataSource, config.Dpi, margins));
                        break;

                    case ElementType.QRCode:
                        zpl.AppendLine(GenerateQrCodeElement(element, dataSource, config.Dpi, margins));
                        break;

                    case ElementType.Image:
                        // Future enhancement
                        break;
                }
            }

            // RFID Encoding (Standard Practice)
            if (rfidConfig != null && rfidConfig.EnableRfidEncoding)
            {
                var rfidElement = elements.FirstOrDefault(e => e.Type == ElementType.Barcode &&
                                                           e.FieldName?.Equals("RFID", StringComparison.OrdinalIgnoreCase) == true);
                if (rfidElement != null)
                {
                    // Normal path: template has a barcode element with FieldName="RFID"
                    zpl.Append(GenerateRfidEncoding(rfidElement, dataSource, rfidConfig));
                }
                else if (dataSource != null && !string.IsNullOrWhiteSpace(dataSource.Rfid))
                {
                    // Fallback: "New" blank template has no RFID element, encode directly from data source
                    // ^RFW has no X/Y position so it works regardless of visual layout
                    zpl.Append(GenerateRfidEncodingFromData(dataSource.Rfid, rfidConfig));
                }
            }

            // Set print quantity (At the VERY END before ^XZ)
            zpl.AppendLine($"^PQ{quantity}");

            // End ZPL format
            zpl.AppendLine("^XZ");

            return zpl.ToString();
        }

        private string GenerateTextElement(LabelElement element, ResourceItem? dataSource, int dpi, string fontName, (int left, int top, int right, int bottom) margins)
        {
            int x = ConvertPositionToDots(element.X, dpi);
            int y = ConvertPositionToDots(element.Y, dpi);
            int width = Math.Max(ConvertPositionToDots(element.Width, dpi), LabelSizeHelper.MmToDots(1, dpi));

            // Zebra resident fonts do not have a true bold flag. A wider font box is the
            // closest deterministic approximation to the bold preview.
            int fontHeight = LabelSizeHelper.FontSizeToZplHeight(element.FontSize, dpi);
            int fontWidth = element.IsBold
                ? Math.Max(fontHeight, LabelSizeHelper.FontSizeToZplWidth(element.FontSize, dpi))
                : LabelSizeHelper.FontSizeToZplWidth(element.FontSize, dpi);

            string content = ResolveFieldValue(element, dataSource);
            content = SanitizeZplContent(content);

            // When a text element has no explicit height, let it wrap naturally instead of
            // forcing a print-only 3-line cap that the preview never showed.
            int elementHeightDots = ConvertPositionToDots(element.Height, dpi);
            int maxLines = (fontHeight > 0 && elementHeightDots > 0)
                ? Math.Max(1, elementHeightDots / fontHeight)
                : 10;
            maxLines = Math.Clamp(maxLines, 1, 10);

            if (element.IsCentered)
            {
                return $"^FO{x},{y}^A{fontName}N,{fontHeight},{fontWidth}^FB{width},{maxLines},0,C,0^FD{content}^FS";
            }
            else
            {
                return $"^FO{x},{y}^A{fontName}N,{fontHeight},{fontWidth}^FB{width},{maxLines},0,L,0^FD{content}^FS";
            }
        }

        private string GenerateBarcodeElement(LabelElement element, ResourceItem? dataSource, int dpi, (int left, int top, int right, int bottom) margins)
        {
            int y = ConvertPositionToDots(element.Y, dpi);
            int height = ConvertPositionToDots(element.Height, dpi);

            // Ensure minimum height for readability
            height = Math.Max(height, LabelSizeHelper.MmToDots(5, dpi)); // Min 5mm

            double ratio = 2.5; // Bar width to height ratio for Code 128

            string data = ResolveFieldValue(element, dataSource);
            data = SanitizeBarcodeData(data);

            var layout = LabelSizeHelper.CalculateCode128Layout(data, element.Width, dpi);
            int elementWidthDots = ConvertPositionToDots(element.Width, dpi);
            int actualBarcodeWidth = layout.ActualWidthDots;
            int moduleWidth = layout.ModuleWidthDots;

            int x;
            if (element.IsCentered)
            {
                // Center barcode within element's defined X position and width
                int elementX = ConvertPositionToDots(element.X, dpi);

                // Calculate centered X position within the element's bounding box
                x = elementX + Math.Max(0, (elementWidthDots - actualBarcodeWidth) / 2);
            }
            else
            {
                x = ConvertPositionToDots(element.X, dpi);
            }

            // Apply unified barcode offset (same for all DPI)
            var barcodeOffsetMm = LabelSizeHelper.BARCODE_X_OFFSET_MM;
            if (barcodeOffsetMm != 0)
            {
                int offsetDots = LabelSizeHelper.MmToDots(barcodeOffsetMm, dpi);
                x = Math.Max(0, x + offsetDots);
            }

            return $"^FO{x},{y}^BY{moduleWidth},{ratio},{height}^BCN,{height},N,N,N^FD{data}^FS";
        }

        /// <summary>
        /// Calculates the required quiet zone width in dots for a given barcode type.
        /// Per GS1 standards, Code 128 requires 10x the module width on each side.
        /// </summary>
        private int CalculateQuietZone(int moduleWidth, string barcodeType)
        {
            return barcodeType switch
            {
                "Code128" => moduleWidth * 10,     // GS1 standard: 10x module width
                "EAN13" or "UPCA" => moduleWidth * 11,  // GS1 standard: 11x for EAN/UPC
                "QRCode" => moduleWidth * 4,       // 4 modules quiet zone
                _ => moduleWidth * 10              // Safe default
            };
        }

        private string ResolveFieldValue(LabelElement element, ResourceItem? dataSource)
        {
            return LabelFieldValueResolver.ResolveVisualValue(element, dataSource);
        }

        /// <summary>
        /// Converts screen pixels to printer dots for position and dimension values.
        /// Delegates to LabelSizeHelper for consistent conversion across the application.
        /// </summary>
        private int ConvertPositionToDots(double pixels, int dpi)
        {
            return LabelSizeHelper.ScreenPixelsToDots(Math.Max(0, pixels), dpi);
        }

        private string SanitizeZplContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            content = content.Replace("^", "~5E");
            if (content.Length > 100) content = content.Substring(0, 100);
            return content;
        }

        private string SanitizeBarcodeData(string data)
        {
            if (string.IsNullOrEmpty(data)) return "0000";
            var sanitized = new string(data.Where(c =>
                char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' || c == ' ').ToArray());
            if (string.IsNullOrEmpty(sanitized)) return "0000";
            if (sanitized.Length > 50) sanitized = sanitized.Substring(0, 50);
            return sanitized;
        }

        private string GenerateRfidEncoding(LabelElement element, ResourceItem? dataSource, RfidConfiguration config)
        {
            string rfidData = LabelFieldValueResolver.ResolveRawValue(element, dataSource);
            return GenerateRfidEncodingFromData(rfidData, config);
        }

        /// <summary>
        /// Core RFID encoding logic — does not require a visual LabelElement.
        /// Used both by element-based encoding and the fallback path for blank templates.
        /// </summary>
        private string GenerateRfidEncodingFromData(string rfidData, RfidConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(rfidData)) rfidData = "0000000000000000";

            var formatChar = config.DataFormat switch
            {
                RfidDataFormat.ASCII => "A",
                RfidDataFormat.Hexadecimal => "H",
                RfidDataFormat.EPC => "E",
                _ => "H"
            };

            if (config.DataFormat == RfidDataFormat.Hexadecimal)
            {
                rfidData = SanitizeHexData(rfidData);
                if (rfidData.Length % 2 != 0) rfidData = "0" + rfidData;
            }

            int byteCount = rfidData.Length / 2;
            // Explicit format: ^RFW,[format],[block],[bytes],[master]
            return $"^RFW,{formatChar},{config.StartingBlock},{byteCount},1^FD{rfidData}^FS\r\n";
        }

        private string SanitizeHexData(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return "0000000000000000";
            var hexChars = "0123456789ABCDEFabcdef";
            var sanitized = new string(data.Where(c => hexChars.Contains(c)).ToArray());
            sanitized = sanitized.ToUpperInvariant();
            if (sanitized.Length < 16) sanitized = sanitized.PadLeft(16, '0');
            if (sanitized.Length > 48) sanitized = sanitized.Substring(0, 48);
            return sanitized;
        }

        /// <summary>
        /// Strips leading zeros from an RFID string for human-readable display on printed labels.
        /// Does NOT affect RFID encoding — only used for text/barcode visual elements.
        /// Follows chipmo2 pattern: fixedRfid = rfid.replace(/^0+/, '')
        /// </summary>
        internal static string StripLeadingZerosForDisplay(string rfid)
        {
            return LabelFieldValueResolver.StripLeadingZerosForDisplay(rfid);
        }

        /// <summary>
        /// Generates ZPL commands for a QR Code element.
        /// Uses ^BQ command for QR code generation on Zebra printers.
        /// </summary>
        private string GenerateQrCodeElement(LabelElement element, ResourceItem? dataSource, int dpi, (int left, int top, int right, int bottom) margins)
        {
            int x = ConvertPositionToDots(element.X, dpi);
            int y = ConvertPositionToDots(element.Y, dpi);

            string data = ResolveFieldValue(element, dataSource);
            data = SanitizeQrCodeData(data);

            // Calculate magnification based on element height
            // QR Code module size = magnification factor (1-10)
            int heightDots = ConvertPositionToDots(element.Height, dpi);
            int magnification = Math.Clamp(heightDots / 25, 2, 10); // Reasonable scale

            // ^BQ command: ^BQo,m,n
            // o = orientation (N = normal)
            // m = model (2 = enhanced, recommended)
            // n = magnification factor (1-10)
            // Error correction is automatic (M level)
            return $"^FO{x},{y}^BQN,2,{magnification}^FDMA,{data}^FS";
        }

        private string SanitizeQrCodeData(string data)
        {
            if (string.IsNullOrEmpty(data)) return "N/A";
            // QR codes support alphanumeric, so we allow more characters
            if (data.Length > 100) data = data.Substring(0, 100);
            // Escape special ZPL characters
            data = data.Replace("^", "~5E");
            return data;
        }
    }
}
