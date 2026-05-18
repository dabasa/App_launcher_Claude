using Microsoft.Win32;
using System.Reflection;

namespace AppLauncher.Helpers;

public static class StartupRegistryHelper
{
    private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AppLauncher";

    public static void Apply(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key == null) return;

        if (enable)
        {
            string? exePath = Assembly.GetExecutingAssembly().Location
                .Replace(".dll", ".exe");
            if (exePath != null)
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
