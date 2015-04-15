using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Microsoft.Azure.BatchExplorer.Converters
{
    /// <summary>
    /// Converts a set of booleans to a single bool value by ANDing each value.
    /// </summary>
    public class BooleanAndConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool result = true;

            foreach (object value in values)
            {
                bool? boolean = value as bool?;
                if (boolean != null)
                {
                    result &= boolean.Value;
                }
            }

            return result;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
