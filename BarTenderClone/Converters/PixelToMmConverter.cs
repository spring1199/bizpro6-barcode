using System;
using System.Globalization;
using System.Windows.Data;

namespace BarTenderClone.Converters
{
    /// <summary>
    /// Converts between screen pixels and millimeters for UI display.
    /// Internal storage: pixels (at 96 DPI)
    /// UI display: millimeters
    /// </summary>
    public class PixelToMmConverter : IValueConverter
    {
        // 1 inch = 25.4mm, Screen = 96 DPI
        private const double MM_PER_PIXEL = 25.4 / 96.0; // ≈ 0.2646
        private const double PIXEL_PER_MM = 96.0 / 25.4; // ≈ 3.78

        /// <summary>
        /// Converts pixels to mm for display in UI
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double pixels)
            {
                double mm = pixels * MM_PER_PIXEL;
                return Math.Round(mm, 1); // Round to 1 decimal place
            }
            return value;
        }

        /// <summary>
        /// Converts mm input back to pixels for storage
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str && double.TryParse(str, out double mm))
            {
                return mm * PIXEL_PER_MM;
            }
            if (value is double mmValue)
            {
                return mmValue * PIXEL_PER_MM;
            }
            return value;
        }
    }
}
