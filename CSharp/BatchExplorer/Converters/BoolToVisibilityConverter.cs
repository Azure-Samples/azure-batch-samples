using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Microsoft.Azure.BatchExplorer.Converters
{
    /// <summary>
    /// Converts a boolean to a Visibility value
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var returnVisible = (bool)value;

            if (returnVisible)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
