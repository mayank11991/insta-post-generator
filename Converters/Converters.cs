using System.Globalization;
using Microsoft.Maui.Controls;

namespace InstaPostGenerator.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isSelected = (bool)value;
        var param = parameter?.ToString();

        return param switch
        {
            "selected" => isSelected ? Color.FromArgb("#D1FF02") : Color.FromArgb("#2A2D35"),
            "border" => isSelected ? Color.FromArgb("#D1FF02") : Color.FromArgb("#444"),
            "radio" => isSelected ? Color.FromArgb("#3A3A3A") : Color.FromArgb("#1A1A1A"),
            "radio_border" => isSelected ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#333"),
            "radio_inverse" => isSelected ? Color.FromArgb("#3A3A3A") : Color.FromArgb("#1A1A1A"),
            "radio_inverse_border" => isSelected ? Color.FromArgb("#FFFFFF") : Color.FromArgb("#333"),
            _ => isSelected ? Colors.Green : Colors.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isSelected = (bool)value;
        return isSelected ? "checkmark_circle.svg" : "circle.svg";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class GeneratingTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isGenerating = (bool)value;
        return isGenerating ? "⏳ Generating Posts..." : "🚀 Generate 10 Posts";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(bool)value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value?.ToString());
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = (int)value;
        var inverse = parameter?.ToString() == "inverse";
        var result = count > 0;
        return inverse ? !result : result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToRadioFillConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? Color.FromArgb("#D1FF02") : Colors.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}