using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using FileSynchronizer.Core.Models;

namespace FileSynchronizer.UI.Common;

public class BytesToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
        return "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ActionToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SyncActionType action)
        {
            return action switch
            {
                SyncActionType.CopyLeftToRight => new SolidColorBrush(Color.FromRgb(16, 185, 129)),  // Green Emerald
                SyncActionType.CopyRightToLeft => new SolidColorBrush(Color.FromRgb(59, 130, 246)),  // Blue
                SyncActionType.DeleteRight => new SolidColorBrush(Color.FromRgb(239, 68, 68)),       // Red
                SyncActionType.DeleteLeft => new SolidColorBrush(Color.FromRgb(239, 68, 68)),        // Red
                SyncActionType.Conflict => new SolidColorBrush(Color.FromRgb(245, 158, 11)),        // Amber Orange
                SyncActionType.InSync => new SolidColorBrush(Color.FromRgb(107, 114, 128)),         // Gray
                SyncActionType.Skipped => new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
            };
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ActionToBgBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SyncActionType action)
        {
            return action switch
            {
                SyncActionType.CopyLeftToRight => new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
                SyncActionType.CopyRightToLeft => new SolidColorBrush(Color.FromArgb(35, 59, 130, 246)),
                SyncActionType.DeleteRight => new SolidColorBrush(Color.FromArgb(35, 239, 68, 68)),
                SyncActionType.DeleteLeft => new SolidColorBrush(Color.FromArgb(35, 239, 68, 68)),
                SyncActionType.Conflict => new SolidColorBrush(Color.FromArgb(45, 245, 158, 11)),
                SyncActionType.InSync => new SolidColorBrush(Color.FromArgb(20, 156, 163, 175)),
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool flag && flag;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}
