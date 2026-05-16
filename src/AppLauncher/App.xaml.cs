using System.Windows;
using AppLauncher.Services;
using AppLauncher.ViewModels;
using AppLauncher.Views;

namespace AppLauncher;

public partial class App : Application
{
    public static ConfigService ConfigService { get; } = new();
    public static LauncherViewModel? LauncherViewModel { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigService.Load();

        var vm = new LauncherViewModel(ConfigService.Current);
        LauncherViewModel = vm;
        var window = new LauncherWindow();
        var snapService = new SnapService();

        snapService.Attach(window);
        window.SetViewModel(vm);

        window.Show();

        // Show 後に吸着位置を計算（DPI スケール取得に PresentationSource が必要なため）
        snapService.ApplySnap();

        MainWindow = window;
    }
}
