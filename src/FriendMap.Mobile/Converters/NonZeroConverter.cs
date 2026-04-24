using System.Globalization;

namespace FriendMap.Mobile.Converters;

public class NonZeroConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i) return i > 0;
        if (value is long l) return l > 0;
        if (value is double d) return d > 0;
        if (value is float f) return f > 0;
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
