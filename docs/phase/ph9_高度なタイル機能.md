# Ph.9 実装指示書 ─ 高度なタイル機能

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.9 高度なタイル機能 |
| 実装目的 | D&D 専用タイル・system タイル・GIF アニメーション・webview タイルの実装 |
| 未確定事項 | なし |
| 既存影響 | `TileControl` に画像表示・GIF・ファイルドロップを追加。`TileGridControl` のタイル生成ロジックをタイプ別に分岐。新規 NuGet パッケージ 2 本追加 |
| 追加ライブラリ | `Microsoft.Web.WebView2`・`System.Management` |
| 実装範囲 | D&D 専用タイルのファイルドロップ起動 / 静的画像・GIF アニメーション表示 / system タイル情報取得と描画 / webview タイル（ページ切替時ロード継続） / 編集モードでのファイルドロップによるタイル自動作成 |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `AppLauncher.csproj` | 変更：NuGet 2 本追加 |
| `Services/SystemInfoService.cs` | 新規：CPU / メモリ / GPU / LAN / ストレージ / OS 情報取得 |
| `Services/TileLaunchService.cs` | 変更：`LaunchWithDrop` 追加 |
| `Views/Controls/TileControl.xaml` | 変更：画像・GIF 表示要素追加 |
| `Views/Controls/TileControl.xaml.cs` | 変更：画像表示・GIF 再生・ファイルドロップ処理 |
| `Views/Controls/SystemTileControl.xaml` | 新規：system タイル UI（テキスト / 円形グラフ） |
| `Views/Controls/SystemTileControl.xaml.cs` | 新規：タイマー・データ取得・アクセント切替・描画 |
| `Views/Controls/WebViewTileControl.xaml` | 新規：webview タイル UI |
| `Views/Controls/WebViewTileControl.xaml.cs` | 新規：WebView2 制御・ページ間キャッシュ管理 |
| `Views/Controls/TileGridControl.xaml.cs` | 変更：タイプ別 Control 生成・編集モードでのファイルドロップ |

---

## 実装仕様

### 1. AppLauncher.csproj の変更

既存の `<ItemGroup>` に以下を追加する。

```xml
<PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2903.40" />
<PackageReference Include="System.Management" Version="9.0.0" />
```

---

### 2. Services/SystemInfoService.cs（新規）

#### 概要

各種システム情報を取得して返すシングルトンサービス。`PerformanceCounter`（CPU / LAN）と WMI（メモリ / GPU / OS）と `DriveInfo`（ストレージ）を使用する。

#### 戻り値型

```csharp
namespace AppLauncher.Services;

/// <param name="MainText">テキスト表示時の上段（例："CPU 42.3 %"）</param>
/// <param name="SubText">テキスト表示時の下段（例："i9-14900K"）</param>
/// <param name="Percentage">円形グラフ用 0〜100（使用率・使用割合）</param>
/// <param name="ThresholdExceeded">Percentage が Threshold を超えているか</param>
public record SystemData(string MainText, string SubText, double Percentage, bool ThresholdExceeded);
```

#### クラス実装

