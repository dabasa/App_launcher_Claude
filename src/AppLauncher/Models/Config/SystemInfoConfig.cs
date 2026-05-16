namespace AppLauncher.Models.Config;

public class SystemInfoConfig
{
    public string Category { get; set; } = "usage";
    public string DeviceType { get; set; } = "cpu";
    public string Target { get; set; } = "CPU(0)";
    public string DisplayFormat { get; set; } = "text";
    public string MainColor { get; set; } = "blue";
    public string AccentColor { get; set; } = "orange";
    public string BackgroundAccent { get; set; } = "red";
    public int Threshold { get; set; } = 80;
    public string ClickAction { get; set; } = "refresh";
    public int UpdateIntervalMs { get; set; } = 60000;
}
