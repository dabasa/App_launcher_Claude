using System.Diagnostics;
using AppLauncher.ViewModels;

namespace AppLauncher.Services;

public static class TileLaunchService
{
    public static void Launch(TileViewModel tile)
    {
        if (tile.Type == "settings")
        {
            HandleSettingsTile(tile.Path);
            return;
        }

        try
        {
            switch (tile.Type)
            {
                case "app":
                    if (!tile.Args.Contains("{drop}"))
                        LaunchApp(tile);
                    break;
                case "url":
                    Process.Start(new ProcessStartInfo(tile.Path) { UseShellExecute = true });
                    break;
                case "folder":
                    Process.Start(new ProcessStartInfo(
                        Environment.ExpandEnvironmentVariables(tile.Path))
                        { UseShellExecute = true });
                    break;
            }
        }
        catch (Exception) { /* 起動失敗は無視 */ }
    }

    private static void HandleSettingsTile(string path)
    {
        switch (path)
        {
            case "settings://globalSettings":
                App.LauncherViewModel?.OpenGlobalSettingsCommand.Execute(null);
                break;
        }
    }

    private static void LaunchApp(TileViewModel tile)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Environment.ExpandEnvironmentVariables(tile.Path),
            UseShellExecute = true,
        };
        if (!string.IsNullOrEmpty(tile.Args))
            psi.Arguments = tile.Args;
        if (!string.IsNullOrEmpty(tile.WorkDir))
            psi.WorkingDirectory = Environment.ExpandEnvironmentVariables(tile.WorkDir);
        Process.Start(psi);
    }

    public static void LaunchWithDrop(TileViewModel tile, string[] paths)
    {
        if (string.IsNullOrEmpty(tile.Path)) return;
        try
        {
            string dropArg = string.Join(" ", paths.Select(p => $"\"{p}\""));
            string args    = tile.Args.Replace("{drop}", dropArg);
            var psi = new ProcessStartInfo
            {
                FileName        = Environment.ExpandEnvironmentVariables(tile.Path),
                Arguments       = args,
                UseShellExecute = true,
            };
            if (!string.IsNullOrEmpty(tile.WorkDir))
                psi.WorkingDirectory = Environment.ExpandEnvironmentVariables(tile.WorkDir);
            Process.Start(psi);
        }
        catch (Exception) { }
    }
}
