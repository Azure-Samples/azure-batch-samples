using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Azure.Batch.Common;

namespace Microsoft.Azure.BatchExplorer.Converters
{
    /// <summary>
    /// Converts job schedule states to color for UI display
    /// </summary>
    public class JobScheduleStateColorConverter : IValueConverter
    {
        //TODO: Switch this and other color converters to be colorblind friendly?
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            JobScheduleState state = (JobScheduleState)value;
            switch (state)
            {
                case JobScheduleState.Active:
                    return new SolidColorBrush(Colors.Blue);
                case JobScheduleState.Completed:
                    return new SolidColorBrush(Colors.Green);
                case JobScheduleState.Deleting:
                    return new SolidColorBrush(Colors.Gray);
                case JobScheduleState.Disabled:
                    return new SolidColorBrush(Colors.Gray);
                case JobScheduleState.Terminating:
                    return new SolidColorBrush(Colors.DarkRed);
                case JobScheduleState.Invalid:
                    return new SolidColorBrush(Colors.Gray);
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
