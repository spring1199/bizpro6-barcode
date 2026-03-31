using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BarTenderClone.Converters
{
    public class EnumToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            string checkValue = value.ToString() ?? string.Empty;
            string targetValue = parameter.ToString() ?? string.Empty;

            return checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
