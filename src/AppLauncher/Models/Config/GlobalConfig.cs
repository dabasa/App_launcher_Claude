namespace AppLauncher.Models.Config;

public class GlobalConfig
{
    public bool TaskTrayResident { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public int TileCountCols { get; set; } = 6;
    public int TileCountRows { get; set; } = 4;
    public int PageNameFontSizePt { get; set; } = 16;
    public string SnapPosition { get; set; } = "right";
    public int SnapMonitor { get; set; } = 1;
    public int StorageDelayMs { get; set; } = 100;
    public int AnimationDurationMs { get; set; } = 300;
    public bool AlwaysOnTop { get; set; } = true;
    public double? SavedWindowTop  { get; set; } = null;
    public double? SavedWindowLeft { get; set; } = null;
}
