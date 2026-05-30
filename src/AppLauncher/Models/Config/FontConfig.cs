namespace AppLauncher.Models.Config;

public class FontConfig
{
    public string FontName     { get; set; } = "";
    public int    FontSizePt   { get; set; } = 16;
    public string FontColor    { get; set; } = "white";
    public bool   AutoFontSize { get; set; } = false;
}
