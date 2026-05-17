using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppLauncher.Models;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class LauncherViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private int _currentPageIndex;

    [ObservableProperty]
    private AppMode _mode = AppMode.Normal;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isStored;

    [ObservableProperty]
    private TileViewModel? _editingTile;

    [ObservableProperty]
    private TileViewModel? _pendingDeleteTile;

    private bool _pinnedBeforeEdit;

    public ObservableCollection<PageViewModel> Pages { get; }

    public double PageNameFontSize { get; }

    public PageViewModel CurrentPage => Pages[CurrentPageIndex];

    public LauncherViewModel(AppConfig config)
    {
        Pages = new ObservableCollection<PageViewModel>(
            config.Pages.Select(p => new PageViewModel(p)));
        PageNameFontSize = config.Global.PageNameFontSizePt * 4.0 / 3.0;
    }

    [RelayCommand]
    private void NavigateToPage(int index)
    {
        if (index >= 0 && index < Pages.Count)
            CurrentPageIndex = index;
    }

    [RelayCommand]
    private void ToggleMode()
    {
        if (Mode == AppMode.Normal)
        {
            _pinnedBeforeEdit = IsPinned;
            Mode     = AppMode.Edit;
            IsPinned = true;
        }
        else
        {
            Mode     = AppMode.Normal;
            IsPinned = _pinnedBeforeEdit;
        }
    }

    [RelayCommand]
    private void OpenTileEdit(TileViewModel tile)
    {
        EditingTile = tile;
        Mode = AppMode.TileEdit;
    }

    [RelayCommand]
    private void CloseTileEdit()
    {
        EditingTile = null;
        Mode = AppMode.Edit;
    }

    [RelayCommand]
    private void RequestDeleteTile(TileViewModel tile)
    {
        PendingDeleteTile = tile;
    }

    [RelayCommand]
    private void ConfirmDeleteTile()
    {
        if (PendingDeleteTile == null) return;
        CurrentPage.Tiles.Remove(PendingDeleteTile);
        PendingDeleteTile = null;
    }

    [RelayCommand]
    private void CancelDeleteTile()
    {
        PendingDeleteTile = null;
    }

    [RelayCommand]
    private void AddPage()
    {
        if (Pages.Count >= 10) return;
        var src = CurrentPage;
        var cfg = new PageConfig
        {
            Name              = $"ページ{Pages.Count + 1}",
            BackgroundColor   = src.BackgroundColor,
            BackgroundOpacity = src.BackgroundOpacity,
            HandleColor       = src.HandleColor,
            HandleOpacity     = src.HandleOpacity,
            PinFrameColor     = src.PinFrameColor,
            PinFrameOpacity   = src.PinFrameOpacity,
        };
        int insertAt = CurrentPageIndex + 1;
        Pages.Insert(insertAt, new PageViewModel(cfg));
        CurrentPageIndex = insertAt;
    }

    [RelayCommand]
    private void RemovePage()
    {
        if (Pages.Count <= 1) return;
        if (CurrentPage.Tiles.Count > 0) return;
        int removed = CurrentPageIndex;
        CurrentPageIndex = removed > 0 ? removed - 1 : 0;
        Pages.RemoveAt(removed);
    }

    [RelayCommand]
    private void MovePageLeft()
    {
        if (CurrentPageIndex <= 0) return;
        int i = CurrentPageIndex;
        (Pages[i], Pages[i - 1]) = (Pages[i - 1], Pages[i]);
        CurrentPageIndex = i - 1;
    }

    [RelayCommand]
    private void MovePageRight()
    {
        if (CurrentPageIndex >= Pages.Count - 1) return;
        int i = CurrentPageIndex;
        (Pages[i], Pages[i + 1]) = (Pages[i + 1], Pages[i]);
        CurrentPageIndex = i + 1;
    }
}
