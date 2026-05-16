using System.Windows.Media;

namespace AppLauncher.Models;

public static class ColorPalette
{
    private static readonly Dictionary<string, Color> Palette = new()
    {
        { "blue",   Color.FromRgb(0x4A, 0x90, 0xD9) },
        { "green",  Color.FromRgb(0x4C, 0xAF, 0x72) },
        { "purple", Color.FromRgb(0x9B, 0x6D, 0xD9) },
        { "orange", Color.FromRgb(0xE8, 0x84, 0x3A) },
        { "red",    Color.FromRgb(0xE0, 0x52, 0x52) },
        { "gray",   Color.FromRgb(0x8C, 0x9B, 0xAB) },
        { "teal",   Color.FromRgb(0x2B, 0xB5, 0xA0) },
        { "pink",   Color.FromRgb(0xE8, 0x68, 0xA2) },
        { "yellow", Color.FromRgb(0xF0, 0xC2, 0x33) },
        { "black",  Color.FromRgb(0x2D, 0x2D, 0x2D) },
        { "white",  Color.FromRgb(0xF0, 0xF0, 0xF0) },
    };

    public static IReadOnlyDictionary<string, Color> All => Palette;

    public static Color GetColor(string name) =>
        Palette.TryGetValue(name, out var color) ? color : Colors.Transparent;

    public static SolidColorBrush GetBrush(string name) =>
        new(GetColor(name));

    // 透過率（0〜90 %）を WPF の不透明度（0.0〜1.0）へ変換する
    // 透過率30% = 30%透明 = 70%不透明 → WPF Opacity 0.70
    public static double OpacityToDouble(int transparencyPercent) =>
        1.0 - transparencyPercent / 100.0;
}
