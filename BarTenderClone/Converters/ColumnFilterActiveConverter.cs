using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BarTenderClone.Converters
{
    public class ColumnFilterActiveConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is Dictionary<string, HashSet<string>> columnFilters &&
                values[1] is string columnKey)
            {
                bool isActive = columnFilters.ContainsKey(columnKey) && columnFilters[columnKey].Count > 0;
                return isActive;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
