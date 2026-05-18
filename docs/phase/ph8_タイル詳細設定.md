# Ph.8 実装指示書 ─ タイル詳細設定

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.8 タイル詳細設定 |
| 実装目的 | タイル編集ボタン（✎）押下後に表示されるタイル詳細設定パネルの実装 |
| 未確定事項 | なし |
| 既存影響 | `LauncherViewModel` に `EditingTileVm` プロパティ・`ConfirmTileEditCommand` を追加、`CloseTileEditCommand` を修正。`LauncherWindow` に `TileEditControl` を追加してモード切替で表示/非表示を制御 |
| 追加ライブラリ | なし |
| 実装範囲 | タイル詳細設定パネルUI（基本・起動・システム情報・見た目の各セクション）。OK 時に変更をタイルへ反映し config.json へ保存。キャンセル時は変更を破棄。編集中の変更はプレビューエリアへ即時反映 |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `Helpers/ColorHelper.cs` | 新規：HSL 色空間変換・UI 配色計算ユーティリティ |
| `ViewModels/TileEditViewModel.cs` | 新規：タイル編集中の一時状態を保持する VM（変更は OK 時のみ実際のタイルへ反映） |
| `ViewModels/LauncherViewModel.cs` | 変更：`EditingTileVm` プロパティ追加・`OpenTileEditCommand` 更新・`ConfirmTileEditCommand` 追加・`CloseTileEditCommand` 修正・`SyncPagesToConfig` 追加 |
| `Views/Controls/TileEditControl.xaml` | 新規：タイル詳細設定パネルの UI（スクロール可能・セクション折りたたみ） |
| `Views/Controls/TileEditControl.xaml.cs` | 新規：コードビハインド（コンボボックス初期化・セクション折りたたみ・ファイル参照ダイアログ） |
| `Views/LauncherWindow.xaml` | 変更：Frame 内に TileEditControl を追加 |
| `Views/LauncherWindow.xaml.cs` | 変更：Mode 変更時の TileEditControl の表示/非表示と配色初期化 |

---

## 実装仕様

### 1. Helpers/ColorHelper.cs（新規）

ページ背景色から UI 要素配色を計算する。計算ルールは `definition/ui/ランチャー本体.md § 8` 参照（L±15pt・S-5pt 調整）。

```csharp
using System.Windows.Media;

namespace AppLauncher.Helpers;

public static class ColorHelper
{
    /// <summary>
    /// ページ背景色をもとに UI 要素色（セクション区切り・ボタン等）を計算する。
    /// HSL 変換 → L±15pt 調整 → S-5pt 調整 → RGB 変換。
    /// </summary>
    public static Color ComputeUiElementColor(Color baseColor)
    {
        RgbToHsl(baseColor, out double h, out double s, out double l);
        l = l >= 0.5 ? Math.Max(0.0, l - 0.15) : Math.Min(1.0, l + 0.15);
        s = Math.Max(0.0, s - 0.05);
        return HslToRgb(h, s, l);
    }

    private static void RgbToHsl(Color c, out double h, out double s, out double l)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;
        if (max == min) { h = s = 0; return; }
        double d = max - min;
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        if      (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else               h = (r - g) / d + 4;
        h /= 6;
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        if (s == 0) { byte v = (byte)(l * 255); return Color.FromRgb(v, v, v); }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        return Color.FromRgb(
            (byte)(HueToRgb(p, q, h + 1.0 / 3) * 255),
            (byte)(HueToRgb(p, q, h)            * 255),
            (byte)(HueToRgb(p, q, h - 1.0 / 3) * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1; if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
```

---

### 2. ViewModels/TileEditViewModel.cs（新規）

