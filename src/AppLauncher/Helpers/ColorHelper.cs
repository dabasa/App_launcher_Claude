using System.Windows.Media;

namespace AppLauncher.Helpers;

public static class ColorHelper
{
    /// <summary>
    /// ページ背景色をもとに UI 要素色（セクション区切り・ボタン等）を計算する。
    /// HSL 変換 → L±15pt 調整 → S-5pt 調整 → RGB 変換。
    /// </summary>
    public static Color ComputeUiElementColor(Color baseColor)
    {
        RgbToHsl(baseColor, out double h, out double s, out double l);
        l = l >= 0.5 ? Math.Max(0.0, l - 0.15) : Math.Min(1.0, l + 0.15);
        s = Math.Max(0.0, s - 0.05);
        return HslToRgb(h, s, l);
    }

    private static void RgbToHsl(Color c, out double h, out double s, out double l)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;
        if (max == min) { h = s = 0; return; }
        double d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        if      (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else               h = (r - g) / d + 4;
        h /= 6;
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        if (s == 0) { byte v = (byte)(l * 255); return Color.FromRgb(v, v, v); }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        return Color.FromRgb(
            (byte)(HueToRgb(p, q, h + 1.0 / 3) * 255),
            (byte)(HueToRgb(p, q, h)            * 255),
            (byte)(HueToRgb(p, q, h - 1.0 / 3) * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1; if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
