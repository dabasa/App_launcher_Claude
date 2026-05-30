using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AppLauncher.Helpers;

/// <summary>フォント名文字列を FontFamily に変換する。空文字はシステムデフォルトを返す。</summary>
[ValueConversion(typeof(string), typeof(FontFamily))]
public class FontNameToFamilyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrEmpty(name))
        {
            try { return new FontFamily(name); }
            catch { }
        }
        return SystemFonts.MessageFontFamily;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