```csharp
using System.Diagnostics;
using System.Management;
using AppLauncher.Models.Config;

namespace AppLauncher.Services;

public sealed class SystemInfoService : IDisposable
{
    public static readonly SystemInfoService Instance = new();

    // CPU PerformanceCounter（初回値は 0 のため初期化時に捨てる）
    private readonly PerformanceCounter _cpuTotal;

    private SystemInfoService()
    {
        _cpuTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _cpuTotal.NextValue();
    }

    public SystemData GetData(SystemInfoConfig cfg) => cfg.Category switch
    {
        "os"      => GetOsData(),
        "storage" => GetStorageData(cfg),
        "usage"   => GetUsageData(cfg),
        _         => new SystemData("N/A", "", 0, false),
    };

    // ─── OS 情報 ──────────────────────────────────────────────────────────
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
        catch { /* WMI 失敗時はフォールバック */ }
        return new SystemData(caption, version, 0, false);
    }

    // ─── ストレージ ───────────────────────────────────────────────────────
    private static SystemData GetStorageData(SystemInfoConfig cfg)
    {
        var target = string.IsNullOrEmpty(cfg.Target) ? "C:" : cfg.Target;
        try
        {
            // "C:" 形式にドライブ文字だけを取り出す
            var driveLetter = target.TrimEnd('\\').TrimEnd('/');
            if (!driveLetter.EndsWith(':')) driveLetter += ':';
            var drive = new DriveInfo(driveLetter);
            if (!drive.IsReady) return new SystemData($"{driveLetter} 未マウント", "", 0, false);

            double totalGb = drive.TotalSize / 1073741824.0;
            double usedGb  = (drive.TotalSize - drive.AvailableFreeSpace) / 1073741824.0;
            double pct     = usedGb / totalGb * 100.0;
            string main    = $"{driveLetter} {usedGb:F1}/{totalGb:F1} GB";
            string sub     = $"{pct:F1} %";
            return new SystemData(main, sub, pct, pct >= cfg.Threshold);
        }
        catch { return new SystemData($"{target} エラー", "", 0, false); }
    }

    // ─── 使用率（CPU / メモリ / GPU / LAN）────────────────────────────────
    private SystemData GetUsageData(SystemInfoConfig cfg)
    {
        return cfg.DeviceType switch
        {
            "cpu"    => GetCpuData(cfg),
            "memory" => GetMemoryData(cfg),
            "gpu"    => GetGpuData(cfg),
            "lan"    => GetLanData(cfg),
            _        => new SystemData("N/A", "", 0, false),
        };
    }

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
                ulong totalKb = (ulong)o["TotalVisibleMemorySize"];
                ulong freeKb  = (ulong)o["FreePhysicalMemory"];
                double usedGb  = (totalKb - freeKb) / 1048576.0;
                double totalGb = totalKb / 1048576.0;
                double pct     = (totalKb - freeKb) / (double)totalKb * 100.0;
                return new SystemData($"MEM {usedGb:F1}/{totalGb:F1} GB", $"{pct:F1} %", pct, pct >= cfg.Threshold);
            }
        }
        catch { /* フォールバック */ }
        return new SystemData("MEM N/A", "", 0, false);
    }

    private static SystemData GetGpuData(SystemInfoConfig cfg)
    {
        // GPU 使用率は WMI で取得できない環境が多いため 0 で返す（N/A 表示）
        // 将来的にベンダー API または PerfCounter 拡張で対応
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject o in s.Get())
            {
                string name = o["Name"]?.ToString() ?? "GPU";
                return new SystemData("GPU N/A", name, 0, false);
            }
        }
        catch { /* フォールバック */ }
        return new SystemData("GPU N/A", "", 0, false);
    }

    private static SystemData GetLanData(SystemInfoConfig cfg)
    {
        // 帯域に対する使用率は環境依存のため受信・送信バイト数を表示する
        try
        {
            var ifName = string.IsNullOrEmpty(cfg.Target) ? null : cfg.Target;
            using var s = new ManagementObjectSearcher(
                "SELECT Name, BytesReceivedPersec, BytesSentPersec FROM " +
                "Win32_PerfFormattedData_Tcpip_NetworkInterface");
            foreach (ManagementObject o in s.Get())
            {
                string name = o["Name"]?.ToString() ?? "";
                if (ifName != null && !name.Contains(ifName, StringComparison.OrdinalIgnoreCase)) continue;
                ulong rx = (ulong)o["BytesReceivedPersec"];
                ulong tx = (ulong)o["BytesSentPersec"];
                string main = $"↓{rx / 1024.0:F0} KB/s  ↑{tx / 1024.0:F0} KB/s";
                return new SystemData(main, name, 0, false);
            }
        }
        catch { /* フォールバック */ }
        return new SystemData("LAN N/A", "", 0, false);
    }

    public void Dispose()
    {
        _cpuTotal.Dispose();
    }
}
```

---

### 3. Services/TileLaunchService.cs の変更

以下のメソッドを追加する（既存コードはそのまま）。

```csharp
// ─── 追加：D&D ドロップ起動 ───────────────────────────────────────────────
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
    catch (Exception) { /* 起動失敗は無視 */ }
}
```

---

### 4. Views/Controls/TileControl.xaml の変更

#### 変更内容

`TileBorder` 内の `<Grid>` を以下に置き換える。既存の `DndOverlay` と `EditOverlay` は変更しない。

`TileImage`（画像・GIF 用 `Image`）とレイアウト用内部 Grid を追加し、`TitleText` は内部 Grid の中へ移動する。

```xml
<Border x:Name="TileBorder" CornerRadius="20">
    <Grid>
        <!-- 画像・テキストレイアウト（ImagePosition に応じてコードビハインドで Grid.Row/Column を設定） -->
        <Grid x:Name="LayoutGrid">
            <Grid.RowDefinitions>
                <RowDefinition Height="*"/>
                <RowDefinition Height="0"/>
            </Grid.RowDefinitions>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="0"/>
            </Grid.ColumnDefinitions>

            <Image x:Name="TileImage" Grid.Row="0" Grid.Column="0"
                   Stretch="Uniform" Margin="8" Visibility="Collapsed"/>

            <TextBlock x:Name="TitleText" Grid.Row="0" Grid.Column="0"
                       HorizontalAlignment="Center" VerticalAlignment="Center"
                       TextWrapping="Wrap" TextAlignment="Center" Padding="8"/>
        </Grid>

        <!-- D&D 専用オーバーレイ（通常は非表示） -->
        <Border x:Name="DndOverlay"
                Visibility="Collapsed"
                CornerRadius="10"
                HorizontalAlignment="Center"
                VerticalAlignment="Center"
                Padding="10,4">
            <TextBlock Text="D&amp;D専用"
                       FontSize="16"
                       Foreground="#F0F0F0"/>
        </Border>

        <!-- 編集モードオーバーレイ（編集モード中のマウスオーバーで表示） -->
        <Grid x:Name="EditOverlay" Visibility="Collapsed">
            <Border x:Name="EditButton"
                    Width="22" Height="22"
                    HorizontalAlignment="Left" VerticalAlignment="Top"
                    Margin="4" CornerRadius="4" Cursor="Hand"
                    Background="#CC333333">
                <TextBlock Text="✎" FontSize="11" Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <Border x:Name="DeleteButton"
                    Width="22" Height="22"
                    HorizontalAlignment="Right" VerticalAlignment="Top"
                    Margin="4" CornerRadius="4" Cursor="Hand"
                    Background="#CCE05252">
                <TextBlock Text="✕" FontSize="11" Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <Border x:Name="ResizeHandle"
                    Width="16" Height="16"
                    HorizontalAlignment="Right" VerticalAlignment="Bottom"
                    Margin="4" CornerRadius="3" Cursor="SizeNWSE"
                    Background="#CCF0F0F0"/>
        </Grid>
    </Grid>
</Border>
```

