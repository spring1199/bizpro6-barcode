using System;
using System.Globalization;
using System.Windows.Data;
using BarTenderClone.Helpers;

namespace BarTenderClone.Converters
{
    /// <summary>
    /// Scales font size by FONT_SCALING_FACTOR to achieve WYSIWYG preview.
    /// The preview displays fonts at the same relative size they will print.
    /// </summary>
    public class FontScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double fontSize)
                return fontSize * LabelSizeHelper.FONT_SCALING_FACTOR;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double scaledFontSize)
                return scaledFontSize / LabelSizeHelper.FONT_SCALING_FACTOR;
            return value;
        }
    }
}
