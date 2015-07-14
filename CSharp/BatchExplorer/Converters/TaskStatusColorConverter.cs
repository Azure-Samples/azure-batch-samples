using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Azure.Batch.Common;

namespace Microsoft.Azure.BatchExplorer.Converters
{
    /// <summary>
    /// Converts task status states to color for UI display
    /// </summary>
    public class TaskStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TaskState state = (TaskState)value;
            switch (state)
            {
                case TaskState.Invalid:
                    return new SolidColorBrush(Colors.DarkRed);
                case TaskState.Active:
                    return new SolidColorBrush(Colors.Orange);
                case TaskState.Running:
                    return new SolidColorBrush(Colors.Blue);
                case TaskState.Completed:
                    return new SolidColorBrush(Colors.LimeGreen);
                case TaskState.Unmapped:
                    return new SolidColorBrush(Colors.DarkGray);
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
