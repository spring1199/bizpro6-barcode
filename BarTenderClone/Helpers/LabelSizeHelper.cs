using System;

namespace BarTenderClone.Helpers
{
    /// <summary>
    /// Centralized helper class for label size calculations and coordinate conversions.
    /// Provides proper scaling between screen display (96 DPI) and printer output (203/300/600 DPI).
    /// </summary>
    public static class LabelSizeHelper
    {
        // Display constants
        public const double SCREEN_DPI = 96.0;

        // Physical size constants
        public const double MM_PER_INCH = 25.4;
        public const double POINTS_PER_INCH = 72.0;

        // Printer safety margins (in mm) - prevents edge cropping
        public const double MARGIN_LEFT_MM = 2.0;
        public const double MARGIN_TOP_MM = 2.0;
        public const double MARGIN_RIGHT_MM = 2.0;
        public const double MARGIN_BOTTOM_MM = 2.0;

        // Global Label Home offset (in mm) - shifts entire print area
        // Set to 0 for WYSIWYG (preview = print output)
        // Adjust these values ONLY if printer hardware requires physical offset
        public const double LABEL_HOME_X_MM = 0.0;  // No offset for WYSIWYG
        public const double LABEL_HOME_Y_MM = 0.0;  // No offset for WYSIWYG

        // Unified barcode X offset (in mm) - applies to ALL DPI settings
        // Set to 0 for consistent positioning across all printer resolutions
        // Adjust if barcodes appear misaligned on specific printers
        public const double BARCODE_X_OFFSET_MM = 0.0;  // No offset by default

        // Font scaling factor for readability on physical labels
        // Set to 1.5 for WYSIWYG matching (balanced between 96 DPI screen and 203 DPI printer)
        // Adjust if printed text appears too small/large vs screen preview
        public const double FONT_SCALING_FACTOR = 1.5;

        // Font width as percentage of height (0.8 = 80% width for lighter/non-bold text)
        // Adjust: 0.75 = thinner, 0.85 = slightly bolder, 1.0 = bold (equal width/height)
        public const double FONT_WIDTH_RATIO = 0.8;

        /// <summary>
        /// Converts millimeters to inches
        /// </summary>
        public static double MmToInches(double mm) => mm / MM_PER_INCH;

        /// <summary>
        /// Converts inches to millimeters
        /// </summary>
        public static double InchesToMm(double inches) => inches * MM_PER_INCH;

        /// <summary>
        /// Converts inches to screen pixels for preview display
        /// </summary>
        public static double InchesToScreenPixels(double inches) => inches * SCREEN_DPI;

        /// <summary>
        /// Converts screen pixels to inches
        /// </summary>
        public static double ScreenPixelsToInches(double pixels) => pixels / SCREEN_DPI;

        /// <summary>
        /// Converts millimeters to printer dots directly
        /// </summary>
        public static int MmToDots(double mm, int printerDpi)
        {
            double inches = mm / MM_PER_INCH;
            return (int)Math.Round(inches * printerDpi);
        }

        /// <summary>
        /// Converts millimeters to screen pixels for UI preview
        /// </summary>
        public static double MmToScreenPixels(double mm)
        {
            double inches = mm / MM_PER_INCH;
            return inches * SCREEN_DPI;
        }

        /// <summary>
        /// Converts screen pixels to printer dots.
        /// Used for positioning (X, Y) and dimensions (Width, Height).
        /// </summary>
        public static int ScreenPixelsToDots(double pixels, int printerDpi)
        {
            double inches = pixels / SCREEN_DPI;
            return (int)Math.Round(inches * printerDpi);
        }

        /// <summary>
        /// Converts font size from screen pixels to ZPL font height dots.
        /// Uses FONT_SCALING_FACTOR to match preview appearance on print.
        /// </summary>
        public static int FontSizeToZplHeight(double screenFontSizePixels, int printerDpi)
        {
            // Convert screen pixels to inches, then to printer dots
            // Apply FONT_SCALING_FACTOR for WYSIWYG matching
            double inches = screenFontSizePixels / SCREEN_DPI;
            int dots = (int)Math.Round(inches * printerDpi * FONT_SCALING_FACTOR);
            return Math.Max(dots, 15);
        }

        /// <summary>
        /// Converts font size from screen pixels to ZPL font width dots.
        /// Uses FONT_WIDTH_RATIO to create proportional (non-bold) text.
        /// </summary>
        /// <param name="screenFontSizePixels">Font size as shown in preview (e.g., 28px)</param>
        /// <param name="printerDpi">Printer resolution (203, 300, or 600 DPI)</param>
        /// <returns>Font width in dots for ZPL ^A command</returns>
        public static int FontSizeToZplWidth(double screenFontSizePixels, int printerDpi)
        {
            int height = FontSizeToZplHeight(screenFontSizePixels, printerDpi);
            return Math.Max((int)(height * FONT_WIDTH_RATIO), 16);
        }

        /// <summary>
        /// Calculates optimal barcode module width in dots.
        /// Targets 10 mil (0.01 inches) for reliable barcode scanning.
        /// </summary>
        /// <param>Printer resolution (203, 300, or 600 DPI)</param>
        /// <returns>Module width in dots for ZPL ^BY command</returns>
        public static int CalculateBarcodeModuleWidth(int printerDpi)
        {
            // Target 10 mil (0.01 inches) module width for reliable scanning
            const double TARGET_MIL = 0.010;
            int moduleWidth = (int)Math.Round(TARGET_MIL * printerDpi);

            // Clamp to reasonable range (2-6 dots)
            return Math.Clamp(moduleWidth, 2, 6);
        }

