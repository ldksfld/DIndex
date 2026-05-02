using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using DIndex.Core.Indexing.LinkedList;

namespace DIndex.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        return v is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        return v is Visibility.Visible;
    }
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        return v is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        return v is not Visibility.Visible;
    }
}

public sealed class TimestampToStringConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        if (v is long ts && ts > 0)
            return DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");

        return "-";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        throw new NotImplementedException();
    }
}

public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        if (v is long size)
        {
            if (size < 1024)
                return $"{size} Б";

            if (size < 1024 * 1024)
                return $"{size / 1024.0:F1} КБ";

            if (size < 1024L * 1024 * 1024)
                return $"{size / (1024.0 * 1024):F1} МБ";

            return $"{size / (1024.0 * 1024 * 1024):F2} ГБ";
        }

        return "-";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        throw new NotImplementedException();
    }
}

public sealed class OperationTypeToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush InsertBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly SolidColorBrush DeleteBrush = new(Color.FromRgb(0xEF, 0x44, 0x44));
    private static readonly SolidColorBrush UpdateBrush = new(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly SolidColorBrush SearchBrush = new(Color.FromRgb(0x64, 0x74, 0x8B));

    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        return v switch
        {
            OperationType.Insert => InsertBrush,
            OperationType.Delete => DeleteBrush,
            OperationType.Update => UpdateBrush,
            _ => SearchBrush,
        };
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        throw new NotImplementedException();
    }
}

public sealed class OperationTypeToStringConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        return v switch
        {
            OperationType.Insert => "Вставка",
            OperationType.Delete => "Видалення",
            OperationType.Update => "Оновлення",
            OperationType.Search => "Пошук",
            _ => v?.ToString() ?? ""
        };
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        throw new NotImplementedException();
    }
}

public sealed class EntityFlagsToStringConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        if (v is not byte flags)
            return "-";

        if ((flags & 0x02) != 0) return "Видалений";
        if ((flags & 0x04) != 0) return "Заблокований";
        if ((flags & 0x01) != 0) return "Активний";

        return "-";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        throw new NotImplementedException();
    }
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        return v is null || (v is string s && s.Length == 0)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        throw new NotImplementedException();
    }
}

public sealed class StringTruncateConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        if (v is not string s)
            return "";

        int maxLen;

        if (p is int len)
        {
            maxLen = len;
        }
        else if (p is string ps && int.TryParse(ps, out int parsed))
        {
            maxLen = parsed;
        }
        else
        {
            maxLen = 32;
        }

        return s.Length <= maxLen ? s : s[..maxLen] + "…";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        throw new NotImplementedException();
    }
}

public sealed class BoolNegateConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        return v is not true;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        return v is not true;
    }
}

public sealed class IntEqualConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        return v is int intVal
            && p is string pStr
            && int.TryParse(pStr, out int expected)
            && intVal == expected;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
    {
        return v is true && p is string pStr && int.TryParse(pStr, out int val)
            ? val
            : DependencyProperty.UnsetValue;
    }
}
