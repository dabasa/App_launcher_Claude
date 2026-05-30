using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class TileViewModel : ObservableObject
{
    [ObservableProperty] private int _col;
    [ObservableProperty] private int _row;
    [ObservableProperty] private int _colSpan;
    [ObservableProperty] private int _rowSpan;

    public string Type             { get; }
    public string Title            { get; }
    public string Path             { get; }
    public string Args             { get; }
    public string WorkDir          { get; }
    public string Color            { get; }
    public int    Opacity          { get; }
    public string ImagePath        { get; }
    public string ImagePosition    { get; }
    public bool   ImageTransparent { get; }

    // ─── フォント設定 ────────────────────────────────────────────────────
    public FontConfig  TitleFont   { get; }
    public FontConfig? ContentFont { get; }

    // 既存コード（TileControl / SystemTileControl）との後方互換プロパティ
    public string FontName     => TitleFont.FontName;
    public int    FontSizePt   => TitleFont.FontSizePt;
    public string FontColor    => TitleFont.FontColor;
    public bool   AutoFontSize => TitleFont.AutoFontSize;

    public SystemInfoConfig? SystemInfo { get; }

    public TileViewModel(TileConfig config)
    {
        _col     = config.Col;
        _row     = config.Row;
        _colSpan = config.ColSpan;
        _rowSpan = config.RowSpan;
        Type          = config.Type;
        Title         = config.Title;
        Path          = config.Path;
        Args          = config.Args;
        WorkDir       = config.WorkDir;
        Color         = config.Color;
        Opacity       = config.Opacity;
        ImagePath        = config.ImagePath;
        ImagePosition    = config.ImagePosition;
        ImageTransparent = config.ImageTransparent;
        TitleFont        = config.TitleFont   ?? new FontConfig();
        ContentFont      = config.ContentFont;
        SystemInfo       = config.SystemInfo;
    }

    public static TileViewModel CreateNew(int col, int row) => new(new TileConfig
    {
        Col     = col,  Row     = row,
        ColSpan = 1,    RowSpan = 1,
        Type    = "app",
        Color   = "blue", Opacity = 20,
        TitleFont     = new FontConfig { FontSizePt = 16, FontColor = "white" },
        ImagePosition = "top",
    });

    public TileConfig ToConfig() => new()
    {
        Col    = Col,     Row    = Row,
        ColSpan = ColSpan, RowSpan = RowSpan,
        Type   = Type,    Title  = Title,
        Path   = Path,    Args   = Args,    WorkDir = WorkDir,
        Color  = Color,   Opacity = Opacity,
        ImagePath        = ImagePath,
        ImagePosition    = ImagePosition,
        ImageTransparent = ImageTransparent,
        TitleFont        = TitleFont,
        ContentFont      = ContentFont,
        SystemInfo       = SystemInfo,
    };
}