`TileViewModel` からコピーして生成する一時 VM。OK 時は `ToConfig()` → `new TileViewModel(config)` でタイルを差し替える。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using AppLauncher.Models;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class TileEditViewModel : ObservableObject
{
    // ─── 基本 ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApp))]
    [NotifyPropertyChangedFor(nameof(IsSystem))]
    [NotifyPropertyChangedFor(nameof(HasPath))]
    [NotifyPropertyChangedFor(nameof(HasArgs))]
    [NotifyPropertyChangedFor(nameof(HasWorkDir))]
    [NotifyPropertyChangedFor(nameof(HasSystemInfo))]
    private string _type;

    // ─── 起動 ─────────────────────────────────────────────────────────────
    [ObservableProperty] private string _path;
    [ObservableProperty] private string _args;
    [ObservableProperty] private string _workDir;

    // ─── 見た目 ───────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBackground))]
    private string _color;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBackground))]
    private int _opacity;

    [ObservableProperty] private string _fontName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewFontSize))]
    private int _fontSizePt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewForeground))]
    private string _fontColor;

    [ObservableProperty] private string _imagePath;
    [ObservableProperty] private string _imagePosition;
    [ObservableProperty] private bool   _imageTransparent;

    // ─── システム情報 ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SiHasDeviceType))]
    [NotifyPropertyChangedFor(nameof(SiHasAccent))]
    [NotifyPropertyChangedFor(nameof(SiHasCircleFormat))]
    private string _siCategory;

    [ObservableProperty] private string _siDeviceType;
    [ObservableProperty] private string _siTarget;
    [ObservableProperty] private string _siDisplayFormat;
    [ObservableProperty] private string _siMainColor;
    [ObservableProperty] private string _siAccentColor;
    [ObservableProperty] private string _siBackgroundAccent;
    [ObservableProperty] private int    _siThreshold;
    [ObservableProperty] private string _siClickAction;
    [ObservableProperty] private int    _siUpdateIntervalMs;

    // ─── 配置情報（編集対象外） ────────────────────────────────────────────
    public int Col     { get; }
    public int Row     { get; }
    public int ColSpan { get; }
    public int RowSpan { get; }

    // ─── 条件プロパティ ───────────────────────────────────────────────────
    public bool IsApp         => Type == "app";
    public bool IsSystem      => Type == "system";
    public bool HasPath       => Type != "system";
    public bool HasArgs       => Type == "app";
    public bool HasWorkDir    => Type == "app";
    public bool HasSystemInfo => Type == "system";
    public bool SiHasDeviceType  => SiCategory == "usage";
    public bool SiHasAccent      => SiCategory is "storage" or "usage";
    public bool SiHasCircleFormat => SiCategory is "storage" or "usage";

    // ─── プレビュー用計算プロパティ ────────────────────────────────────────
    public SolidColorBrush PreviewBackground
    {
        get
        {
            var c = ColorPalette.GetColor(Color);
            double a = ColorPalette.OpacityToDouble(Opacity);
            return new SolidColorBrush(
                System.Windows.Media.Color.FromArgb((byte)(255 * a), c.R, c.G, c.B));
        }
    }

    public SolidColorBrush PreviewForeground => ColorPalette.GetBrush(FontColor);
    public double PreviewFontSize => FontSizePt * 4.0 / 3.0;

    public TileEditViewModel(TileViewModel source)
    {
        Col     = source.Col;
        Row     = source.Row;
        ColSpan = source.ColSpan;
        RowSpan = source.RowSpan;

        _title   = source.Title;
        _type    = source.Type;
        _path    = source.Path;
        _args    = source.Args;
        _workDir = source.WorkDir;
        _color   = source.Color;
        _opacity = source.Opacity;
        _fontName    = source.FontName;
        _fontSizePt  = source.FontSizePt;
        _fontColor   = source.FontColor;
        _imagePath   = source.ImagePath;
        _imagePosition    = source.ImagePosition;
        _imageTransparent = source.ImageTransparent;

        var si = source.SystemInfo;
        _siCategory         = si?.Category         ?? "usage";
        _siDeviceType       = si?.DeviceType       ?? "cpu";
        _siTarget           = si?.Target           ?? "";
        _siDisplayFormat    = si?.DisplayFormat    ?? "text";
        _siMainColor        = si?.MainColor        ?? "white";
        _siAccentColor      = si?.AccentColor      ?? "orange";
        _siBackgroundAccent = si?.BackgroundAccent ?? "red";
        _siThreshold        = si?.Threshold        ?? 80;
        _siClickAction      = si?.ClickAction      ?? "refresh";
        _siUpdateIntervalMs = si?.UpdateIntervalMs ?? 60000;
    }

    public TileConfig ToConfig() => new()
    {
        Col     = Col,     Row     = Row,
        ColSpan = ColSpan, RowSpan = RowSpan,
        Type    = Type,    Title   = Title,
        Path    = Path,    Args    = Args,  WorkDir = WorkDir,
        Color   = Color,   Opacity = Opacity,
        FontName         = FontName,    FontSizePt  = FontSizePt,
        FontColor        = FontColor,   ImagePath   = ImagePath,
        ImagePosition    = ImagePosition,
        ImageTransparent = ImageTransparent,
        SystemInfo = Type == "system" ? new SystemInfoConfig
        {
            Category         = SiCategory,
            DeviceType       = SiDeviceType,
            Target           = SiTarget,
            DisplayFormat    = SiDisplayFormat,
            MainColor        = SiMainColor,
            AccentColor      = SiAccentColor,
            BackgroundAccent = SiBackgroundAccent,
            Threshold        = SiThreshold,
            ClickAction      = SiClickAction,
            UpdateIntervalMs = SiUpdateIntervalMs,
        } : null,
    };
}
```

---

### 3. ViewModels/LauncherViewModel.cs の変更

以下を追加・変更する（他の実装は現状のまま）。

```csharp
// ─── 追加プロパティ ────────────────────────────────────────────────────────
[ObservableProperty]
private TileEditViewModel? _editingTileVm;