---

### 5. Views/Controls/TileControl.xaml.cs の変更

#### 変更概要

- `Apply()` に画像レイアウト設定・GIF 初期化を追加
- `Loaded` / `Unloaded` イベントで GIF タイマーを Start / Stop
- `AllowDrop = true` と `Drop` イベントで D&D ファイルドロップを処理

#### 追加フィールド

```csharp
private GifBitmapDecoder? _gifDecoder;
private int               _gifFrameIndex;
private DispatcherTimer?  _gifTimer;
private string            _imagePath = "";
```

#### コンストラクタへの追加

```csharp
// 既存の EditButton / DeleteButton / ResizeHandle 配線の後に追加
AllowDrop = true;
Drop     += OnFileDrop;
Loaded   += OnLoaded;
Unloaded += OnUnloaded;
```

#### Apply() への追加

`Apply()` メソッド末尾に以下を追加する（既存の背景色・タイトル設定はそのまま）。

```csharp
// 画像表示・レイアウト
_imagePath = tile.ImagePath;
SetupImageAndLayout(tile);
```

#### 追加メソッド群

```csharp
// ─── 画像レイアウト ─────────────────────────────────────────────────────
private void SetupImageAndLayout(TileViewModel tile)
{
    bool hasImage = !string.IsNullOrEmpty(tile.ImagePath);
    TileImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;

    if (hasImage)
    {
        double opacity = tile.ImageTransparent
            ? ColorPalette.OpacityToDouble(tile.Opacity)
            : 1.0;
        TileImage.Opacity = opacity;

        // GIF と静的画像を判別（Loaded 後に実際の描画を開始）
        string ext = System.IO.Path.GetExtension(tile.ImagePath).ToLowerInvariant();
        if (ext != ".gif")
            LoadStaticImage(tile.ImagePath);
    }

    ArrangeImageAndText(tile.ImagePosition, hasImage);
}

private static void LoadStaticImage(string path)
{
    // 静的画像はコンストラクタで即ロード（Loaded を待たず）
}

// ImagePosition に応じて LayoutGrid の Row/Column を切り替える
private void ArrangeImageAndText(string position, bool hasImage)
{
    // デフォルト：テキストのみ（1 行 1 列、セル (0,0)）
    LayoutGrid.RowDefinitions[1].Height    = new GridLength(0);
    LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(0);
    Grid.SetRow(TitleText, 0);    Grid.SetRowSpan(TitleText, 2);
    Grid.SetColumn(TitleText, 0); Grid.SetColumnSpan(TitleText, 2);
    Grid.SetRow(TileImage, 0);    Grid.SetRowSpan(TileImage, 1);
    Grid.SetColumn(TileImage, 0); Grid.SetColumnSpan(TileImage, 1);

    if (!hasImage) return;

    Grid.SetRowSpan(TitleText, 1);
    Grid.SetColumnSpan(TitleText, 1);

    switch (position)
    {
        case "top":
        case "bottom":
            LayoutGrid.RowDefinitions[1].Height   = GridLength.Auto;
            LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(0);
            Grid.SetColumnSpan(TileImage, 2);
            Grid.SetColumnSpan(TitleText, 2);
            bool imgTop = position == "top";
            Grid.SetRow(TileImage, imgTop ? 0 : 1);
            Grid.SetRow(TitleText, imgTop ? 1 : 0);
            Grid.SetColumn(TileImage, 0); Grid.SetColumn(TitleText, 0);
            break;

        case "left":
        case "right":
            LayoutGrid.RowDefinitions[1].Height   = new GridLength(0);
            LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(1, GridUnitType.Star);
            LayoutGrid.ColumnDefinitions[0].Width  = new GridLength(1, GridUnitType.Star);
            Grid.SetRowSpan(TileImage, 2);
            Grid.SetRowSpan(TitleText, 2);
            bool imgLeft = position == "left";
            Grid.SetColumn(TileImage, imgLeft ? 0 : 1);
            Grid.SetColumn(TitleText, imgLeft ? 1 : 0);
            Grid.SetRow(TileImage, 0); Grid.SetRow(TitleText, 0);
            break;
    }
}

// ─── GIF タイマー ──────────────────────────────────────────────────────
private void OnLoaded(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrEmpty(_imagePath)) return;
    string ext = System.IO.Path.GetExtension(_imagePath).ToLowerInvariant();

    if (ext == ".gif")
    {
        StartGif(_imagePath);
    }
    else if (!string.IsNullOrEmpty(_imagePath))
    {
        try
        {
            TileImage.Source = new BitmapImage(new Uri(_imagePath, UriKind.Absolute));
        }
        catch { TileImage.Source = null; }
    }
}

private void OnUnloaded(object sender, RoutedEventArgs e) => StopGif();

private void StartGif(string path)
{
    try
    {
        _gifDecoder = new GifBitmapDecoder(
            new Uri(path, UriKind.Absolute),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        if (_gifDecoder.Frames.Count == 0) return;
        _gifFrameIndex = 0;
        TileImage.Source = _gifDecoder.Frames[0];

        if (_gifDecoder.Frames.Count > 1)
        {
            _gifTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            _gifTimer.Tick += (_, _) =>
            {
                if (_gifDecoder == null) return;
                _gifFrameIndex = (_gifFrameIndex + 1) % _gifDecoder.Frames.Count;
                TileImage.Source = _gifDecoder.Frames[_gifFrameIndex];
            };
            _gifTimer.Start();
        }
    }
    catch { /* パス不正等は無視 */ }
}

private void StopGif()
{
    _gifTimer?.Stop();
    _gifTimer    = null;
    _gifDecoder  = null;
}

// ─── D&D ファイルドロップ（通常モード・D&D 専用タイルのみ）───────────────
private void OnFileDrop(object sender, DragEventArgs e)
{
    if (_tile == null) return;
    if (App.LauncherViewModel?.Mode != AppMode.Normal) return;
    if (!_tile.Args.Contains("{drop}")) return;
    if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
    var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
    TileLaunchService.LaunchWithDrop(_tile, paths);
}
```

