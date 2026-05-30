using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using System.Windows.Media;
using AppLauncher.Models;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class FontDetailViewModel : ObservableObject
{
    public sealed record FontColorEntry(string Name, SolidColorBrush Brush);

    // ─── フォント設定プロパティ ────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFontFamily))]
    private string _fontName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FontSizeSliderEnabled))]
    [NotifyPropertyChangedFor(nameof(PreviewFontSize))]
    private int _fontSizePt = 16;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewForeground))]
    private string _fontColor = "white";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FontSizeSliderEnabled))]
    private bool _autoFontSize = false;

    // ─── 派生プロパティ ───────────────────────────────────────────────────
    public bool   FontSizeSliderEnabled => !AutoFontSize;
    public double PreviewFontSize       => FontSizePt * 4.0 / 3.0;
    public SolidColorBrush PreviewForeground => ColorPalette.GetBrush(FontColor);
    public FontFamily PreviewFontFamily =>
        string.IsNullOrEmpty(FontName) ? SystemFonts.MessageFontFamily : new FontFamily(FontName);

    // ─── ダイアログ表示用 ─────────────────────────────────────────────────
    public string PreviewText { get; }
    public string DialogTitle { get; }

    // ─── 選択肢リスト ─────────────────────────────────────────────────────
    public IReadOnlyList<string>         FontFamilies { get; } = BuildFontFamilyList();
    public IReadOnlyList<FontColorEntry> ColorItems   { get; } =
        ColorPalette.All.Keys
            .Select(n => new FontColorEntry(n, ColorPalette.GetBrush(n)))
            .ToList();

    // ─── コンストラクター ─────────────────────────────────────────────────
    public FontDetailViewModel(FontConfig src, string previewText, string dialogTitle)
    {
        PreviewText = previewText;
        DialogTitle = dialogTitle;
        _fontName     = src.FontName;
        _fontSizePt   = src.FontSizePt;
        _fontColor    = src.FontColor;
        _autoFontSize = src.AutoFontSize;
    }

    // ─── 結果取得 ─────────────────────────────────────────────────────────
    public FontConfig ToConfig() => new()
    {
        FontName     = FontName,
        FontSizePt   = FontSizePt,
        FontColor    = FontColor,
        AutoFontSize = AutoFontSize,
    };

    // ─── フォントリスト構築（キャッシュ） ─────────────────────────────────
    private static readonly Lazy<IReadOnlyList<string>> _fontFamiliesCache = new(BuildFontFamilyList);

    private static IReadOnlyList<string> BuildFontFamilyList()
    {
        var list = new List<string> { "" };
        list.AddRange(
            Fonts.SystemFontFamilies
                 .Select(f => f.Source)
                 .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
        return list;
    }
}
