namespace AppLauncher.Models.Config;

public class PageConfig
{
    public string Name { get; set; } = "ページ1";
    public string BackgroundColor { get; set; } = "gray";
    public int BackgroundOpacity { get; set; } = 30;
    public string HandleColor { get; set; } = "gray";
    public int HandleOpacity { get; set; } = 20;
    public string PinFrameColor { get; set; } = "white";
    public int PinFrameOpacity { get; set; } = 50;
    public List<TileConfig> Tiles { get; set; } = new();
}
