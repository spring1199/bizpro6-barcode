using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BarTenderClone.Converters
{
    public class EnumToLocalizedResourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            string enumTypeName = value.GetType().Name;
            string enumValueName = value.ToString() ?? string.Empty;
            string resourceKey = $"{enumTypeName}_{enumValueName}";

            // Look up in Application Resources
            if (Application.Current != null && Application.Current.Resources.Contains(resourceKey))
            {
                return Application.Current.Resources[resourceKey]?.ToString() ?? enumValueName;
            }

            // Fallback: look up with parameter prefix if provided
            if (parameter is string prefix)
            {
                string paramResourceKey = $"{prefix}_{enumValueName}";
                if (Application.Current != null && Application.Current.Resources.Contains(paramResourceKey))
                {
                    return Application.Current.Resources[paramResourceKey]?.ToString() ?? enumValueName;
                }
            }

            return enumValueName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
