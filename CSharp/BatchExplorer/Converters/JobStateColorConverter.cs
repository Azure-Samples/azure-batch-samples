//Copyright (c) Microsoft Corporation

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Azure.Batch.Common;

namespace Microsoft.Azure.BatchExplorer.Converters
{
    /// <summary>
    /// Converts job states to color for UI display
    /// </summary>
    public class JobStateColorConverter : IValueConverter
    {
        //TODO: Switch this and other color converters to be colorblind friendly?
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            JobState state = (JobState)value;
            switch (state)
            {
                case JobState.Active:
                    return new SolidColorBrush(Colors.Blue);
                case JobState.Completed:
                    return new SolidColorBrush(Colors.Green);
                case JobState.Deleting:
                    return new SolidColorBrush(Colors.Gray);
                case JobState.Disabled:
                    return new SolidColorBrush(Colors.Gray);
                case JobState.Disabling:
                    return new SolidColorBrush(Colors.Gray);
                case JobState.Enabling:
                    return new SolidColorBrush(Colors.LightBlue);
                case JobState.Terminating:
                    return new SolidColorBrush(Colors.DarkRed);
                case JobState.Invalid:
                case JobState.Unmapped:
                default:
                    return new SolidColorBrush(Colors.Gray);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
