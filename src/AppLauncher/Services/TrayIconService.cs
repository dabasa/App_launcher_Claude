using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using AppLauncher.ViewModels;
using AppLauncher.Views;

namespace AppLauncher.Services;

public class TrayIconService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private LauncherWindow? _window;

    public void Attach(LauncherWindow window)
    {
        _window   = window;
        _trayIcon = (TaskbarIcon)Application.Current.Resources["TrayIcon"];
        _trayIcon.TrayMouseDoubleClick += OnTrayDoubleClick;
    }

    private void OnTrayDoubleClick(object sender, RoutedEventArgs e)
    {
        var vm = App.LauncherViewModel;
        if (vm == null) return;

        if (vm.IsStored)
        {
            App.SnapService?.ForceExpand(() => PinToggle(vm));
            return;
        }
        PinToggle(vm);
    }

    private void PinToggle(LauncherViewModel vm)
    {
        _window?.Activate();
        vm.IsPinned = !vm.IsPinned;
    }

    public void Dispose() => _trayIcon?.Dispose();
}
