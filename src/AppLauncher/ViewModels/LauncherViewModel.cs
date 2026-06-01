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
    [NotifyPropertyChangedFor(nameof(DisplayPage))]
    private int _currentPageIndex;

    [ObservableProperty]
    private AppMode _mode = AppMode.Normal;

    public event Action? ModeChanging;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isStored;

    [ObservableProperty]
    private TileViewModel? _editingTile;

    [ObservableProperty]
    private TileViewModel? _pendingDeleteTile;

    [ObservableProperty]
    private TileEditViewModel? _editingTileVm;

    [ObservableProperty]
    private PageEditViewModel? _pageEditVm;

    [ObservableProperty]
    private GlobalSettingsViewModel? _globalSettingsVm;

    partial void OnGlobalSettingsVmChanged(GlobalSettingsViewModel? oldValue, GlobalSettingsViewModel? newValue)
    {
        if (oldValue != null)
            oldValue.PropertyChanged -= OnGlobalSettingsVmPropertyChanged;
        if (newValue != null)
            newValue.PropertyChanged += OnGlobalSettingsVmPropertyChanged;
    }

    private void OnGlobalSettingsVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GlobalSettingsViewModel.PageNameFontSizePt))
            OnPropertyChanged(nameof(PageNameFontSize));
    }

    private bool    _pinnedBeforeEdit;
    private AppMode _modeBeforeGlobalSettings = AppMode.Settings;
    private AppMode _previousMode             = AppMode.Normal;

    private int _settingsPageIndex;
    public int SettingsPageIndex
    {
        get => _settingsPageIndex;
        set
        {
            _settingsPageIndex = Math.Clamp(value, 0, Math.Max(0, SettingsPages.Count - 1));
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayPage));
        }
    }

    private static readonly (string title, string path)[] SettingsTileDefs =
    [
        ("全体設定", "settings://globalSettings"),
    ];

    public List<PageViewModel> SettingsPages { get; } = [];

    public ObservableCollection<PageViewModel> Pages { get; }

    public double PageNameFontSize =>
        (Mode == AppMode.GlobalSettings && GlobalSettingsVm != null)
            ? GlobalSettingsVm.PageNameFontSizePt * 4.0 / 3.0
            : App.ConfigService.Current.Global.PageNameFontSizePt * 4.0 / 3.0;

    public PageViewModel CurrentPage => Pages[CurrentPageIndex];

    public PageViewModel DisplayPage =>
        Mode is AppMode.Settings or AppMode.GlobalSettings
            ? SettingsPages[Math.Clamp(_settingsPageIndex, 0, Math.Max(0, SettingsPages.Count - 1))]
            : CurrentPage;

    public LauncherViewModel(AppConfig config)
    {
        Pages = new ObservableCollection<PageViewModel>(
            config.Pages.Select(p => new PageViewModel(p)));
        BuildSettingsPages();
    }

    partial void OnModeChanging(AppMode value)
    {
        ModeChanging?.Invoke();
    }

    partial void OnModeChanged(AppMode value)
    {
        // Edit モードから TileEdit/PageEdit 以外（=編集完了）へ遷移したとき保存
        if (_previousMode == AppMode.Edit &&
            value != AppMode.TileEdit && value != AppMode.PageEdit)
        {
            SyncPagesToConfig();
            App.ConfigService.Save();
        }
        _previousMode = value;
        OnPropertyChanged(nameof(DisplayPage));
        OnPropertyChanged(nameof(PageNameFontSize));
    }

    public void ForceSave()
    {
        SyncPagesToConfig();
        App.ConfigService.Save();
    }

    private void BuildSettingsPages()
    {
        SettingsPages.Clear();
        int cols = App.ConfigService.Current.Global.TileCountCols;
        int rows = App.ConfigService.Current.Global.TileCountRows;
        int tilesPerPage = cols * rows;

        for (int start = 0; start < SettingsTileDefs.Length; start += tilesPerPage)
        {
            var pageConfig = new PageConfig { Name = "詳細設定" };
            int end = Math.Min(start + tilesPerPage, SettingsTileDefs.Length);
            for (int i = start; i < end; i++)
            {
                var (title, path) = SettingsTileDefs[i];
                int localIdx = i - start;
                pageConfig.Tiles.Add(new TileConfig
                {
                    Col        = localIdx % cols,
                    Row        = localIdx / cols,
                    ColSpan    = 1,
                    RowSpan    = 1,
                    Type       = "settings",
                    Title      = title,
                    Path       = path,
                    Color      = "gray",
                    Opacity    = 0,
                    FontSizePt = 16,
                    FontColor  = "white",
                });
            }
            SettingsPages.Add(new PageViewModel(pageConfig));
        }
    }

    [RelayCommand]
    private void NavigateToPage(int index)
    {
        if (index >= 0 && index < Pages.Count)
            CurrentPageIndex = index;
    }

    [RelayCommand]
    private void NavigateToSettingsPage(int index)
    {
        SettingsPageIndex = index;
    }

    [RelayCommand]
    private void ToggleMode()
    {
        switch (Mode)
        {
            case AppMode.Normal:
                _pinnedBeforeEdit = IsPinned;
                Mode     = AppMode.Edit;
                IsPinned = true;
                break;
            case AppMode.Edit:
                Mode     = AppMode.Settings;
                IsPinned = true;
                break;
            case AppMode.Settings:
                Mode     = AppMode.Normal;
                IsPinned = _pinnedBeforeEdit;
                break;
        }
    }

    [RelayCommand]
    private void OpenTileEdit(TileViewModel tile)
    {
        EditingTile   = tile;
        EditingTileVm = new TileEditViewModel(tile);
        Mode = AppMode.TileEdit;
    }

    [RelayCommand]
    private void ConfirmTileEdit()
    {
        if (EditingTileVm == null || EditingTile == null) return;
        var newConfig = EditingTileVm.ToConfig();
        int idx = CurrentPage.Tiles.IndexOf(EditingTile);
        EditingTile   = null;
        EditingTileVm = null;
        if (idx >= 0)
            CurrentPage.Tiles[idx] = new TileViewModel(newConfig);
        SyncPagesToConfig();
        App.ConfigService.Save();
        Mode = AppMode.Edit;
    }

    [RelayCommand]
    private void CloseTileEdit()
    {
        EditingTile   = null;
        EditingTileVm = null;
        Mode = AppMode.Edit;
    }

    [RelayCommand]
    private void OpenPageEdit()
    {
        PageEditVm = new PageEditViewModel(CurrentPage);
        Mode = AppMode.PageEdit;
    }

    [RelayCommand]
    private void ConfirmPageEdit()
    {
        if (PageEditVm == null) return;
        int idx = CurrentPageIndex;
        var newConfig = PageEditVm.ToConfig(Pages[idx].Name, Pages[idx].Tiles);
        PageEditVm = null;
        Pages[idx] = new PageViewModel(newConfig);
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(DisplayPage));
        SyncPagesToConfig();
        App.ConfigService.Save();
        Mode = AppMode.Edit;
    }

    [RelayCommand]
    private void ClosePageEdit()
    {
        PageEditVm = null;
        Mode = AppMode.Edit;
    }

    [RelayCommand]
    private void OpenGlobalSettings()
    {
        _modeBeforeGlobalSettings = Mode;
        GlobalSettingsVm = new GlobalSettingsViewModel();
        Mode = AppMode.GlobalSettings;
    }

    [RelayCommand]
    private void ConfirmGlobalSettings()
    {
        if (GlobalSettingsVm == null) return;
        GlobalSettingsVm.ApplyToConfig();
        App.ConfigService.Save();
        GlobalSettingsVm = null;
        BuildSettingsPages();
        OnPropertyChanged(nameof(DisplayPage));
        OnPropertyChanged(nameof(PageNameFontSize));
        Mode = _modeBeforeGlobalSettings;
    }

    [RelayCommand]
    private void CloseGlobalSettings()
    {
        GlobalSettingsVm = null;
        Mode = _modeBeforeGlobalSettings;
    }

    private void SyncPagesToConfig()
    {
        App.ConfigService.Current.Pages = Pages.Select(p => new PageConfig
        {
            Name              = p.Name,
            BackgroundColor   = p.BackgroundColor,
            BackgroundOpacity = p.BackgroundOpacity,
            HandleColor       = p.HandleColor,
            HandleOpacity     = p.HandleOpacity,
            PinFrameColor     = p.PinFrameColor,
            PinFrameOpacity   = p.PinFrameOpacity,
            Tiles = p.Tiles.Select(t => t.ToConfig()).ToList(),
        }).ToList();
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
        SyncPagesToConfig();
        App.ConfigService.Save();
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
        SyncPagesToConfig();
        App.ConfigService.Save();
    }

    [RelayCommand]
    private void RemovePage()
    {
        if (Pages.Count <= 1) return;
        if (CurrentPage.Tiles.Count > 0) return;
        int removed = CurrentPageIndex;
        CurrentPageIndex = removed > 0 ? removed - 1 : 0;
        Pages.RemoveAt(removed);
        SyncPagesToConfig();
        App.ConfigService.Save();
    }

    [RelayCommand]
    private void MovePageLeft()
    {
        if (CurrentPageIndex <= 0) return;
        int i = CurrentPageIndex;
        (Pages[i], Pages[i - 1]) = (Pages[i - 1], Pages[i]);
        CurrentPageIndex = i - 1;
        SyncPagesToConfig();
        App.ConfigService.Save();
    }

    [RelayCommand]
    private void MovePageRight()
    {
        if (CurrentPageIndex >= Pages.Count - 1) return;
        int i = CurrentPageIndex;
        (Pages[i], Pages[i + 1]) = (Pages[i + 1], Pages[i]);
        CurrentPageIndex = i + 1;
        SyncPagesToConfig();
        App.ConfigService.Save();
    }
}