#### using 追加

```csharp
using System.Windows.Media.Imaging;
using System.Windows.Threading;
```

---

### 6. Views/Controls/SystemTileControl.xaml（新規）

```xml
<UserControl x:Class="AppLauncher.Views.Controls.SystemTileControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Loaded="OnLoaded" Unloaded="OnUnloaded">
    <Grid>
        <!-- テキスト表示 -->
        <StackPanel x:Name="TextPanel"
                    VerticalAlignment="Center" HorizontalAlignment="Center"
                    Margin="8">
            <TextBlock x:Name="MainText"
                       FontWeight="SemiBold"
                       TextAlignment="Center" TextWrapping="Wrap"/>
            <TextBlock x:Name="SubText"
                       Opacity="0.75"
                       TextAlignment="Center" TextWrapping="Wrap" Margin="0,2,0,0"/>
        </StackPanel>

        <!-- 円形グラフ表示 -->
        <Canvas x:Name="CirclePanel" Visibility="Collapsed"
                HorizontalAlignment="Center" VerticalAlignment="Center"
                Width="80" Height="80">
            <!-- 背景リング -->
            <Ellipse Canvas.Left="10" Canvas.Top="10"
                     Width="60" Height="60"
                     StrokeThickness="8" Fill="Transparent"
                     Stroke="#30F0F0F0"/>
            <!-- 値アーク（-90 度回転で 12 時から開始） -->
            <Ellipse x:Name="CircleArc"
                     Canvas.Left="10" Canvas.Top="10"
                     Width="60" Height="60"
                     StrokeThickness="8" Fill="Transparent"
                     StrokeStartLineCap="Round" StrokeEndLineCap="Round"
                     RenderTransformOrigin="0.5,0.5">
                <Ellipse.RenderTransform>
                    <RotateTransform Angle="-90"/>
                </Ellipse.RenderTransform>
            </Ellipse>
            <!-- 中央テキスト（使用率 %） -->
            <TextBlock x:Name="CircleCenterText"
                       Canvas.Left="0" Canvas.Top="28"
                       Width="80" TextAlignment="Center"
                       FontSize="14" FontWeight="SemiBold"/>
            <!-- 下部ラベル（デバイス名等） -->
            <TextBlock x:Name="CircleLabel"
                       Canvas.Left="0" Canvas.Top="62"
                       Width="80" TextAlignment="Center"
                       FontSize="10" Opacity="0.8"/>
        </Canvas>

        <!-- 編集モードオーバーレイ（TileControl と同構造） -->
        <Grid x:Name="EditOverlay" Visibility="Collapsed">
            <Border x:Name="EditButton"
                    Width="22" Height="22"
                    HorizontalAlignment="Left" VerticalAlignment="Top"
                    Margin="4" CornerRadius="4" Cursor="Hand"
                    Background="#CC333333">
                <TextBlock Text="✎" FontSize="11" Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <Border x:Name="DeleteButton"
                    Width="22" Height="22"
                    HorizontalAlignment="Right" VerticalAlignment="Top"
                    Margin="4" CornerRadius="4" Cursor="Hand"
                    Background="#CCE05252">
                <TextBlock Text="✕" FontSize="11" Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <Border x:Name="ResizeHandle"
                    Width="16" Height="16"
                    HorizontalAlignment="Right" VerticalAlignment="Bottom"
                    Margin="4" CornerRadius="3" Cursor="SizeNWSE"
                    Background="#CCF0F0F0"/>
        </Grid>
    </Grid>
</UserControl>
```

---