// ─── 変更：OpenTileEditCommand（EditingTileVm の生成を追加） ──────────────
[RelayCommand]
private void OpenTileEdit(TileViewModel tile)
{
    EditingTile   = tile;
    EditingTileVm = new TileEditViewModel(tile);
    Mode = AppMode.TileEdit;
}

// ─── 追加：ConfirmTileEditCommand（OK ボタン） ────────────────────────────
[RelayCommand]
private void ConfirmTileEdit()
{
    if (EditingTileVm == null || EditingTile == null) return;
    var newConfig = EditingTileVm.ToConfig();
    int idx = CurrentPage.Tiles.IndexOf(EditingTile);
    EditingTile   = null;
    EditingTileVm = null;
    if (idx >= 0)
        CurrentPage.Tiles[idx] = new TileViewModel(newConfig);
    SyncPagesToConfig();
    App.ConfigService.Save();
    Mode = AppMode.Edit;
}

// ─── 変更：CloseTileEditCommand（キャンセル。EditingTileVm のクリアを追加） ─
[RelayCommand]
private void CloseTileEdit()
{
    EditingTile   = null;
    EditingTileVm = null;
    Mode = AppMode.Edit;
}

// ─── 追加：SyncPagesToConfig ──────────────────────────────────────────────
// 現在の ViewModel 状態を AppConfig.Pages へ反映する（Save 前に呼ぶ）
private void SyncPagesToConfig()
{
    App.ConfigService.Current.Pages = Pages.Select(p => new PageConfig
    {
        Name              = p.Name,
        BackgroundColor   = p.BackgroundColor,
        BackgroundOpacity = p.BackgroundOpacity,
        HandleColor       = p.HandleColor,
        HandleOpacity     = p.HandleOpacity,
        PinFrameColor     = p.PinFrameColor,
        PinFrameOpacity   = p.PinFrameOpacity,
        Tiles = p.Tiles.Select(t => t.ToConfig()).ToList(),
    }).ToList();
}
```

using に `AppLauncher.Models.Config` が既に含まれていることを確認すること。

---

### 4. Views/Controls/TileEditControl.xaml（新規）

Frame 内に重ねて表示し、通常コンテンツを覆う。DataContext は LauncherViewModel のまま（`LauncherWindow` から継承）。各設定項目は `{Binding EditingTileVm.プロパティ名}` でバインドする。

```xml
<UserControl x:Class="AppLauncher.Views.Controls.TileEditControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Loaded="OnLoaded">
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
    </UserControl.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/> <!-- ヘッダー：プレビュー + モード名 -->
            <RowDefinition Height="*"/>   <!-- スクロール可能セクション群 -->
            <RowDefinition Height="Auto"/> <!-- OK / キャンセルボタン -->
        </Grid.RowDefinitions>

        <!-- ─── ヘッダー ─────────────────────────────────────────────── -->
        <Grid Grid.Row="0" Margin="12,12,12,4">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- プレビュー（2×1 タイルサイズ相当：200×96） -->
            <Border Grid.Column="0" Width="200" Height="96" CornerRadius="8"
                    Background="{Binding EditingTileVm.PreviewBackground}">
                <TextBlock Text="{Binding EditingTileVm.Title}"
                           Foreground="{Binding EditingTileVm.PreviewForeground}"
                           FontSize="{Binding EditingTileVm.PreviewFontSize}"
                           HorizontalAlignment="Center" VerticalAlignment="Center"
                           TextWrapping="Wrap" TextAlignment="Center" Padding="8"/>
            </Border>

            <!-- モード名ラベル -->
            <TextBlock Grid.Column="1" Text="タイル編集"
                       Foreground="#F0F0F0" FontSize="15" FontWeight="SemiBold"
                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </Grid>

        <!-- ─── スクロール可能セクション群 ──────────────────────────── -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Margin="12,4,12,4">
            <StackPanel>

                <!-- ── 基本セクション ── -->
                <Border x:Name="BasicHeader" Cursor="Hand" Padding="0,8,0,6"
                        BorderThickness="0,0,0,1" Margin="0,8,0,0">
                    <TextBlock x:Name="BasicArrow" Text="▲ 基本"
                               Foreground="#F0F0F0" FontWeight="SemiBold"/>
                </Border>
                <StackPanel x:Name="BasicContent" Margin="0,6,0,0">
                    <!-- タイトル -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="タイトル" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1"
                                 Text="{Binding EditingTileVm.Title, UpdateSourceTrigger=PropertyChanged}"
                                 Background="#30FFFFFF" Foreground="#F0F0F0"
                                 BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                    </Grid>
                    <!-- 種類 -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="種類" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <ComboBox x:Name="TypeCombo" Grid.Column="1"
                                  SelectedValue="{Binding EditingTileVm.Type, UpdateSourceTrigger=PropertyChanged}"
                                  SelectedValuePath="Value"/>
                    </Grid>
                </StackPanel>

                <!-- ── 起動セクション（system 以外） ── -->
                <StackPanel Visibility="{Binding EditingTileVm.HasPath, Converter={StaticResource BoolToVis}}">
                    <Border x:Name="LaunchHeader" Cursor="Hand" Padding="0,8,0,6"
                            BorderThickness="0,0,0,1" Margin="0,8,0,0">
                        <TextBlock x:Name="LaunchArrow" Text="▲ 起動"
                                   Foreground="#F0F0F0" FontWeight="SemiBold"/>
                    </Border>
                    <StackPanel x:Name="LaunchContent" Margin="0,6,0,0">
                        <!-- パス / URL -->
                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="パス/URL" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <TextBox Grid.Column="1"
                                     Text="{Binding EditingTileVm.Path, UpdateSourceTrigger=PropertyChanged}"
                                     Background="#30FFFFFF" Foreground="#F0F0F0"
                                     BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                            <Border x:Name="BrowsePathButton" Grid.Column="2"
                                    Width="36" Height="22" Margin="4,0,0,0"
                                    CornerRadius="4" Cursor="Hand"
                                    Visibility="{Binding EditingTileVm.IsApp, Converter={StaticResource BoolToVis}}">
                                <TextBlock Text="参照" Foreground="White"
                                           HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="11"/>
                            </Border>
                        </Grid>
                        <!-- 引数（app のみ） -->
                        <Grid Margin="0,4"
                              Visibility="{Binding EditingTileVm.HasArgs, Converter={StaticResource BoolToVis}}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="引数" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <TextBox Grid.Column="1"
                                     Text="{Binding EditingTileVm.Args, UpdateSourceTrigger=PropertyChanged}"
                                     Background="#30FFFFFF" Foreground="#F0F0F0"
                                     BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                        </Grid>
                        <!-- 作業フォルダ（app のみ） -->
                        <Grid Margin="0,4"
                              Visibility="{Binding EditingTileVm.HasWorkDir, Converter={StaticResource BoolToVis}}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="作業フォルダ" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <TextBox Grid.Column="1"
                                     Text="{Binding EditingTileVm.WorkDir, UpdateSourceTrigger=PropertyChanged}"
                                     Background="#30FFFFFF" Foreground="#F0F0F0"
                                     BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                            <Border x:Name="BrowseWorkDirButton" Grid.Column="2"
                                    Width="36" Height="22" Margin="4,0,0,0"
                                    CornerRadius="4" Cursor="Hand">
                                <TextBlock Text="参照" Foreground="White"
                                           HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="11"/>
                            </Border>
                        </Grid>
                    </StackPanel>
                </StackPanel>

                <!-- ── システム情報セクション（system のみ） ── -->
                <StackPanel Visibility="{Binding EditingTileVm.HasSystemInfo, Converter={StaticResource BoolToVis}}">
                    <Border x:Name="SystemHeader" Cursor="Hand" Padding="0,8,0,6"
                            BorderThickness="0,0,0,1" Margin="0,8,0,0">
                        <TextBlock x:Name="SystemArrow" Text="▲ システム情報"
                                   Foreground="#F0F0F0" FontWeight="SemiBold"/>
                    </Border>
                    <StackPanel x:Name="SystemContent" Margin="0,6,0,0">
                        <!-- カテゴリ -->
                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="カテゴリ" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <ComboBox x:Name="SiCategoryCombo" Grid.Column="1"
                                      SelectedValue="{Binding EditingTileVm.SiCategory, UpdateSourceTrigger=PropertyChanged}"
                                      SelectedValuePath="Value"/>
                        </Grid>
                        <!-- デバイス種別（usage のみ） -->
                        <Grid Margin="0,4"
                              Visibility="{Binding EditingTileVm.SiHasDeviceType, Converter={StaticResource BoolToVis}}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="デバイス種別" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <ComboBox x:Name="SiDeviceTypeCombo" Grid.Column="1"
                                      SelectedValue="{Binding EditingTileVm.SiDeviceType, UpdateSourceTrigger=PropertyChanged}"
                                      SelectedValuePath="Value"/>
                        </Grid>
                        <!-- 対象 -->
                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="対象" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <TextBox Grid.Column="1"
                                     Text="{Binding EditingTileVm.SiTarget, UpdateSourceTrigger=PropertyChanged}"
                                     Background="#30FFFFFF" Foreground="#F0F0F0"
                                     BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                        </Grid>
                        <!-- 表示形式（storage / usage のみ） -->
                        <Grid Margin="0,4"
                              Visibility="{Binding EditingTileVm.SiHasCircleFormat, Converter={StaticResource BoolToVis}}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="表示形式" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <ComboBox x:Name="SiDisplayFormatCombo" Grid.Column="1"
                                      SelectedValue="{Binding EditingTileVm.SiDisplayFormat, UpdateSourceTrigger=PropertyChanged}"
                                      SelectedValuePath="Value"/>
                        </Grid>
                        <!-- メインカラー -->
                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="メインカラー" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <ComboBox x:Name="SiMainColorCombo" Grid.Column="1"
                                      SelectedValue="{Binding EditingTileVm.SiMainColor, UpdateSourceTrigger=PropertyChanged}"
                                      SelectedValuePath="Name">
                                <ComboBox.ItemTemplate>
                                    <DataTemplate>
                                        <StackPanel Orientation="Horizontal">
                                            <Border Width="14" Height="14" Margin="0,0,6,0" CornerRadius="2" Background="{Binding Brush}"/>
                                            <TextBlock Text="{Binding Name}" Foreground="#2D2D2D"/>
                                        </StackPanel>
                                    </DataTemplate>
                                </ComboBox.ItemTemplate>
                            </ComboBox>
                        </Grid>
                        <!-- アクセント（storage / usage のみ） -->
                        <Grid Margin="0,4"
                              Visibility="{Binding EditingTileVm.SiHasAccent, Converter={StaticResource BoolToVis}}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="アクセント" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <ComboBox x:Name="SiAccentColorCombo" Grid.Column="1"
                                      SelectedValue="{Binding EditingTileVm.SiAccentColor, UpdateSourceTrigger=PropertyChanged}"
                                      SelectedValuePath="Name">
                                <ComboBox.ItemTemplate>
                                    <DataTemplate>
                                        <StackPanel Orientation="Horizontal">
                                            <Border Width="14" Height="14" Margin="0,0,6,0" CornerRadius="2" Background="{Binding Brush}"/>
                                            <TextBlock Text="{Binding Name}" Foreground="#2D2D2D"/>
                                        </StackPanel>
                                    </DataTemplate>
                                </ComboBox.ItemTemplate>
                            </ComboBox>
                        </Grid>
                        <!-- 背景アクセント（storage / usage のみ） -->
                        <Grid Margin="0,4"
                              Visibility="{Binding EditingTileVm.SiHasAccent, Converter={StaticResource BoolToVis}}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="背景アクセント" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <ComboBox x:Name="SiBgAccentColorCombo" Grid.Column="1"
                                      SelectedValue="{Binding EditingTileVm.SiBackgroundAccent, UpdateSourceTrigger=PropertyChanged}"
                                      SelectedValuePath="Name">
                                <ComboBox.ItemTemplate>
                                    <DataTemplate>
                                        <StackPanel Orientation="Horizontal">
                                            <Border Width="14" Height="14" Margin="0,0,6,0" CornerRadius="2" Background="{Binding Brush}"/>
                                            <TextBlock Text="{Binding Name}" Foreground="#2D2D2D"/>
                                        </StackPanel>
                                    </DataTemplate>
                                </ComboBox.ItemTemplate>
                            </ComboBox>
                        </Grid>
                        <!-- 基準値（storage / usage のみ） -->
                        <Grid Margin="0,4"
                              Visibility="{Binding EditingTileVm.SiHasAccent, Converter={StaticResource BoolToVis}}">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="基準値" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <TextBox Grid.Column="1"
                                     Text="{Binding EditingTileVm.SiThreshold, UpdateSourceTrigger=PropertyChanged}"
                                     Background="#30FFFFFF" Foreground="#F0F0F0"
                                     BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                            <TextBlock Grid.Column="2" Text="%" Foreground="#F0F0F0"
                                       VerticalAlignment="Center" Margin="4,0,0,0"/>
                        </Grid>
                        <!-- 更新間隔 -->
                        <Grid Margin="0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="90"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="更新間隔" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                            <TextBox Grid.Column="1"
                                     Text="{Binding EditingTileVm.SiUpdateIntervalMs, UpdateSourceTrigger=PropertyChanged}"
                                     Background="#30FFFFFF" Foreground="#F0F0F0"
                                     BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                            <TextBlock Grid.Column="2" Text="ms" Foreground="#F0F0F0"
                                       VerticalAlignment="Center" Margin="4,0,0,0"/>
                        </Grid>
                    </StackPanel>
                </StackPanel>

                <!-- ── 見た目セクション ── -->
                <Border x:Name="AppearanceHeader" Cursor="Hand" Padding="0,8,0,6"
                        BorderThickness="0,0,0,1" Margin="0,8,0,0">
                    <TextBlock x:Name="AppearanceArrow" Text="▲ 見た目"
                               Foreground="#F0F0F0" FontWeight="SemiBold"/>
                </Border>
                <StackPanel x:Name="AppearanceContent" Margin="0,6,0,0">
                    <!-- カラー -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="カラー" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <ComboBox x:Name="TileColorCombo" Grid.Column="1"
                                  SelectedValue="{Binding EditingTileVm.Color, UpdateSourceTrigger=PropertyChanged}"
                                  SelectedValuePath="Name">
                            <ComboBox.ItemTemplate>
                                <DataTemplate>
                                    <StackPanel Orientation="Horizontal">
                                        <Border Width="14" Height="14" Margin="0,0,6,0" CornerRadius="2" Background="{Binding Brush}"/>
                                        <TextBlock Text="{Binding Name}" Foreground="#2D2D2D"/>
                                    </StackPanel>
                                </DataTemplate>
                            </ComboBox.ItemTemplate>
                        </ComboBox>
                    </Grid>
                    <!-- 透過率 -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="44"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="透過率" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <Slider Grid.Column="1"
                                Value="{Binding EditingTileVm.Opacity, UpdateSourceTrigger=PropertyChanged}"
                                Minimum="0" Maximum="90" VerticalAlignment="Center"/>
                        <TextBlock Grid.Column="2" Foreground="#F0F0F0"
                                   VerticalAlignment="Center" Margin="4,0,0,0">
                            <Run Text="{Binding EditingTileVm.Opacity, Mode=OneWay}"/><Run Text="%"/>
                        </TextBlock>
                    </Grid>
                    <!-- フォント名 -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="フォント名" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <ComboBox x:Name="FontNameCombo" Grid.Column="1"
                                  SelectedValue="{Binding EditingTileVm.FontName, UpdateSourceTrigger=PropertyChanged}"/>
                    </Grid>
                    <!-- フォントサイズ -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="フォントサイズ" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1"
                                 Text="{Binding EditingTileVm.FontSizePt, UpdateSourceTrigger=PropertyChanged}"
                                 Background="#30FFFFFF" Foreground="#F0F0F0"
                                 BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                        <TextBlock Grid.Column="2" Text="pt" Foreground="#F0F0F0"
                                   VerticalAlignment="Center" Margin="4,0,0,0"/>
                    </Grid>
                    <!-- フォントカラー -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="フォントカラー" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <ComboBox x:Name="FontColorCombo" Grid.Column="1"
                                  SelectedValue="{Binding EditingTileVm.FontColor, UpdateSourceTrigger=PropertyChanged}"
                                  SelectedValuePath="Name">
                            <ComboBox.ItemTemplate>
                                <DataTemplate>
                                    <StackPanel Orientation="Horizontal">
                                        <Border Width="14" Height="14" Margin="0,0,6,0" CornerRadius="2" Background="{Binding Brush}"/>
                                        <TextBlock Text="{Binding Name}" Foreground="#2D2D2D"/>
                                    </StackPanel>
                                </DataTemplate>
                            </ComboBox.ItemTemplate>
                        </ComboBox>
                    </Grid>
                    <!-- 画像 / アイコン -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="画像/アイコン" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <TextBox Grid.Column="1"
                                 Text="{Binding EditingTileVm.ImagePath, UpdateSourceTrigger=PropertyChanged}"
                                 Background="#30FFFFFF" Foreground="#F0F0F0"
                                 BorderThickness="0,0,0,1" BorderBrush="#60F0F0F0" Padding="4,2"/>
                        <Border x:Name="BrowseImageButton" Grid.Column="2"
                                Width="36" Height="22" Margin="4,0,0,0"
                                CornerRadius="4" Cursor="Hand">
                            <TextBlock Text="参照" Foreground="White"
                                       HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="11"/>
                        </Border>
                    </Grid>
                    <!-- 表示位置 -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="表示位置" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <ComboBox x:Name="ImagePositionCombo" Grid.Column="1"
                                  SelectedValue="{Binding EditingTileVm.ImagePosition, UpdateSourceTrigger=PropertyChanged}"
                                  SelectedValuePath="Value"/>
                    </Grid>
                    <!-- 画像透過 -->
                    <Grid Margin="0,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="90"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Grid.Column="0" Text="画像透過" Foreground="#F0F0F0" VerticalAlignment="Center"/>
                        <CheckBox Grid.Column="1"
                                  IsChecked="{Binding EditingTileVm.ImageTransparent, UpdateSourceTrigger=PropertyChanged}"
                                  VerticalAlignment="Center"/>
                    </Grid>
                </StackPanel>

                <Rectangle Height="12"/>
            </StackPanel>
        </ScrollViewer>

        <!-- ─── OK / キャンセルボタン ─────────────────────────────────── -->
        <StackPanel Grid.Row="2" Orientation="Horizontal"
                    HorizontalAlignment="Right" Margin="12,4,12,12">
            <Border x:Name="CancelButton" Width="80" Height="32" CornerRadius="6" Cursor="Hand">
                <TextBlock Text="キャンセル" Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="12"/>
            </Border>
            <Border x:Name="OkButton" Width="80" Height="32" CornerRadius="6" Cursor="Hand" Margin="8,0,0,0">
                <TextBlock Text="OK" Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="12"/>
            </Border>
        </StackPanel>
    </Grid>