        /// <summary>
        /// Gets safe printable area margins in screen pixels.
        /// Use these margins to prevent content from being cropped at label edges.
        /// </summary>
        /// <returns>Tuple of (left, top, right, bottom) margins in screen pixels</returns>
        public static (double left, double top, double right, double bottom) GetSafeMarginsPixels()
        {
            return (
                InchesToScreenPixels(MmToInches(MARGIN_LEFT_MM)),
                InchesToScreenPixels(MmToInches(MARGIN_TOP_MM)),
                InchesToScreenPixels(MmToInches(MARGIN_RIGHT_MM)),
                InchesToScreenPixels(MmToInches(MARGIN_BOTTOM_MM))
            );
        }

        /// <summary>
        /// Gets safe printable area margins in printer dots.
        /// Use when generating ZPL to ensure content fits within printable area.
        /// </summary>
        /// <param name="printerDpi">Printer resolution (203, 300, or 600 DPI)</param>
        /// <returns>Tuple of (left, top, right, bottom) margins in printer dots</returns>
        public static (int left, int top, int right, int bottom) GetSafeMarginsDots(int printerDpi)
        {
            return (
                (int)Math.Round(MmToInches(MARGIN_LEFT_MM) * printerDpi),
                (int)Math.Round(MmToInches(MARGIN_TOP_MM) * printerDpi),
                (int)Math.Round(MmToInches(MARGIN_RIGHT_MM) * printerDpi),
                (int)Math.Round(MmToInches(MARGIN_BOTTOM_MM) * printerDpi)
            );
        }

        /// <summary>
        /// Calculates responsive font size based on label dimensions.
        /// Scales font proportionally so layouts work on any label size.
        /// </summary>
        /// <param name="labelWidthInches">Label width in inches</param>
        /// <param name="labelHeightInches">Label height in inches</param>
        /// <param name="category">Font size category (Small, Medium, Large, ExtraLarge)</param>
        /// <returns>Font size in screen pixels</returns>
        public static double CalculateResponsiveFontSize(
            double labelWidthInches,
            double labelHeightInches,
            FontSizeCategory category)
        {
            // Base area: 54mm x 34mm = 2.126" x 1.339" = 2.846 sq inches
            const double BASE_AREA = 2.846;
            double currentArea = labelWidthInches * labelHeightInches;
            double scaleFactor = Math.Sqrt(currentArea / BASE_AREA);

            // Base font sizes for 54x34mm label (screen pixels)
            // These sizes fit properly in preview; 1.9x scaling applies during print
            double baseFontSize = category switch
            {
                FontSizeCategory.Small => 8,       // Auxiliary text (codes, dates)
                FontSizeCategory.Medium => 9,      // Product code (default for new text)
                FontSizeCategory.Large => 10,      // Product name
                FontSizeCategory.ExtraLarge => 10, // Price
                _ => 9
            };

            return Math.Round(baseFontSize * scaleFactor);
        }

        /// <summary>
        /// Calculates responsive barcode dimensions based on label size.
        /// Ensures barcodes scale proportionally with label dimensions.
        /// </summary>
        /// <param name="labelWidthInches">Label width in inches</param>
        /// <param name="labelHeightInches">Label height in inches</param>
        /// <returns>Tuple of (width, height) in screen pixels</returns>
        public static (double width, double height) CalculateResponsiveBarcodeSize(
            double labelWidthInches,
            double labelHeightInches)
        {
            // Base area: 54mm x 34mm = 2.846 sq inches
            const double BASE_AREA = 2.846;
            double currentArea = labelWidthInches * labelHeightInches;
            double scaleFactor = Math.Sqrt(currentArea / BASE_AREA);

            // Return default height, width will be calculated dynamically by CalculateCode128Width
            return (
                100 * scaleFactor, // Default width fallback
                30 * scaleFactor   // Compact barcode height
            );
        }

        /// <summary>
        /// Calculates the expected width of a Code 128 barcode in screen pixels.
        /// Formula: ((dataLength * 11) + 35) * moduleWidthDots rendered as inches then pixels.
        /// </summary>
        public static double CalculateCode128Width(string data, int printerDpi)
        {
            if (string.IsNullOrEmpty(data)) data = "12345678";
            
            int moduleWidthDots = CalculateBarcodeModuleWidth(printerDpi);
            // Each char is 11 modules + 35 modules for start/stop/quiet
            int totalDots = (data.Length * 11 + 35) * moduleWidthDots;
            
            // Convert dots to inches, then inches to screen pixels
            double inches = (double)totalDots / printerDpi;
            return Math.Round(InchesToScreenPixels(inches));
        }
    }

    /// <summary>
    /// Font size categories for responsive layout
    /// </summary>
    public enum FontSizeCategory
    {
        Small,       // Auxiliary text (codes, dates)
        Medium,      // Standard text (product codes)
        Large,       // Prominent text (product names)
        ExtraLarge   // Very prominent text (prices)
    }
}
