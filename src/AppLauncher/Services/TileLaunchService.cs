using System.Diagnostics;
using AppLauncher.ViewModels;

namespace AppLauncher.Services;

public static class TileLaunchService
{
    public static void Launch(TileViewModel tile)
    {
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
}
