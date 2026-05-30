using System.Globalization;
using System.Windows.Data;

namespace AppLauncher.Helpers;

/// <summary>空文字（システムデフォルト）を表示名に変換する。</summary>
[ValueConversion(typeof(string), typeof(string))]
public class FontNameDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && string.IsNullOrEmpty(s) ? "システムデフォルト" : value ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && s == "システムデフォルト" ? "" : value ?? "";
}
