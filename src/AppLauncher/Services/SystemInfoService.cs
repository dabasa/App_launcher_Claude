using System.Diagnostics;
using System.IO;
using System.Management;
using AppLauncher.Models.Config;

namespace AppLauncher.Services;

public sealed class SystemInfoService : IDisposable
{
    public static readonly SystemInfoService Instance = new();

    private readonly PerformanceCounter _cpuTotal;

    private SystemInfoService()
    {
        _cpuTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuTotal.NextValue(); // 初回値は 0 のため捨てる
    }

    public SystemData GetData(SystemInfoConfig cfg) => cfg.Category switch
    {
        "os"      => GetOsData(),
        "storage" => GetStorageData(cfg),
        "usage"   => GetUsageData(cfg),
        _         => new SystemData("N/A", "", 0, false),
    };

    // ─── OS 情報 ─────────────────────────────────────────────────────────────
    private static SystemData GetOsData()
    {
        string caption = "Windows";
        string version = Environment.OSVersion.Version.ToString();
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
            foreach (ManagementObject o in s.Get())
                caption = o["Caption"]?.ToString() ?? caption;
        }
        catch { }
        return new SystemData(caption, version, 0, false);
    }

    // ─── ストレージ ───────────────────────────────────────────────────────────
    private static SystemData GetStorageData(SystemInfoConfig cfg)
    {
        var target = string.IsNullOrEmpty(cfg.Target) ? "C:" : cfg.Target;
        try
        {
            string driveLetter = target.TrimEnd('\\', '/');
            if (!driveLetter.EndsWith(':')) driveLetter += ':';
            var drive = new DriveInfo(driveLetter);
            if (!drive.IsReady) return new SystemData($"{driveLetter} 未マウント", "", 0, false);

            double totalGb = drive.TotalSize / 1_073_741_824.0;
            double usedGb  = (drive.TotalSize - drive.AvailableFreeSpace) / 1_073_741_824.0;
            double pct     = usedGb / totalGb * 100.0;
            return new SystemData(
                $"{driveLetter} {usedGb:F1}/{totalGb:F1} GB",
                $"{pct:F1} %",
                pct,
                pct >= cfg.Threshold);
        }
        catch { return new SystemData($"{target} エラー", "", 0, false); }
    }

    // ─── 使用率 ───────────────────────────────────────────────────────────────
    private SystemData GetUsageData(SystemInfoConfig cfg) => cfg.DeviceType switch
    {
        "cpu"    => GetCpuData(cfg),
        "memory" => GetMemoryData(cfg),
        "gpu"    => GetGpuData(cfg),
        "lan"    => GetLanData(cfg),
        _        => new SystemData("N/A", "", 0, false),
    };

    private SystemData GetCpuData(SystemInfoConfig cfg)
    {
        float pct = _cpuTotal.NextValue();
        return new SystemData($"CPU {pct:F1} %", cfg.Target, pct, pct >= cfg.Threshold);
    }

    private static SystemData GetMemoryData(SystemInfoConfig cfg)
    {
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject o in s.Get())
            {
                ulong totalKb  = (ulong)o["TotalVisibleMemorySize"];
                ulong freeKb   = (ulong)o["FreePhysicalMemory"];
                double usedGb  = (totalKb - freeKb) / 1_048_576.0;
                double totalGb = totalKb / 1_048_576.0;
                double pct     = (totalKb - freeKb) / (double)totalKb * 100.0;
                return new SystemData(
                    $"MEM {usedGb:F1}/{totalGb:F1} GB",
                    $"{pct:F1} %",
                    pct,
                    pct >= cfg.Threshold);
            }
        }
        catch { }
        return new SystemData("MEM N/A", "", 0, false);
    }

    private static SystemData GetGpuData(SystemInfoConfig cfg)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject o in s.Get())
            {
                string name = o["Name"]?.ToString() ?? "GPU";
                return new SystemData("GPU N/A", name, 0, false);
            }
        }
        catch { }
        return new SystemData("GPU N/A", "", 0, false);
    }

    private static SystemData GetLanData(SystemInfoConfig cfg)
    {
        try
        {
            string? ifName = string.IsNullOrEmpty(cfg.Target) ? null : cfg.Target;
            using var s = new ManagementObjectSearcher(
                "SELECT Name, BytesReceivedPersec, BytesSentPersec FROM " +
                "Win32_PerfFormattedData_Tcpip_NetworkInterface");
            foreach (ManagementObject o in s.Get())
            {
                string name = o["Name"]?.ToString() ?? "";
                if (ifName != null && !name.Contains(ifName, StringComparison.OrdinalIgnoreCase)) continue;
                ulong rx   = (ulong)o["BytesReceivedPersec"];
                ulong tx   = (ulong)o["BytesSentPersec"];
                string main = $"↓{rx / 1024.0:F0} KB/s  ↑{tx / 1024.0:F0} KB/s";
                return new SystemData(main, name, 0, false);
            }
        }
        catch { }
        return new SystemData("LAN N/A", "", 0, false);
    }

    public void Dispose() => _cpuTotal.Dispose();
}

public record SystemData(
    string MainText,
    string SubText,
    double Percentage,
    bool   ThresholdExceeded);