### 7. Views/Controls/SystemTileControl.xaml.cs（新規）

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AppLauncher.Models;
using AppLauncher.Models.Config;
using AppLauncher.Services;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class SystemTileControl : UserControl
{
    private TileViewModel?      _tile;
    private SystemInfoConfig?   _si;
    private DispatcherTimer?    _timer;

    public event Action<TileViewModel>? EditRequested;
    public event Action<TileViewModel>? DeleteRequested;
    public event Action<TileViewModel, MouseButtonEventArgs>? ResizeStarted;

    public SystemTileControl()
    {
        InitializeComponent();
        EditButton.MouseLeftButtonUp     += (_, e) => { e.Handled = true; if (_tile != null) EditRequested?.Invoke(_tile); };
        DeleteButton.MouseLeftButtonUp   += (_, e) => { e.Handled = true; if (_tile != null) DeleteRequested?.Invoke(_tile); };
        ResizeHandle.MouseLeftButtonDown += (_, e) => { if (_tile != null) { e.Handled = true; ResizeStarted?.Invoke(_tile, e); } };
    }

    public void Apply(TileViewModel tile, int cornerRadius, Color pageBackgroundColor)
    {
        _tile = tile;
        _si   = tile.SystemInfo;
        if (_si == null) return;

        // テキスト系スタイル設定
        var mainBrush = ColorPalette.GetBrush(tile.FontColor);
        MainText.Foreground        = mainBrush;
        SubText.Foreground         = mainBrush;
        CircleCenterText.Foreground = mainBrush;
        CircleLabel.Foreground     = mainBrush;
        MainText.FontSize          = tile.FontSizePt * 4.0 / 3.0;
        SubText.FontSize           = Math.Max(10, tile.FontSizePt * 4.0 / 3.0 - 4);
        CircleArc.Stroke           = ColorPalette.GetBrush(_si.MainColor);
    }

    // ─── ライフサイクル ────────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FetchAndUpdate();
        StartTimer();
        SubscribeMode();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        UnsubscribeMode();
    }

    private void StartTimer()
    {
        if (_si == null) return;
        int interval = Math.Clamp(_si.UpdateIntervalMs, 100, 3_600_000);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(interval) };
        _timer.Tick += (_, _) => FetchAndUpdate();
        _timer.Start();
    }

    // ─── 編集モードオーバーレイ ────────────────────────────────────────────
    private bool _modeSubscribed;

    private void SubscribeMode()
    {
        if (_modeSubscribed || App.LauncherViewModel is not { } vm) return;
        vm.PropertyChanged += OnVmPropertyChanged;
        _modeSubscribed = true;
    }

    private void UnsubscribeMode()
    {
        if (!_modeSubscribed || App.LauncherViewModel is not { } vm) return;
        vm.PropertyChanged -= OnVmPropertyChanged;
        _modeSubscribed = false;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.Mode))
            UpdateEditOverlay();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateEditOverlay();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        EditOverlay.Visibility = Visibility.Collapsed;
    }

    private void UpdateEditOverlay()
    {
        bool show = IsMouseOver && App.LauncherViewModel?.Mode == AppMode.Edit;
        EditOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── クリック（system タイルはクリック動作「表示更新」のみ） ─────────────
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (App.LauncherViewModel?.Mode != AppMode.Normal) return;
        // clickAction == "refresh"
        FetchAndUpdate();
    }

    // ─── データ取得・描画 ──────────────────────────────────────────────────
    private void FetchAndUpdate()
    {
        if (_si == null || _tile == null) return;
        var data = SystemInfoService.Instance.GetData(_si);
        Render(data);
    }

    private void Render(SystemData data)
    {
        if (_si == null || _tile == null) return;
        bool isCircle = _si.DisplayFormat == "circle" &&
                        _si.Category is "storage" or "usage";

        // アクセント・背景アクセント適用
        var mainBrush   = ColorPalette.GetBrush(
            data.ThresholdExceeded ? _si.AccentColor : _si.MainColor);
        var bgColor     = data.ThresholdExceeded
            ? ColorPalette.GetColor(_si.BackgroundAccent)
            : ColorPalette.GetColor(_tile.Color);
        double bgAlpha  = ColorPalette.OpacityToDouble(_tile.Opacity);
        var border = (Border)((Grid)Parent!).Children[0]; // TileBorder を取得できない場合は別途対応
        // ※ TileBorder への参照は TileGridControl.Rebuild() で Apply() 呼び出し時に渡す設計とする
        // （下記「8. TileGridControl の変更」参照）

        CircleArc.Stroke = mainBrush;

        if (isCircle)
        {
            TextPanel.Visibility   = Visibility.Collapsed;
            CirclePanel.Visibility = Visibility.Visible;

            // 円弧の長さを使用率から計算
            // StrokeThickness=8、半径=26（Ellipse Width=60 の中心線）
            const double strokeT = 8.0;
            const double radius  = 26.0; // (60 / 2) - (strokeT / 2)
            double circumference = 2 * Math.PI * radius / strokeT;
            double used          = Math.Clamp(data.Percentage / 100.0, 0, 1) * circumference;
            double unused        = circumference - used;
            CircleArc.StrokeDashArray = new DoubleCollection([used, unused]);
            CircleArc.Stroke          = mainBrush;

            CircleCenterText.Text     = $"{data.Percentage:F0}%";
            CircleCenterText.Foreground = mainBrush;
            CircleLabel.Text          = data.SubText;
        }
        else
        {
            TextPanel.Visibility   = Visibility.Visible;
            CirclePanel.Visibility = Visibility.Collapsed;
            MainText.Text          = data.MainText;
            SubText.Text           = data.SubText;
            MainText.Foreground    = mainBrush;
        }
    }
}
```

> **注意**：`Render()` 内でタイル背景色をアクセント色に切り替える処理は、`TileGridControl` 側で `SystemTileControl` に `Border` 参照を渡す、または `TileGridControl` がイベントを購読する形で実装する。本仕様では `TileGridControl.Rebuild()` 内で `Apply()` に `Border` を渡し、`SystemTileControl` が背景を直接操作する方式を採用する（後述 § 10 参照）。

---

### 8. Views/Controls/WebViewTileControl.xaml（新規）

```xml
<UserControl x:Class="AppLauncher.Views.Controls.WebViewTileControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf">
    <Grid>
        <wv2:WebView2 x:Name="WebView" IsHitTestVisible="True"/>

        <!-- 編集モードオーバーレイ -->
        <Grid x:Name="EditOverlay" Visibility="Collapsed">
            <Border x:Name="EditButton"
                    Width="22" Height="22"
                    HorizontalAlignment="Left" VerticalAlignment="Top"
                    Margin="4" CornerRadius="4" Cursor="Hand"
                    Background="#CC333333">
                <TextBlock Text="✎" FontSize="11" Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <Border x:Name="DeleteButton"
                    Width="22" Height="22"
                    HorizontalAlignment="Right" VerticalAlignment="Top"
                    Margin="4" CornerRadius="4" Cursor="Hand"
                    Background="#CCE05252">
                <TextBlock Text="✕" FontSize="11" Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
            <Border x:Name="ResizeHandle"
                    Width="16" Height="16"
                    HorizontalAlignment="Right" VerticalAlignment="Bottom"
                    Margin="4" CornerRadius="3" Cursor="SizeNWSE"
                    Background="#CCF0F0F0"/>
        </Grid>
    </Grid>
