using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class TileViewModel : ObservableObject
{
    public int Col { get; }
    public int Row { get; }
    public int ColSpan { get; }
    public int RowSpan { get; }
    public string Type { get; }
    public string Title { get; }
    public string Path { get; }
    public string Args { get; }
    public string WorkDir { get; }
    public string Color { get; }
    public int Opacity { get; }
    public string FontName { get; }
    public int FontSizePt { get; }
    public string FontColor { get; }
    public string ImagePath { get; }
    public string ImagePosition { get; }
    public bool ImageTransparent { get; }
    public SystemInfoConfig? SystemInfo { get; }

    public TileViewModel(TileConfig config)
    {
        Col = config.Col;
        Row = config.Row;
        ColSpan = config.ColSpan;
        RowSpan = config.RowSpan;
        Type = config.Type;
        Title = config.Title;
        Path = config.Path;
        Args = config.Args;
        WorkDir = config.WorkDir;
        Color = config.Color;
        Opacity = config.Opacity;
        FontName = config.FontName;
        FontSizePt = config.FontSizePt;
        FontColor = config.FontColor;
        ImagePath = config.ImagePath;
        ImagePosition = config.ImagePosition;
        ImageTransparent = config.ImageTransparent;
        SystemInfo = config.SystemInfo;
    }
}
