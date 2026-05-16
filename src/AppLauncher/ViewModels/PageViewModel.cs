using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class PageViewModel : ObservableObject
{
    public string Name { get; }
    public string BackgroundColor { get; }
    public int BackgroundOpacity { get; }
    public string HandleColor { get; }
    public int HandleOpacity { get; }
    public string PinFrameColor { get; }
    public int PinFrameOpacity { get; }
    public List<TileViewModel> Tiles { get; }

    public PageViewModel(PageConfig config)
    {
        Name = config.Name;
        BackgroundColor = config.BackgroundColor;
        BackgroundOpacity = config.BackgroundOpacity;
        HandleColor = config.HandleColor;
        HandleOpacity = config.HandleOpacity;
        PinFrameColor = config.PinFrameColor;
        PinFrameOpacity = config.PinFrameOpacity;
        Tiles = config.Tiles.Select(t => new TileViewModel(t)).ToList();
    }
}
