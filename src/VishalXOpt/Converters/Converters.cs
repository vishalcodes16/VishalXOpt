using System.Globalization;
using System.Windows;
using System.Windows.Data;
using VishalXOpt.Models;

namespace VishalXOpt.Converters;

/// <summary>Converts a bool into one of two strings via ConverterParameter formatted as
/// "TrueText|FalseText" - used for toggle-style button labels.</summary>
public sealed class BoolToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var options = (parameter as string)?.Split('|') ?? new[] { "True", "False" };
        var isTrue = value is true;
        return isTrue ? options[0] : (options.Length > 1 ? options[1] : options[0]);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is true);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !(value is true);
}

/// <summary>Visible when the bound value is non-null (or, with ConverterParameter="Invert",
/// visible only when it IS null - used to show the Tools grid only while no detail is open).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNull = value is null;
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        var visible = invert ? isNull : !isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Shows an element only when the bound enum value matches ConverterParameter
/// (compared by name) - used to switch between the four top-level sections in MainWindow
/// without a full DataTemplateSelector.</summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return Visibility.Collapsed;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RiskLevelToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RiskLevel and not RiskLevel.None ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Maps a GameReadyX <c>CheckStatus</c> to a themed brush.</summary>
public sealed class CheckStatusToBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush Pass =
        new(System.Windows.Media.Color.FromRgb(0x3C, 0xCB, 0x7F));
    private static readonly System.Windows.Media.SolidColorBrush Warn =
        new(System.Windows.Media.Color.FromRgb(0xE8, 0xA6, 0x3C));
    private static readonly System.Windows.Media.SolidColorBrush Fail =
        new(System.Windows.Media.Color.FromRgb(0xE8, 0x4B, 0x4B));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "Pass" => Pass,
            "Warn" => Warn,
            "Fail" => Fail,
            _ => Warn
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class CheckStatusToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
        {
            "Pass" => "\u2713",
            "Warn" => "!",
            "Fail" => "\u2715",
            _ => "?"
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class RiskToBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush Danger =
        new(System.Windows.Media.Color.FromRgb(0xE8, 0x4B, 0x4B));
    private static readonly System.Windows.Media.SolidColorBrush Warning =
        new(System.Windows.Media.Color.FromRgb(0xE8, 0xA6, 0x3C));
    private static readonly System.Windows.Media.SolidColorBrush Neutral =
        new(System.Windows.Media.Color.FromRgb(0x6B, 0x7A, 0x99));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        RiskLevel.High => Danger,
        RiskLevel.Medium => Warning,
        RiskLevel.Low => Warning,
        _ => Neutral
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
