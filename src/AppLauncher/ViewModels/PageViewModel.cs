using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class PageViewModel : ObservableObject
{
    [ObservableProperty] private string _name;

    public string BackgroundColor   { get; }
    public int    BackgroundOpacity { get; }
    public string HandleColor       { get; }
    public int    HandleOpacity     { get; }
    public string PinFrameColor     { get; }
    public int    PinFrameOpacity   { get; }

    public ObservableCollection<TileViewModel> Tiles { get; }

    public PageViewModel(PageConfig config)
    {
        _name             = config.Name;
        BackgroundColor   = config.BackgroundColor;
        BackgroundOpacity = config.BackgroundOpacity;
        HandleColor       = config.HandleColor;
        HandleOpacity     = config.HandleOpacity;
        PinFrameColor     = config.PinFrameColor;
        PinFrameOpacity   = config.PinFrameOpacity;
        Tiles = new ObservableCollection<TileViewModel>(
            config.Tiles.Select(t => new TileViewModel(t)));
    }
}
