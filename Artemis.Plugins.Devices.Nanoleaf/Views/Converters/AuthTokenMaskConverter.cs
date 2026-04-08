using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Artemis.Plugins.Devices.Nanoleaf.Views.Converters;

public class AuthTokenMaskConverter : IValueConverter
{
    public static readonly AuthTokenMaskConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? string.Empty : "••••••••";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
