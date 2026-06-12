using System;
using System.Globalization;
using System.Windows.Data;

namespace BarTenderClone.Converters
{
    public class IncrementConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int offset = 1;
            if (parameter != null && int.TryParse(parameter.ToString(), out var p))
            {
                offset = p;
            }
            if (value is int i) return (i + offset).ToString();
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