</UserControl>
```

---

### 5. Views/Controls/TileEditControl.xaml.cs（新規）

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppLauncher.Helpers;
using AppLauncher.Models;

namespace AppLauncher.Views.Controls;

public partial class TileEditControl : UserControl
{
    private sealed record ColorItem(string Name, SolidColorBrush Brush);
    private sealed record ValueItem(string Value, string Display);

    private static readonly IReadOnlyList<ColorItem> ColorItems =
        ColorPalette.All.Keys
            .Select(n => new ColorItem(n, ColorPalette.GetBrush(n)))
            .ToList();

    private static readonly IReadOnlyList<ValueItem> TileTypes =
    [
        new("app",     "アプリ (app)"),
        new("url",     "URL (url)"),
        new("folder",  "フォルダ (folder)"),
        new("webview", "ウェブビュー (webview)"),
        new("system",  "システム情報 (system)"),
    ];

    private static readonly IReadOnlyList<ValueItem> SiCategories =
    [
        new("os",      "OS情報"),
        new("storage", "ストレージ"),
        new("usage",   "使用率"),
    ];

    private static readonly IReadOnlyList<ValueItem> SiDeviceTypes =
    [
        new("cpu",    "CPU"),
        new("memory", "メモリ"),
        new("gpu",    "GPU"),
        new("lan",    "LAN"),
    ];

    private static readonly IReadOnlyList<ValueItem> SiDisplayFormats =
    [
        new("text",   "テキスト表示"),
        new("circle", "円形グラフ"),
    ];

    private static readonly IReadOnlyList<ValueItem> ImagePositions =
    [
        new("top",    "上"),
        new("bottom", "下"),
        new("left",   "左"),
        new("right",  "右"),
    ];

    public TileEditControl()
    {
        InitializeComponent();

        TypeCombo.ItemsSource        = TileTypes;
        TypeCombo.DisplayMemberPath  = nameof(ValueItem.Display);

        SiCategoryCombo.ItemsSource       = SiCategories;
        SiCategoryCombo.DisplayMemberPath = nameof(ValueItem.Display);

        SiDeviceTypeCombo.ItemsSource       = SiDeviceTypes;
        SiDeviceTypeCombo.DisplayMemberPath = nameof(ValueItem.Display);

        SiDisplayFormatCombo.ItemsSource       = SiDisplayFormats;
        SiDisplayFormatCombo.DisplayMemberPath = nameof(ValueItem.Display);

        SiMainColorCombo.ItemsSource    = ColorItems;
        SiAccentColorCombo.ItemsSource  = ColorItems;
        SiBgAccentColorCombo.ItemsSource = ColorItems;
        TileColorCombo.ItemsSource      = ColorItems;
        FontColorCombo.ItemsSource      = ColorItems;

        FontNameCombo.ItemsSource = new[] { "" }
            .Concat(Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n))
            .ToList();

        ImagePositionCombo.ItemsSource       = ImagePositions;
        ImagePositionCombo.DisplayMemberPath = nameof(ValueItem.Display);

        BasicHeader.MouseLeftButtonUp      += (_, _) => ToggleSection(BasicContent,      BasicArrow,      "基本");
        LaunchHeader.MouseLeftButtonUp     += (_, _) => ToggleSection(LaunchContent,     LaunchArrow,     "起動");
        SystemHeader.MouseLeftButtonUp     += (_, _) => ToggleSection(SystemContent,     SystemArrow,     "システム情報");
        AppearanceHeader.MouseLeftButtonUp += (_, _) => ToggleSection(AppearanceContent, AppearanceArrow, "見た目");

        BrowsePathButton.MouseLeftButtonUp    += OnBrowsePath;
        BrowseWorkDirButton.MouseLeftButtonUp += OnBrowseWorkDir;
        BrowseImageButton.MouseLeftButtonUp   += OnBrowseImage;

        OkButton.MouseLeftButtonUp     += (_, _) => App.LauncherViewModel?.ConfirmTileEditCommand.Execute(null);
        CancelButton.MouseLeftButtonUp += (_, _) => App.LauncherViewModel?.CloseTileEditCommand.Execute(null);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyUiElementColor();

    // ページ背景色から計算した UI 配色をボタン・セクション区切りへ適用する
    public void ApplyUiElementColor()
    {
        var vm = App.LauncherViewModel;
        if (vm == null) return;
        var baseColor = ColorPalette.GetColor(vm.CurrentPage.BackgroundColor);
        var uiBrush   = new SolidColorBrush(ColorHelper.ComputeUiElementColor(baseColor));

        BasicHeader.BorderBrush      = uiBrush;
        LaunchHeader.BorderBrush     = uiBrush;
        SystemHeader.BorderBrush     = uiBrush;
        AppearanceHeader.BorderBrush = uiBrush;
        CancelButton.Background      = uiBrush;
        OkButton.Background          = uiBrush;
        BrowsePathButton.Background    = uiBrush;
        BrowseWorkDirButton.Background = uiBrush;
        BrowseImageButton.Background   = uiBrush;
    }

    private static void ToggleSection(UIElement content, TextBlock arrow, string label)
    {
        bool isVisible = content.Visibility == Visibility.Visible;
        content.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
        arrow.Text = (isVisible ? "▼ " : "▲ ") + label;
    }

    private void OnBrowsePath(object sender, MouseButtonEventArgs e)
    {
        var vm = App.LauncherViewModel?.EditingTileVm;
        if (vm == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "実行ファイルを選択",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) vm.Path = dlg.FileName;
    }

    private void OnBrowseWorkDir(object sender, MouseButtonEventArgs e)
    {
        var vm = App.LauncherViewModel?.EditingTileVm;
        if (vm == null) return;
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "作業フォルダを選択" };
        if (dlg.ShowDialog() == true) vm.WorkDir = dlg.FolderName;
    }

    private void OnBrowseImage(object sender, MouseButtonEventArgs e)
    {
        var vm = App.LauncherViewModel?.EditingTileVm;
        if (vm == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "画像 / アイコンを選択",
            Filter = "画像ファイル (*.png;*.jpg;*.gif;*.ico)|*.png;*.jpg;*.gif;*.ico|すべてのファイル (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) vm.ImagePath = dlg.FileName;
    }
}
```

