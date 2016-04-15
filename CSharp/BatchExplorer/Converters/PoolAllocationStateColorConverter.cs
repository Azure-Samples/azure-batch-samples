//Copyright (c) Microsoft Corporation

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Azure.Batch.Common;

namespace Microsoft.Azure.BatchExplorer.Converters
{
    /// <summary>
    /// Converts pool allocation states to color for UI display
    /// </summary>
    public class PoolAllocationStateColorConverter : IValueConverter
    {
        //TODO: Switch this and other color converters to be colorblind friendly?
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            AllocationState? state = null;
            if (value != null)
            {
                state = (AllocationState)value;
            }

            switch (state)
            {
                case AllocationState.Steady:
                    return new SolidColorBrush(Colors.Blue);
                case AllocationState.Resizing:
                    return new SolidColorBrush(Colors.LightBlue);
                case AllocationState.Stopping:
                    return new SolidColorBrush(Colors.Red);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
