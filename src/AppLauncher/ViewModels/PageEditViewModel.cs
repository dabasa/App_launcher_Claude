using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Models;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class PageEditViewModel : ObservableObject
{
    [ObservableProperty] private string _backgroundColor;
    [ObservableProperty] private int    _backgroundOpacity;
    [ObservableProperty] private string _handleColor;
    [ObservableProperty] private int    _handleOpacity;
    [ObservableProperty] private string _pinFrameColor;
    [ObservableProperty] private int    _pinFrameOpacity;

    public IReadOnlyList<string> ColorOptions { get; } = [.. ColorPalette.All.Keys];

    public PageEditViewModel(PageViewModel source)
    {
        _backgroundColor   = source.BackgroundColor;
        _backgroundOpacity = source.BackgroundOpacity;
        _handleColor       = source.HandleColor;
        _handleOpacity     = source.HandleOpacity;
        _pinFrameColor     = source.PinFrameColor;
        _pinFrameOpacity   = source.PinFrameOpacity;
    }

    public PageConfig ToConfig(string pageName, IReadOnlyList<TileViewModel> tiles) => new()
    {
        Name              = pageName,
        BackgroundColor   = BackgroundColor,
        BackgroundOpacity = BackgroundOpacity,
        HandleColor       = HandleColor,
        HandleOpacity     = HandleOpacity,
        PinFrameColor     = PinFrameColor,
        PinFrameOpacity   = PinFrameOpacity,
        Tiles             = tiles.Select(t => t.ToConfig()).ToList(),
    };
}
