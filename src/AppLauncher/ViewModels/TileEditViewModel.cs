using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty] private string _fontName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFontSize))]
    private int _fontSizePt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewForeground))]
    private string _fontColor;

    [ObservableProperty] private string _imagePath;
    [ObservableProperty] private string _imagePosition;
    [ObservableProperty] private bool   _imageTransparent;

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

    // ─── 条件プロパティ ───────────────────────────────────────────────────
    public bool IsApp         => Type == "app";
    public bool IsSystem      => Type == "system";
    public bool HasPath       => Type != "system";
    public bool HasArgs       => Type == "app";
    public bool HasWorkDir    => Type == "app";
    public bool HasSystemInfo => Type == "system";
    public bool SiHasDeviceType   => SiCategory == "usage";
    public bool SiHasAccent       => SiCategory is "storage" or "usage";
    public bool SiHasCircleFormat => SiCategory == "storage" || (SiCategory == "usage" && SiDeviceType != "lan");
    public bool SiHasTarget       => SiCategory == "storage" || (SiCategory == "usage" && SiDeviceType is "lan" or "gpu");

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
        _fontName    = source.FontName;
        _fontSizePt  = source.FontSizePt;
        _fontColor   = source.FontColor;
        _imagePath   = source.ImagePath;
        _imagePosition    = source.ImagePosition;
        _imageTransparent = source.ImageTransparent;

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

    public TileConfig ToConfig() => new()
    {
        Col     = Col,     Row     = Row,
        ColSpan = ColSpan, RowSpan = RowSpan,
        Type    = Type,    Title   = Title,
        Path    = Path,    Args    = Args,  WorkDir = WorkDir,
        Color   = Color,   Opacity = Opacity,
        FontName         = FontName,    FontSizePt  = FontSizePt,
        FontColor        = FontColor,   ImagePath   = ImagePath,
        ImagePosition    = ImagePosition,
        ImageTransparent = ImageTransparent,
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
}
