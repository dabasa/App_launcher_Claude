using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using AppLauncher.Models;

namespace AppLauncher.Helpers;

public class ColorNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string name ? ColorPalette.GetColor(name) : Colors.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
