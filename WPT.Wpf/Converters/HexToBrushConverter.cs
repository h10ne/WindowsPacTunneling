using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace WPT.Wpf.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
        {
            return System.Windows.Media.Brushes.Gray;
        }

        try
        {
            var color = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return System.Windows.Media.Brushes.Gray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
