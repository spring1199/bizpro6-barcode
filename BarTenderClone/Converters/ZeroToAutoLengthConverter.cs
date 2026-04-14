using System;
using System.Globalization;
using System.Windows.Data;

namespace BarTenderClone.Converters
{
    /// <summary>
    /// Treats zero/negative lengths as Auto in XAML bindings.
    /// This lets legacy text elements keep auto height while newer/resized ones
    /// can opt into explicit height that the print engine already respects.
    /// </summary>
    public class ZeroToAutoLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double length && length > 0)
                return length;

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double length && !double.IsNaN(length))
                return length;

            return 0d;
        }
    }
}
