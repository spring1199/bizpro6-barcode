using System.Collections.Generic;

namespace BarTenderClone.Models
{
    /// <summary>
    /// Data Transfer Object for label template serialization.
    /// Excludes UI-specific properties to prevent serialization issues.
    /// </summary>
    public class LabelTemplateDto
    {
        /// <summary>
        /// Template name displayed to user
        /// </summary>
        public string Name { get; set; } = "New Template";

        /// <summary>
        /// Canvas width in pixels (96 DPI)
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Canvas height in pixels (96 DPI)
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Template width in inches for printing
        /// </summary>
        public double WidthInches { get; set; }

        /// <summary>
        /// Template height in inches for printing
        /// </summary>
        public double HeightInches { get; set; }

        /// <summary>
        /// Collection of label elements
        /// </summary>
        public List<LabelElementDto> Elements { get; set; } = new();
    }

    /// <summary>
    /// Data Transfer Object for label element serialization.
    /// Excludes IsSelected property which is UI state only.
    /// </summary>
    public class LabelElementDto
    {
        /// <summary>
        /// X position on canvas
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y position on canvas
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Element width (used for barcodes)
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Element height (used for barcodes)
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Static content or default value
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Field name for data binding (e.g., "ProductName", "RFID")
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Element type (Text, Barcode, Image)
        /// </summary>
        public ElementType Type { get; set; }

        /// <summary>
        /// Font size for text elements
        /// </summary>
        public double FontSize { get; set; }

        /// <summary>
        /// Bold flag for text elements
        /// </summary>
        public bool IsBold { get; set; }

        /// <summary>
        /// Horizontal centering flag used by text and barcode rendering.
        /// </summary>
        public bool IsCentered { get; set; }

        /// <summary>
        /// Element rotation in degrees. Supported values: 0, 90, 180, 270.
        /// </summary>
        public int RotationDegrees { get; set; }

        /// <summary>
        /// Embedded PNG/JPEG/BMP image data for image elements.
        /// </summary>
        public string ImageDataBase64 { get; set; } = string.Empty;

        /// <summary>
        /// MIME type for embedded image data.
        /// </summary>
        public string ImageMimeType { get; set; } = string.Empty;

        /// <summary>
        /// Original file name for display only.
        /// </summary>
        public string ImageFileName { get; set; } = string.Empty;

        // Note: IsSelected is intentionally excluded - it's UI state only
    }
}
