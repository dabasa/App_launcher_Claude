using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Helpers;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class GlobalSettingsViewModel : ObservableObject
{
    [ObservableProperty] private bool   _taskTrayResident;
    [ObservableProperty] private bool   _startWithWindows;
    [ObservableProperty] private int    _tileCountCols;
    [ObservableProperty] private int    _tileCountRows;
    [ObservableProperty] private int    _tileMargin;
    [ObservableProperty] private int    _pageNameFontSizePt;
    [ObservableProperty] private string _snapPosition = "right";
    [ObservableProperty] private int    _snapMonitor;
    [ObservableProperty] private int    _storageDelayMs;

    public string[] SnapPositions { get; } = ["right", "left", "top", "bottom"];

    public int   MaxMonitor       => App.SnapService?.GetMonitorCount() ?? 1;
    public int[] SnapMonitorItems => Enumerable.Range(1, MaxMonitor).ToArray();

    public GlobalSettingsViewModel()
    {
        var g = App.ConfigService.Current.Global;
        var l = App.ConfigService.Current.Layout;
        TaskTrayResident   = g.TaskTrayResident;
        StartWithWindows   = g.StartWithWindows;
        TileCountCols      = g.TileCountCols;
        TileCountRows      = g.TileCountRows;
        TileMargin         = l.TileMargin;
        PageNameFontSizePt = g.PageNameFontSizePt;
        SnapPosition       = g.SnapPosition;
        SnapMonitor        = g.SnapMonitor;
        StorageDelayMs     = g.StorageDelayMs;
    }

    public void ApplyToConfig()
    {
        var g = App.ConfigService.Current.Global;
        var l = App.ConfigService.Current.Layout;

        // 縮小制限チェック：配置済みタイルの最大座標を下回れない
        int minCols = App.ConfigService.Current.Pages
            .SelectMany(p => p.Tiles)
            .Select(t => t.Col + t.ColSpan)
            .DefaultIfEmpty(1).Max();
        int minRows = App.ConfigService.Current.Pages
            .SelectMany(p => p.Tiles)
            .Select(t => t.Row + t.RowSpan)
            .DefaultIfEmpty(1).Max();

        g.TaskTrayResident  = TaskTrayResident;
        g.StartWithWindows  = StartWithWindows;
        g.TileCountCols     = Math.Max(TileCountCols, minCols);
        g.TileCountRows     = Math.Max(TileCountRows, minRows);
        l.TileMargin        = TileMargin;
        g.PageNameFontSizePt = PageNameFontSizePt;
        g.SnapPosition      = SnapPosition;
        g.SnapMonitor       = SnapMonitor;
        g.StorageDelayMs    = StorageDelayMs;

        StartupRegistryHelper.Apply(StartWithWindows);
        App.SnapService?.ApplySnap();
    }
}
