using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Azure.Batch.Common;

namespace Microsoft.Azure.BatchExplorer.Converters
{
    /// <summary>
    /// Converts job status states to color for UI display
    /// </summary>
    public class JobStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            JobState state = (JobState) value;
            switch (state)
            {
                case JobState.Invalid:
                    return new SolidColorBrush(Colors.DarkRed);
                case JobState.Active:
                    return new SolidColorBrush(Colors.Orange);
                case JobState.Disabling:
                    return new SolidColorBrush(Colors.LightBlue);
                case JobState.Disabled:
                    return new SolidColorBrush(Colors.Blue);
                case JobState.Terminating:
                    return new SolidColorBrush(Colors.Red);
                case JobState.Completed:
                    return new SolidColorBrush(Colors.LimeGreen);
                case JobState.Deleting:
                    return new SolidColorBrush(Colors.LightGray);
                case JobState.Unmapped:
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
