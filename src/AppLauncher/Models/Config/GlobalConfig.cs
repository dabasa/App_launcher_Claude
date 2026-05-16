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
    public int StorageDelayMs { get; set; } = 1000;
    public int AnimationDurationMs { get; set; } = 300;
}
