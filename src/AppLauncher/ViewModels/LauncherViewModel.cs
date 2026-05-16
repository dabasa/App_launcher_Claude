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

    public List<PageViewModel> Pages { get; }

    public double PageNameFontSize { get; }

    public PageViewModel CurrentPage => Pages[CurrentPageIndex];

    public LauncherViewModel(AppConfig config)
    {
        Pages = config.Pages.Select(p => new PageViewModel(p)).ToList();
        PageNameFontSize = config.Global.PageNameFontSizePt * 4.0 / 3.0;
    }

    [RelayCommand]
    private void NavigateToPage(int index)
    {
        if (index >= 0 && index < Pages.Count)
            CurrentPageIndex = index;
    }
}