---

### 6. Views/LauncherWindow.xaml の変更

Frame 内の `<Grid>` に `TileEditControl` を追加する。`Grid.RowSpan="7"` で全行を覆い、`DeleteConfirmOverlay`（ZIndex=100）より手前・通常コンテンツより前面（ZIndex=50）に配置する。

追加位置：`DeleteConfirmOverlay` の直前。

```xml
<!-- タイル詳細設定パネル（TileEdit モード時に Frame 内全体に表示） -->
<controls:TileEditControl x:Name="TileEditPanel"
                          Grid.RowSpan="7"
                          Visibility="Collapsed"
                          Panel.ZIndex="50"/>
```

---

### 7. Views/LauncherWindow.xaml.cs の変更

`SetViewModel` 内の既存 `vm.PropertyChanged` ラムダに `Mode` 購読を追加する。

```csharp
// ─── 変更：既存の PropertyChanged ラムダに以下を追加 ──────────────────────
if (e.PropertyName == nameof(LauncherViewModel.Mode))
{
    bool isTileEdit = vm.Mode == AppMode.TileEdit;
    TileEditPanel.Visibility = isTileEdit ? Visibility.Visible : Visibility.Collapsed;
    if (isTileEdit)
        TileEditPanel.ApplyUiElementColor();
}
```

