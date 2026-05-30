using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AppLauncher.Models;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class TileEditViewModel : ObservableObject
{
    // ─── 基本 ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApp))]
    [NotifyPropertyChangedFor(nameof(IsSystem))]
    [NotifyPropertyChangedFor(nameof(HasPath))]
    [NotifyPropertyChangedFor(nameof(HasArgs))]
    [NotifyPropertyChangedFor(nameof(HasWorkDir))]
    [NotifyPropertyChangedFor(nameof(HasSystemInfo))]
    private string _type;

    // ─── 起動 ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _path;
    [ObservableProperty] private string _args;
    [ObservableProperty] private string _workDir;

    // ─── 見た目 ───────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBackground))]
    private string _color;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBackground))]
    private int _opacity;

    [ObservableProperty] private string _imagePath;
    [ObservableProperty] private string _imagePosition;
    [ObservableProperty] private bool   _imageTransparent;

    // ─── フォント（TitleFont 作業コピー） ────────────────────────────────
    // TitleFont の各プロパティを [ObservableProperty] 相当の手動実装で公開する。
    // これにより既存の XAML バインディング（スライダー・色選択等）が変更なしで動作する。
    private FontConfig _titleFontWork;
    private FontConfig _contentFontWork;

    public string FontName
    {
        get => _titleFontWork.FontName;
        set
        {
            _titleFontWork.FontName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewAutoFontSize));
        }
    }

    public int FontSizePt
    {
        get => _titleFontWork.FontSizePt;
        set
        {
            _titleFontWork.FontSizePt = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewFontSize));
            OnPropertyChanged(nameof(PreviewAutoFontSize));
        }
    }

    public string FontColor
    {
        get => _titleFontWork.FontColor;
        set
        {
            _titleFontWork.FontColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewForeground));
        }
    }

    public bool AutoFontSize
    {
        get => _titleFontWork.AutoFontSize;
        set
        {
            _titleFontWork.AutoFontSize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FontSizeSliderEnabled));
            OnPropertyChanged(nameof(PreviewAutoFontSize));
        }
    }

    // ─── システム情報 ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SiHasDeviceType))]
    [NotifyPropertyChangedFor(nameof(SiHasAccent))]
    [NotifyPropertyChangedFor(nameof(SiHasCircleFormat))]
    [NotifyPropertyChangedFor(nameof(SiHasTarget))]
    private string _siCategory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SiHasTarget))]
    private string _siDeviceType;
    [ObservableProperty] private string _siTarget;
    [ObservableProperty] private string _siDisplayFormat;
    [ObservableProperty] private string _siMainColor;
    [ObservableProperty] private string _siAccentColor;
    [ObservableProperty] private string _siBackgroundAccent;
    [ObservableProperty] private int    _siThreshold;
    [ObservableProperty] private string _siClickAction;
    [ObservableProperty] private int    _siUpdateIntervalMs;

    // ─── 配置情報（編集対象外） ────────────────────────────────────────────
    public int Col     { get; }
    public int Row     { get; }
    public int ColSpan { get; }
    public int RowSpan { get; }

    // ─── フォントサマリー ─────────────────────────────────────────────────
    public string TitleFontSummary =>
        $"{(string.IsNullOrEmpty(_titleFontWork.FontName) ? "デフォルト" : _titleFontWork.FontName)}" +
        $"  {_titleFontWork.FontSizePt} pt" +
        $"  {_titleFontWork.FontColor}" +
        $"{(_titleFontWork.AutoFontSize ? "  [自動]" : "")}";

    public string ContentFontSummary =>
        $"{(string.IsNullOrEmpty(_contentFontWork.FontName) ? "デフォルト" : _contentFontWork.FontName)}" +
        $"  {_contentFontWork.FontSizePt} pt" +
        $"  {_contentFontWork.FontColor}" +
        $"{(_contentFontWork.AutoFontSize ? "  [自動]" : "")}";

    // ─── 条件プロパティ ───────────────────────────────────────────────────
    public bool IsApp         => Type == "app";
    public bool IsSystem      => Type == "system";
    public bool HasPath       => Type != "system";
    public bool HasArgs       => Type == "app";
    public bool HasWorkDir    => Type == "app";
    public bool HasSystemInfo => Type == "system";
    public bool FontSizeSliderEnabled => !AutoFontSize;

    public bool SiHasDeviceType   => SiCategory == "usage";
    public bool SiHasAccent       => SiCategory is "storage" or "usage";
    public bool SiHasCircleFormat => SiCategory == "storage" || (SiCategory == "usage" && SiDeviceType != "lan");
    public bool SiHasTarget       => SiCategory == "storage" || (SiCategory == "usage" && SiDeviceType is "lan" or "gpu");

    // Title 変更時に PreviewAutoFontSize も再計算
    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(PreviewAutoFontSize));

    // ─── プレビュー用計算プロパティ ────────────────────────────────────────
    public SolidColorBrush PreviewBackground
    {
        get
        {
            var c = ColorPalette.GetColor(Color);
            double a = ColorPalette.OpacityToDouble(Opacity);
            return new SolidColorBrush(
                System.Windows.Media.Color.FromArgb((byte)(255 * a), c.R, c.G, c.B));
        }
    }

    public SolidColorBrush PreviewForeground => ColorPalette.GetBrush(FontColor);
    public double PreviewFontSize => FontSizePt * 4.0 / 3.0;

    // AutoFontSize=true 時はプレビュー枠(200×96)に収まるサイズを計算して返す
    public double PreviewAutoFontSize
    {
        get
        {
            if (!AutoFontSize || string.IsNullOrEmpty(Title)) return PreviewFontSize;

            var measure = new TextBlock
            {
                Text         = Title,
                TextWrapping = TextWrapping.Wrap,
                FontFamily   = string.IsNullOrEmpty(FontName)
                    ? SystemFonts.MessageFontFamily : new FontFamily(FontName),
                Padding      = new Thickness(4),
            };
            double startPt = Math.Clamp(FontSizePt, 6, 72);
            for (double size = startPt; size >= 6; size--)
            {
                measure.FontSize = size * 4.0 / 3.0;
                measure.Measure(new Size(200, double.PositiveInfinity));
                if (measure.DesiredSize.Height <= 96) return size * 4.0 / 3.0;
            }
            return 6 * 4.0 / 3.0;
        }
    }

    // ─── コンストラクター ─────────────────────────────────────────────────
    public TileEditViewModel(TileViewModel source)
    {
        Col     = source.Col;
        Row     = source.Row;
        ColSpan = source.ColSpan;
        RowSpan = source.RowSpan;

        _title   = source.Title;
        _type    = source.Type;
        _path    = source.Path;
        _args    = source.Args;
        _workDir = source.WorkDir;
        _color   = source.Color;
        _opacity = source.Opacity;
        _imagePath        = source.ImagePath;
        _imagePosition    = source.ImagePosition;
        _imageTransparent = source.ImageTransparent;

        _titleFontWork   = CloneFont(source.TitleFont);
        _contentFontWork = source.ContentFont != null
            ? CloneFont(source.ContentFont)
            : new FontConfig { FontSizePt = 12, FontColor = "white" };

        var si = source.SystemInfo;
        _siCategory         = si?.Category         ?? "usage";
        _siDeviceType       = si?.DeviceType       ?? "cpu";
        _siTarget           = si?.Target           ?? "";
        _siDisplayFormat    = si?.DisplayFormat    ?? "text";
        _siMainColor        = si?.MainColor        ?? "white";
        _siAccentColor      = si?.AccentColor      ?? "orange";
        _siBackgroundAccent = si?.BackgroundAccent ?? "red";
        _siThreshold        = si?.Threshold        ?? 80;
        _siClickAction      = si?.ClickAction      ?? "refresh";
        _siUpdateIntervalMs = si?.UpdateIntervalMs ?? 60000;
    }

    // ─── FontConfig 操作（Ph.F-B で詳細ダイアログから呼び出す） ─────────
    public FontConfig GetTitleFontCopy()   => CloneFont(_titleFontWork);
    public FontConfig GetContentFontCopy() => CloneFont(_contentFontWork);

    public void ApplyTitleFont(FontConfig cfg)
    {
        _titleFontWork = CloneFont(cfg);
        OnPropertyChanged(nameof(FontName));
        OnPropertyChanged(nameof(FontSizePt));
        OnPropertyChanged(nameof(FontColor));
        OnPropertyChanged(nameof(AutoFontSize));
        OnPropertyChanged(nameof(PreviewFontSize));
        OnPropertyChanged(nameof(PreviewAutoFontSize));
        OnPropertyChanged(nameof(PreviewForeground));
        OnPropertyChanged(nameof(FontSizeSliderEnabled));
        OnPropertyChanged(nameof(TitleFontSummary));
    }

    public void ApplyContentFont(FontConfig cfg)
    {
        _contentFontWork = CloneFont(cfg);
        OnPropertyChanged(nameof(ContentFontSummary));
    }

    // ─── ToConfig ────────────────────────────────────────────────────────
    public TileConfig ToConfig() => new()
    {
        Col     = Col,     Row     = Row,
        ColSpan = ColSpan, RowSpan = RowSpan,
        Type    = Type,    Title   = Title,
        Path    = Path,    Args    = Args,  WorkDir = WorkDir,
        Color   = Color,   Opacity = Opacity,
        ImagePath        = ImagePath,
        ImagePosition    = ImagePosition,
        ImageTransparent = ImageTransparent,
        TitleFont        = CloneFont(_titleFontWork),
        ContentFont      = Type == "system" ? CloneFont(_contentFontWork) : null,
        SystemInfo = Type == "system" ? new SystemInfoConfig
        {
            Category         = SiCategory,
            DeviceType       = SiDeviceType,
            Target           = SiTarget,
            DisplayFormat    = SiDisplayFormat,
            MainColor        = SiMainColor,
            AccentColor      = SiAccentColor,
            BackgroundAccent = SiBackgroundAccent,
            Threshold        = SiThreshold,
            ClickAction      = SiClickAction,
            UpdateIntervalMs = SiUpdateIntervalMs,
        } : null,
    };

    private static FontConfig CloneFont(FontConfig src) => new()
    {
        FontName     = src.FontName,
        FontSizePt   = src.FontSizePt,
        FontColor    = src.FontColor,
        AutoFontSize = src.AutoFontSize,
    };
}
