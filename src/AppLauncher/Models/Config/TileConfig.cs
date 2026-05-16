using System.Text.Json.Serialization;

namespace AppLauncher.Models.Config;

public class TileConfig
{
    public int Col { get; set; }
    public int Row { get; set; }
    public int ColSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string Type { get; set; } = "app";
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
    public string WorkDir { get; set; } = "";
    public string Color { get; set; } = "blue";
    public int Opacity { get; set; } = 0;
    public string FontName { get; set; } = "";
    public int FontSizePt { get; set; } = 16;
    public string FontColor { get; set; } = "white";
    public string ImagePath { get; set; } = "";
    public string ImagePosition { get; set; } = "top";
    public bool ImageTransparent { get; set; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SystemInfoConfig? SystemInfo { get; set; }
}
