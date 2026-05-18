using System.Windows;
using AppLauncher.Services;
using AppLauncher.ViewModels;
using AppLauncher.Views;

namespace AppLauncher;

public partial class App : Application
{
    public static ConfigService ConfigService { get; } = new();
    public static LauncherViewModel? LauncherViewModel { get; private set; }
    public static SnapService? SnapService { get; private set; }

    private TrayIconService? _trayIconService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigService.Load();

        var vm     = new LauncherViewModel(ConfigService.Current);
        LauncherViewModel = vm;
        var window = new LauncherWindow();
        var snap   = new SnapService();
        SnapService = snap;

        snap.Attach(window);
        window.SetViewModel(vm);
        window.Show();

        // Show 後に吸着位置を計算（DPI スケール取得に PresentationSource が必要なため）
        snap.ApplySnap();

        _trayIconService = new TrayIconService();
        _trayIconService.Attach(window);

        MainWindow = window;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        base.OnExit(e);
    }

    private void OnSnapRight (object s, RoutedEventArgs e) => ChangeSnap("right");
    private void OnSnapLeft  (object s, RoutedEventArgs e) => ChangeSnap("left");
    private void OnSnapTop   (object s, RoutedEventArgs e) => ChangeSnap("top");
    private void OnSnapBottom(object s, RoutedEventArgs e) => ChangeSnap("bottom");

    private void ChangeSnap(string direction)
    {
        if (LauncherViewModel?.IsStored == true)
            SnapService?.ForceExpand(() => ApplyNewSnap(direction));
        else
            ApplyNewSnap(direction);
    }

    private static void ApplyNewSnap(string direction)
    {
        ConfigService.Current.Global.SnapPosition = direction;
        ConfigService.Save();
        SnapService?.ApplySnap();
    }

    private void OnTogglePin(object s, RoutedEventArgs e)
    {
        if (LauncherViewModel != null)
            LauncherViewModel.IsPinned = !LauncherViewModel.IsPinned;
    }

    private void OnExit(object s, RoutedEventArgs e)
    {
        _trayIconService?.Dispose();
        Shutdown();
    }
}