</UserControl>
```

---

### 9. Views/Controls/WebViewTileControl.xaml.cs（新規）

#### キャッシュ設計

ページを切り替えても WebView2 のロード状態を維持するため、`(PageViewModel, col, row)` をキーとする static キャッシュを `TileGridControl` 側に持つ（§ 10 参照）。`WebViewTileControl` 自体はキャッシュを意識しない。

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppLauncher.Models;
using AppLauncher.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace AppLauncher.Views.Controls;

public partial class WebViewTileControl : UserControl
{
    private TileViewModel? _tile;
    private bool _navigated;

    public event Action<TileViewModel>? EditRequested;
    public event Action<TileViewModel>? DeleteRequested;
    public event Action<TileViewModel, MouseButtonEventArgs>? ResizeStarted;

    public WebViewTileControl()
    {
        InitializeComponent();
        EditButton.MouseLeftButtonUp     += (_, e) => { e.Handled = true; if (_tile != null) EditRequested?.Invoke(_tile); };
        DeleteButton.MouseLeftButtonUp   += (_, e) => { e.Handled = true; if (_tile != null) DeleteRequested?.Invoke(_tile); };
        ResizeHandle.MouseLeftButtonDown += (_, e) => { if (_tile != null) { e.Handled = true; ResizeStarted?.Invoke(_tile, e); } };
        WebView.CoreWebView2InitializationCompleted += OnCoreWebView2Ready;
        _ = WebView.EnsureCoreWebView2Async();
    }

    public void Apply(TileViewModel tile)
    {
        _tile = tile;
        if (WebView.CoreWebView2 != null && !_navigated)
            Navigate(tile.Path);
    }

    private void OnCoreWebView2Ready(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (_tile != null && !_navigated)
            Navigate(_tile.Path);
    }

    private void Navigate(string path)
    {
        if (string.IsNullOrEmpty(path) || WebView.CoreWebView2 == null) return;
        _navigated = true;
        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            WebView.CoreWebView2.Navigate(path);
        else
            WebView.CoreWebView2.Navigate(new Uri(path, UriKind.Absolute).ToString());
    }

    // ─── 編集モードオーバーレイ ────────────────────────────────────────────
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (App.LauncherViewModel?.Mode == AppMode.Edit)
            EditOverlay.Visibility = Visibility.Visible;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        EditOverlay.Visibility = Visibility.Collapsed;
    }
}
```

---

### 10. Views/Controls/TileGridControl.xaml.cs の変更

#### 変更概要

1. `Rebuild()` でタイル種別に応じて Control を分岐生成
2. WebView2 キャッシュ（static Dictionary）を追加
3. 編集モードでの外部ファイルドロップによるタイル自動作成

#### 追加フィールド

```csharp
// webview タイルはページ切替でも破棄しないためキャッシュする
// キー：(ページ, col, row)
private static readonly Dictionary<(PageViewModel, int, int), WebViewTileControl> _webviewCache = [];
```

