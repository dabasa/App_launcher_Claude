namespace AppLauncher.Models.Config;

public class AppConfig
{
    public GlobalConfig Global { get; set; } = new();
    public LayoutConfig Layout { get; set; } = new();
    public List<PageConfig> Pages { get; set; } = new();
}
