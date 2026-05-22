using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using AppLauncher.Models.Config;

namespace AppLauncher.Services;

public sealed class SystemInfoService : IDisposable
{
    public static readonly SystemInfoService Instance = new();

    private readonly object _lock = new();

    private readonly PerformanceCounter _cpuTotal;

    // LAN カウンターキャッシュ（インスタンス名 → (受信, 送信)）
    private readonly Dictionary<string, (PerformanceCounter rx, PerformanceCounter tx)> _lanCounters = new();

    // GPU 名キャッシュ
    private string[]? _gpuNames;

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
        float pct;
        lock (_lock) { pct = _cpuTotal.NextValue(); }
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

    // ─── GPU ─────────────────────────────────────────────────────────────────
    private SystemData GetGpuData(SystemInfoConfig cfg)
    {
        int    physIndex = ParseGpuPhysIndex(cfg.Target);
        string gpuName   = GetGpuName(physIndex);
        double pct       = ReadGpuUsageWmi(physIndex);
        return new SystemData($"GPU {pct:F1} %", gpuName, pct, pct >= cfg.Threshold);
    }

    private static double ReadGpuUsageWmi(int physIndex)
    {
        try
        {
            string physTag = $"phys_{physIndex}_";
            using var s = new ManagementObjectSearcher(
                "SELECT Name, UtilizationPercentage " +
                "FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            double total = 0;
            foreach (ManagementObject o in s.Get())
            {
                string? name = o["Name"]?.ToString();
                if (name == null || !name.Contains(physTag) || !name.Contains("engtype_3D")) continue;
                total += Convert.ToDouble(o["UtilizationPercentage"]);
            }
            return Math.Min(100.0, total);
        }
        catch { return 0; }
    }

    private string GetGpuName(int physIndex)
    {
        lock (_lock)
        {
            if (_gpuNames == null)
            {
                var names = new List<string>();
                try
                {
                    using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                    foreach (ManagementObject o in s.Get())
                        names.Add(o["Name"]?.ToString() ?? "");
                }
                catch { }
                _gpuNames = names.ToArray();
            }
            return physIndex < _gpuNames.Length && !string.IsNullOrEmpty(_gpuNames[physIndex])
                ? _gpuNames[physIndex]
                : $"GPU {physIndex}";
        }
    }

    private static int ParseGpuPhysIndex(string? target)
    {
        if (string.IsNullOrEmpty(target)) return 0;
        var m = Regex.Match(target, @"phys_(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    // ─── LAN ─────────────────────────────────────────────────────────────────
    private SystemData GetLanData(SystemInfoConfig cfg)
    {
        try
        {
            string instName = ResolveLanInstance(cfg.Target);
            if (string.IsNullOrEmpty(instName))
                return new SystemData("LAN N/A", cfg.Target ?? "", 0, false);

            bool isNew;
            (PerformanceCounter rx, PerformanceCounter tx) pair;

            lock (_lock)
            {
                isNew = !_lanCounters.TryGetValue(instName, out pair);
                if (isNew)
                {
                    var rxC = new PerformanceCounter("Network Interface", "Bytes Received/sec", instName, true);
                    var txC = new PerformanceCounter("Network Interface", "Bytes Sent/sec",     instName, true);
                    rxC.NextValue(); // プライミング
                    txC.NextValue();
                    pair = (rxC, txC);
                    _lanCounters[instName] = pair;
                }
            }

            if (isNew) return new SystemData("↓ - KB/s  ↑ - KB/s", instName, 0, false);

            float rx, tx;
            lock (_lock)
            {
                rx = pair.rx.NextValue();
                tx = pair.tx.NextValue();
            }
            return new SystemData($"↓{rx / 1024.0:F0} KB/s  ↑{tx / 1024.0:F0} KB/s", instName, 0, false);
        }
        catch { return new SystemData("LAN N/A", "", 0, false); }
    }

    private static string ResolveLanInstance(string? target)
    {
        try
        {
            var instances = new PerformanceCounterCategory("Network Interface").GetInstanceNames();
            if (string.IsNullOrEmpty(target))
                return instances.FirstOrDefault() ?? "";

            return instances.FirstOrDefault(n => n.Equals(target, StringComparison.OrdinalIgnoreCase))
                ?? instances.FirstOrDefault(n => n.Contains(target, StringComparison.OrdinalIgnoreCase))
                ?? instances.FirstOrDefault(n => target.Contains(n, StringComparison.OrdinalIgnoreCase))
                ?? "";
        }
        catch { return ""; }
    }

    // ─── 選択肢列挙 ──────────────────────────────────────────────────────────
    public static IEnumerable<string> GetDriveTargets()
        => DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name.TrimEnd('\\', '/'));

    public static IEnumerable<string> GetLanTargets()
    {
        try
        {
            return new PerformanceCounterCategory("Network Interface")
                .GetInstanceNames()
                .OrderBy(n => n)
                .ToList();
        }
        catch { return []; }
    }

    public static IEnumerable<string> GetGpuTargets()
    {
        var result   = new List<string>();
        var physIdxs = new SortedSet<int>();

        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
            foreach (ManagementObject o in s.Get())
            {
                string? name = o["Name"]?.ToString();
                if (name == null) continue;
                var m = Regex.Match(name, @"phys_(\d+)");
                if (m.Success) physIdxs.Add(int.Parse(m.Groups[1].Value));
            }
        }
        catch { }

        var gpuNames = new List<string>();
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject o in s.Get())
                gpuNames.Add(o["Name"]?.ToString() ?? "");
        }
        catch { }

        if (physIdxs.Count == 0)
            for (int i = 0; i < gpuNames.Count; i++) physIdxs.Add(i);

        foreach (int idx in physIdxs)
        {
            string label = idx < gpuNames.Count && !string.IsNullOrEmpty(gpuNames[idx])
                ? $"phys_{idx}  {gpuNames[idx]}"
                : $"phys_{idx}";
            result.Add(label);
        }
        return result;
    }

    public void Dispose()
    {
        _cpuTotal.Dispose();
        lock (_lock)
        {
            foreach (var (rx, tx) in _lanCounters.Values)
            {
                try { rx.Dispose(); } catch { }
                try { tx.Dispose(); } catch { }
            }
        }
    }
}

public record SystemData(
    string MainText,
    string SubText,
    double Percentage,
    bool   ThresholdExceeded);