#### Rebuild() 内のタイル配置部分の変更

既存の「タイル配置」コメント以降のループを以下に置き換える。

```csharp
// タイル配置（タイプに応じてコントロールを分岐）
foreach (var tile in page.Tiles)
{
    UserControl control = tile.Type switch
    {
        "system"  => CreateSystemControl(tile, cornerRadius, bgColor),
        "webview" => GetOrCreateWebViewControl(page, tile),
        _         => CreateTileControl(tile, cornerRadius, bgColor),
    };

    Grid.SetColumn(control, tile.Col * 2);
    Grid.SetRow(control, tile.Row * 2);
    Grid.SetColumnSpan(control, tile.ColSpan * 2 - 1);
    Grid.SetRowSpan(control, tile.RowSpan * 2 - 1);
    TileGrid.Children.Add(control);
}
```

#### 新規ヘルパーメソッド

```csharp
private static TileControl CreateTileControl(TileViewModel tile, int cornerRadius, Color bgColor)
{
    var ctrl = new TileControl();
    ctrl.Apply(tile, cornerRadius, bgColor);
    ctrl.EditRequested   += t => App.LauncherViewModel?.OpenTileEditCommand.Execute(t);
    ctrl.DeleteRequested += t => App.LauncherViewModel?.RequestDeleteTileCommand.Execute(t);
    ctrl.ResizeStarted   += OnTileResizeStarted_Static; // 既存の OnTileResizeStarted の参照渡し
    return ctrl;
}

private SystemTileControl CreateSystemControl(TileViewModel tile, int cornerRadius, Color bgColor)
{
    var ctrl = new SystemTileControl();
    ctrl.Apply(tile, cornerRadius, bgColor);
    ctrl.EditRequested   += t => App.LauncherViewModel?.OpenTileEditCommand.Execute(t);
    ctrl.DeleteRequested += t => App.LauncherViewModel?.RequestDeleteTileCommand.Execute(t);
    ctrl.ResizeStarted   += (t, e) => OnTileResizeStarted(t, e);
    return ctrl;
}

private static WebViewTileControl GetOrCreateWebViewControl(PageViewModel page, TileViewModel tile)
{
    var key = (page, tile.Col, tile.Row);
    if (!_webviewCache.TryGetValue(key, out var ctrl))
    {
        ctrl = new WebViewTileControl();
        _webviewCache[key] = ctrl;
    }
    ctrl.Apply(tile);
    ctrl.EditRequested   += t => App.LauncherViewModel?.OpenTileEditCommand.Execute(t);
    ctrl.DeleteRequested += t => App.LauncherViewModel?.RequestDeleteTileCommand.Execute(t);
    ctrl.ResizeStarted   += (t, e) => { /* TODO: ResizeStarted 配線 */ };
    return ctrl;
}
```

> **注意**：既存の `OnTileResizeStarted` はインスタンスメソッドのため、ヘルパー内からは `this` 経由で参照する。`CreateTileControl` をインスタンスメソッドに変更するか、ラムダで `(t, e) => OnTileResizeStarted(t, e)` と渡すこと。

#### 編集モードでのファイルドロップ（外部 → 空スロット）

`TileGrid` の `Drop` イベントハンドラ `OnTileGridDrop()` の末尾に以下を追加する。

```csharp
// ─── 編集モードでの外部ファイルドロップによるタイル作成 ─────────────────
// （D&D データが TileViewModel でなく FileDrop の場合）
private void OnTileGridFileDrop(object sender, DragEventArgs e)
{
    if (DataContext is not PageViewModel page) return;
    if (App.LauncherViewModel?.Mode != AppMode.Edit) return;
    if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

    var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
    if (paths.Length == 0) return;

    var pos        = e.GetPosition(TileGrid);
    var (col, row) = PositionToCell(pos);
    if (!CanPlace(col, row, 1, 1)) return;

    var tile = CreateTileFromFile(paths[0], col, row);
    page.Tiles.Add(tile);
}

private static TileViewModel CreateTileFromFile(string path, int col, int row)
{
    string ext  = System.IO.Path.GetExtension(path).ToLowerInvariant();
    string title = System.IO.Path.GetFileNameWithoutExtension(path);

    // .lnk ショートカット解決（WScript.Shell 経由）
    if (ext == ".lnk")
    {
        try
        {
            dynamic shell    = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic shortcut = shell.CreateShortcut(path);
            string target    = (string)shortcut.TargetPath;
            if (!string.IsNullOrEmpty(target)) path = target;
        }
        catch { /* 解決失敗時はそのまま使用 */ }
    }

    // 画像ファイルは imagePath へ設定
    bool isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".ico" or ".bmp";

    return new TileViewModel(new Models.Config.TileConfig
    {
        Col           = col,      Row      = row,
        ColSpan       = 1,        RowSpan  = 1,
        Type          = "app",
        Title         = title,
        Path          = isImage ? "" : path,
        Color         = "blue",   Opacity  = 20,
        FontSizePt    = 16,       FontColor = "white",
        ImagePath     = isImage ? path : "",
        ImagePosition = "top",
    });
}
```

