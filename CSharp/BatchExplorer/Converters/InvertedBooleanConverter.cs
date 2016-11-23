//Copyright (c) Microsoft Corporation

using System;
using System.Globalization;
using System.Windows.Data;

namespace Microsoft.Azure.BatchExplorer.Converters
{
    /// <summary>
    /// Converts between a bool and its inverse
    /// </summary>
    public class InvertedBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ConvertImpl(value, targetType);
        }       

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ConvertImpl(value, targetType);
        }

        private static object ConvertImpl(object value, Type targetType)
        {
            if (targetType != typeof(bool) && targetType != typeof(bool?))
            {
                throw new InvalidOperationException("The target must be a bool or nullable bool");
            }

            if (targetType == typeof(bool?))
            {
                return !(bool?)value;
            }

            return !(bool)value;
        }
    }
}