---

## 完了条件

- [ ] 編集モードでタイルの編集ボタン（✎）をクリックするとタイル詳細設定パネルが Frame 内全体を覆って表示される
- [ ] パネルの背景が Frame と同じページ背景色で描画される
- [ ] セクション区切り線・OK/キャンセル/参照ボタンの色がページ背景色から計算された値になる
- [ ] プレビューエリア（200×96）がカラー・透過率・タイトル・フォントカラー変更をリアルタイムに反映する
- [ ] 種類コンボボックスを `system` に変更すると起動セクションが非表示になりシステム情報セクションが表示される
- [ ] システム情報セクションでカテゴリを `os` に変更するとデバイス種別・アクセント・基準値等が非表示になる
- [ ] app タイプのパス欄と作業フォルダ欄に「参照」ボタンが表示され、ファイル/フォルダ選択ダイアログが開く
- [ ] 画像/アイコン欄の「参照」ボタンで画像ファイル選択ダイアログが開く
- [ ] セクションヘッダーをクリックすると内容が折りたたみ/展開し ▲▼ が切り替わる
- [ ] カラーコンボボックスの各選択肢にカラーサンプル矩形とカラー名が表示される
- [ ] フォント名コンボボックスに OS インストール済みフォント一覧が表示される（先頭は空欄＝システムデフォルト）
- [ ] OK を押すと変更が対象タイルへ反映され config.json へ保存されて編集モードへ戻る
- [ ] キャンセルを押すと変更が破棄されて編集モードへ戻る
- [ ] `dotnet build` がエラーなく成功する

---

## 参照定義書

- `definition/ui/ランチャー本体.md § 8` — 設定画面共通レイアウト・プレビューエリア・セクション・UI 要素配色計算ルール
- `definition/function/アプリケーション機能定義.md § 2.4` — タイル編集モードの遷移条件
- `definition/function/アプリケーション機能定義.md § 3.5` — タイル詳細設定の全セクション・設定項目一覧
- `definition/data/設定ファイルフォーマット.md § 3.3` — タイル設定の JSON フォーマット・型仕様