コンストラクタの `TileGrid.Drop += OnTileGridDrop;` を以下に変更する（FileDrop も同ハンドラで判定する）。

```csharp
// 既存
TileGrid.Drop += OnTileGridDrop;

// 追加
TileGrid.Drop += OnTileGridFileDrop;
```

`TileGrid` に `AllowDrop="True"` が設定されていることを確認する（既存の D&D 実装で設定済みのはず）。設定がなければ `TileGridControl.xaml` の `<Grid x:Name="TileGrid">` に `AllowDrop="True"` を追加する。

---

### 11. SystemTileControl の背景アクセント切替（補足）

§ 7 の `Render()` 内でタイル背景色をアクセント色に変えるには、`SystemTileControl` に `TileBorder`（`Border`）への参照を渡す必要がある。

`Apply()` のシグネチャを以下に変更する。

```csharp
// border: TileGridControl.Rebuild() 内で生成した外枠 Border（角丸・背景設定済み）
public void Apply(TileViewModel tile, int cornerRadius, Color pageBackgroundColor, Border tileBorder)
{
    _tileBorder = tileBorder;  // フィールドに保持
    // 既存の Apply 処理
}
```

`Render()` 内の背景切替：

```csharp
if (_tileBorder != null)
{
    var bgColor  = data.ThresholdExceeded
        ? ColorPalette.GetColor(_si.BackgroundAccent)
        : ColorPalette.GetColor(_tile.Color);
    double alpha = ColorPalette.OpacityToDouble(_tile.Opacity);
    _tileBorder.Background = new SolidColorBrush(
        Color.FromArgb((byte)(255 * alpha), bgColor.R, bgColor.G, bgColor.B));
}
```

`TileGridControl.CreateSystemControl()` で `Border` を生成して渡す。

```csharp
private SystemTileControl CreateSystemControl(TileViewModel tile, int cornerRadius, Color bgColor)
{
    // タイルと同じ背景を持つ外枠 Border を生成
    var tileColor = ColorPalette.GetColor(tile.Color);
    double alpha  = ColorPalette.OpacityToDouble(tile.Opacity);
    var border = new Border
    {
        CornerRadius = new CornerRadius(cornerRadius),
        Background   = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), tileColor.R, tileColor.G, tileColor.B)),
    };

    var ctrl = new SystemTileControl();
    border.Child = ctrl;   // Border の子として配置

    ctrl.Apply(tile, cornerRadius, bgColor, border);
    ctrl.EditRequested   += t => App.LauncherViewModel?.OpenTileEditCommand.Execute(t);
    ctrl.DeleteRequested += t => App.LauncherViewModel?.RequestDeleteTileCommand.Execute(t);
    ctrl.ResizeStarted   += (t, e) => OnTileResizeStarted(t, e);

    // TileGrid には border を追加する（TileGrid.Children.Add(border)）
    // Rebuild() 内でこのメソッドの戻り値を border に変更すること
    return ctrl; // ← border を返す設計変更が必要（下記注意参照）
}
```

> **実装注意**：`TileGrid.Children.Add()` に渡す要素を `SystemTileControl` から `Border（SystemTileControl を内包）` に変更する必要がある。Rebuild() 内の `UserControl control = tile.Type switch { ... }` を `FrameworkElement control = tile.Type switch { ... }` に変更し、`CreateSystemControl()` が `Border` を返すように修正すること。`WebViewTileControl` と `TileControl` はそのまま `UserControl` を返す。

---

## 完了条件

- [ ] D&D 専用タイル（引数に `{drop}` を含む `app` タイル）にファイルをドロップするとアプリが起動し、ドロップしたファイルパスが引数に渡される
- [ ] 静的画像（PNG / JPG / ICO）が `ImagePosition` に従ってタイル内に表示される
- [ ] GIF 画像がループ再生される。非表示ページへ切り替えると再生が停止し、ページ表示再開時に再開する
- [ ] `system` タイル（OS 情報・ストレージ・CPU 使用率・メモリ使用率）がデータを取得して表示する
- [ ] `system` タイルの自動更新タイマーが `UpdateIntervalMs` 間隔で動作し、非表示ページでは停止する
- [ ] 使用率が `Threshold` を超えると `AccentColor` でグラフ / テキストが描画され、タイル背景が `BackgroundAccent` に切り替わる
- [ ] `system` タイルの表示形式「円形グラフ」を選択すると円形グラフが描画される
- [ ] `webview` タイルが指定 URL のページを表示する。ページを切り替えても再ロードされない
- [ ] 編集モードで外部ファイル（`.exe` / `.lnk` / 画像等）を空スロットへドロップすると自動設定でタイルが作成される
- [ ] `dotnet build` がエラーなく成功する

---

## 参照定義書

- `definition/function/アプリケーション機能定義.md § 3.3` — タイル D&D 操作・ファイルドロップ仕様
- `definition/function/アプリケーション機能定義.md § 3.4` — タイル種別（webview / system）のクリック動作
- `definition/function/アプリケーション機能定義.md § 3.5` — システム情報タイルの全設定項目・カテゴリ別表示仕様
