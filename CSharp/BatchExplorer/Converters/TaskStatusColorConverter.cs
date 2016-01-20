//Copyright (c) Microsoft Corporation

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
            TaskState? state = null;
            if (value != null)
            {
                state = (TaskState) value;
            }

            switch (state)
            {
                case TaskState.Active:
                    return new SolidColorBrush(Colors.Orange);
                case TaskState.Preparing:
                    return new SolidColorBrush(Colors.LightBlue);
                case TaskState.Running:
                    return new SolidColorBrush(Colors.Blue);
                case TaskState.Completed:
                    return new SolidColorBrush(Colors.LimeGreen);
                default:
                    return new SolidColorBrush(Colors.DarkGray);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
