# Ph.7 実装指示書 ─ タイル編集

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.7 タイル編集 |
| 実装目的 | 編集モードでのタイル追加・移動・リサイズ・削除・ページ名インライン編集 |
| 未確定事項 | なし |
| 既存影響 | `TileViewModel` の Col/Row/ColSpan/RowSpan を可変化。`PageViewModel.Tiles` を ObservableCollection へ変換・`Name` を ObservableProperty へ変換。`TileGridControl` を全面改修。`TileControl` に編集オーバーレイ追加（通常モードの動作は変わらない） |
| 追加ライブラリ | なし |
| 実装範囲 | 空スロットクリックによるタイル追加・タイルのD&D移動・リサイズ・削除確認ダイアログ・配置競合チェック・タイルの入れ替え・ページ名インライン編集。タイル詳細設定画面（タイル編集モードUI）は対象外（Ph.8）。タイル編集ボタン押下でのモード遷移は状態管理のみ実装 |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `Models/AppMode.cs` | 変更：`TileEdit` 値を追加 |
| `Services/SnapService.cs` | 変更：`UpdateWindowHeight` で `TileEdit` を `Edit` と同一扱い |
| `ViewModels/TileViewModel.cs` | 変更：`Col`・`Row`・`ColSpan`・`RowSpan` を可変プロパティへ。`CreateNew` / `ToConfig` を追加 |
| `ViewModels/PageViewModel.cs` | 変更：`Name` を `[ObservableProperty]` へ。`Tiles` を `ObservableCollection<TileViewModel>` へ |
| `ViewModels/LauncherViewModel.cs` | 変更：`EditingTile`・`PendingDeleteTile` プロパティ追加。タイル操作コマンド5件追加 |
| `Views/Controls/TileControl.xaml` | 変更：編集モード用オーバーレイ（編集・削除ボタン、リサイズハンドル）追加 |
| `Views/Controls/TileControl.xaml.cs` | 変更：編集モード時のオーバーレイ制御・D&D ドラッグ開始・リサイズ開始イベント発火 |
| `Views/Controls/TileGridControl.xaml` | 変更：オーバーレイ Canvas・DragPreview 矩形追加 |
| `Views/Controls/TileGridControl.xaml.cs` | 変更：空スロット操作・D&D ドロップ・リサイズ・競合チェック（全面改修） |
| `Views/Controls/PageNameControl.xaml` | 変更：インライン編集用 TextBox 追加 |
| `Views/Controls/PageNameControl.xaml.cs` | 変更：モード連動・インライン編集・確定処理 |
| `Views/LauncherWindow.xaml` | 変更：Frame 内にタイル削除確認ダイアログオーバーレイ追加 |
| `Views/LauncherWindow.xaml.cs` | 変更：`SetViewModel` に `PendingDeleteTile` 購読とダイアログボタン配線を追加 |

---

## 実装仕様

### 1. AppMode.cs の変更

`TileEdit` 値を追加する。タイル詳細設定画面（Ph.8 で実装）への遷移状態を表す。

```csharp
namespace AppLauncher.Models;

public enum AppMode { Normal, Edit, TileEdit }
```

---

### 2. SnapService.cs の変更

`UpdateWindowHeight` の判定に `TileEdit` モードを追加する。
`TileEdit` はウィンドウ高さを `Edit` モードと同一（ボトムバー2行分を維持）に保つ。

```csharp
// ─── 変更箇所のみ（他は現状のまま）────────────────────────────────────────
private void UpdateWindowHeight()
{
    bool isExpanded = App.LauncherViewModel?.Mode is AppMode.Edit or AppMode.TileEdit;
    _window.Height = _baseFrameH + (isExpanded ? 33 : 0);
}
```

using に `AppLauncher.Models` が既に含まれていることを確認すること。

---

### 3. TileViewModel.cs の変更

`Col`・`Row`・`ColSpan`・`RowSpan` を `[ObservableProperty]` に変更（移動・リサイズで値を更新するため）。
`CreateNew` で空スロットクリック時のデフォルトタイルを生成する。
`ToConfig` で Ph.8 の OK ボタン保存処理に使用できる変換を提供する。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class TileViewModel : ObservableObject
{
    [ObservableProperty] private int _col;
    [ObservableProperty] private int _row;
    [ObservableProperty] private int _colSpan;
    [ObservableProperty] private int _rowSpan;

    public string Type          { get; }
    public string Title         { get; }
    public string Path          { get; }
    public string Args          { get; }
    public string WorkDir       { get; }
    public string Color         { get; }
    public int    Opacity       { get; }
    public string FontName      { get; }
    public int    FontSizePt    { get; }
    public string FontColor     { get; }
    public string ImagePath     { get; }
    public string ImagePosition { get; }
    public bool   ImageTransparent { get; }
    public SystemInfoConfig? SystemInfo { get; }

    public TileViewModel(TileConfig config)
    {
        _col     = config.Col;
        _row     = config.Row;
        _colSpan = config.ColSpan;
        _rowSpan = config.RowSpan;
        Type          = config.Type;
        Title         = config.Title;
        Path          = config.Path;
        Args          = config.Args;
        WorkDir       = config.WorkDir;
        Color         = config.Color;
        Opacity       = config.Opacity;
        FontName      = config.FontName;
        FontSizePt    = config.FontSizePt;
        FontColor     = config.FontColor;
        ImagePath     = config.ImagePath;
        ImagePosition = config.ImagePosition;
        ImageTransparent = config.ImageTransparent;
        SystemInfo    = config.SystemInfo;
    }

    // 空スロットクリックで呼ぶデフォルトタイル生成
    public static TileViewModel CreateNew(int col, int row) => new(new TileConfig
    {
        Col     = col, Row     = row,
        ColSpan = 1,   RowSpan = 1,
        Type    = "app",
        Color   = "blue", Opacity = 20,
        FontSizePt = 16, FontColor = "white",
        ImagePosition = "top",
    });

    // Ph.8 の OK 保存処理で使用する config への変換
    public TileConfig ToConfig() => new()
    {
        Col    = Col,    Row    = Row,
        ColSpan = ColSpan, RowSpan = RowSpan,
        Type   = Type,   Title  = Title,
        Path   = Path,   Args   = Args,   WorkDir = WorkDir,
        Color  = Color,  Opacity = Opacity,
        FontName = FontName, FontSizePt = FontSizePt, FontColor = FontColor,
        ImagePath = ImagePath, ImagePosition = ImagePosition,
        ImageTransparent = ImageTransparent, SystemInfo = SystemInfo,
    };
}
```

---

### 4. PageViewModel.cs の変更

`Name` を `[ObservableProperty]` に変更（インライン編集で直接セットするため）。
`Tiles` を `ObservableCollection<TileViewModel>` に変更（追加・削除を TileGridControl が監視するため）。

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class PageViewModel : ObservableObject
{
    [ObservableProperty] private string _name;

    public string BackgroundColor   { get; }
    public int    BackgroundOpacity { get; }
    public string HandleColor       { get; }
    public int    HandleOpacity     { get; }
    public string PinFrameColor     { get; }
    public int    PinFrameOpacity   { get; }

    public ObservableCollection<TileViewModel> Tiles { get; }

    public PageViewModel(PageConfig config)
    {
        _name             = config.Name;
        BackgroundColor   = config.BackgroundColor;
        BackgroundOpacity = config.BackgroundOpacity;
        HandleColor       = config.HandleColor;
        HandleOpacity     = config.HandleOpacity;
        PinFrameColor     = config.PinFrameColor;
        PinFrameOpacity   = config.PinFrameOpacity;
        Tiles = new ObservableCollection<TileViewModel>(
            config.Tiles.Select(t => new TileViewModel(t)));
    }
}
```

---

### 5. LauncherViewModel.cs の変更

以下を追加する。

- `EditingTile`：タイル詳細設定中のタイル（Ph.8 で利用）
- `PendingDeleteTile`：削除確認ダイアログの対象タイル
- `OpenTileEditCommand`：タイル編集ボタン押下時に `AppMode.TileEdit` へ遷移
- `CloseTileEditCommand`：タイル詳細設定の OK/キャンセルで `AppMode.Edit` へ戻る（Ph.8 で呼ぶ）
- `RequestDeleteTileCommand`：削除ボタン押下時に確認ダイアログを表示
- `ConfirmDeleteTileCommand`：確認 OK でタイルを削除
- `CancelDeleteTileCommand`：確認キャンセルで閉じる

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppLauncher.Models;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class LauncherViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private int _currentPageIndex;

    [ObservableProperty]
    private AppMode _mode = AppMode.Normal;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isStored;

    [ObservableProperty]
    private TileViewModel? _editingTile;

    [ObservableProperty]
    private TileViewModel? _pendingDeleteTile;

    private bool _pinnedBeforeEdit;

    public ObservableCollection<PageViewModel> Pages { get; }

    public double PageNameFontSize { get; }

    public PageViewModel CurrentPage => Pages[CurrentPageIndex];

    public LauncherViewModel(AppConfig config)
    {
        Pages = new ObservableCollection<PageViewModel>(
            config.Pages.Select(p => new PageViewModel(p)));
        PageNameFontSize = config.Global.PageNameFontSizePt * 4.0 / 3.0;
    }

    [RelayCommand]
    private void NavigateToPage(int index)
    {
        if (index >= 0 && index < Pages.Count)
            CurrentPageIndex = index;
    }

    [RelayCommand]
    private void ToggleMode()
    {
        if (Mode == AppMode.Normal)
        {
            _pinnedBeforeEdit = IsPinned;
            Mode     = AppMode.Edit;
            IsPinned = true;
        }
        else
        {
            Mode     = AppMode.Normal;
            IsPinned = _pinnedBeforeEdit;
        }
    }

    // タイル編集ボタン押下 → TileEdit モードへ遷移（Ph.8 で設定 UI を表示）
    [RelayCommand]
    private void OpenTileEdit(TileViewModel tile)
    {
        EditingTile = tile;
        Mode = AppMode.TileEdit;
    }

    // タイル詳細設定の OK/キャンセルから呼ぶ（Ph.8 で使用）
    [RelayCommand]
    private void CloseTileEdit()
    {
        EditingTile = null;
        Mode = AppMode.Edit;
    }

    // 削除ボタン → 確認ダイアログ表示
    [RelayCommand]
    private void RequestDeleteTile(TileViewModel tile)
    {
        PendingDeleteTile = tile;
    }

    // 確認 OK → タイルを現在ページから削除
    [RelayCommand]
    private void ConfirmDeleteTile()
    {
        if (PendingDeleteTile == null) return;
        CurrentPage.Tiles.Remove(PendingDeleteTile);
        PendingDeleteTile = null;
    }

    // 確認キャンセル → ダイアログを閉じる
    [RelayCommand]
    private void CancelDeleteTile()
    {
        PendingDeleteTile = null;
    }

    [RelayCommand]
    private void AddPage()
    {
        if (Pages.Count >= 10) return;
        var src = CurrentPage;
        var cfg = new PageConfig
        {
            Name              = $"ページ{Pages.Count + 1}",
            BackgroundColor   = src.BackgroundColor,
            BackgroundOpacity = src.BackgroundOpacity,
            HandleColor       = src.HandleColor,
            HandleOpacity     = src.HandleOpacity,
            PinFrameColor     = src.PinFrameColor,
            PinFrameOpacity   = src.PinFrameOpacity,
        };
        int insertAt = CurrentPageIndex + 1;
        Pages.Insert(insertAt, new PageViewModel(cfg));
        CurrentPageIndex = insertAt;
    }

    [RelayCommand]
    private void RemovePage()
    {
        if (Pages.Count <= 1) return;
        if (CurrentPage.Tiles.Count > 0) return;
        int removed = CurrentPageIndex;
        CurrentPageIndex = removed > 0 ? removed - 1 : 0;
        Pages.RemoveAt(removed);
    }

    [RelayCommand]
    private void MovePageLeft()
    {
        if (CurrentPageIndex <= 0) return;
        int i = CurrentPageIndex;
        (Pages[i], Pages[i - 1]) = (Pages[i - 1], Pages[i]);
        CurrentPageIndex = i - 1;
    }

    [RelayCommand]
    private void MovePageRight()
    {
        if (CurrentPageIndex >= Pages.Count - 1) return;
        int i = CurrentPageIndex;
        (Pages[i], Pages[i + 1]) = (Pages[i + 1], Pages[i]);
        CurrentPageIndex = i + 1;
    }
}
```

---

### 6. TileControl.xaml の変更

`EditOverlay` Grid を追加する。編集モード中のマウスオーバーで表示される。

```xml
<UserControl x:Class="AppLauncher.Views.Controls.TileControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             RenderTransformOrigin="0.5,1.0">

    <UserControl.RenderTransform>
        <ScaleTransform x:Name="HoverScale"/>
    </UserControl.RenderTransform>

    <Border x:Name="TileBorder" CornerRadius="20">
        <Grid>
            <!-- タイルタイトル -->
            <TextBlock x:Name="TitleText"
                       HorizontalAlignment="Center"
                       VerticalAlignment="Center"
                       TextWrapping="Wrap"
                       TextAlignment="Center"/>

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
                <!-- 編集ボタン（左上） -->
                <Border x:Name="EditButton"
                        Width="22" Height="22"
                        HorizontalAlignment="Left" VerticalAlignment="Top"
                        Margin="4" CornerRadius="4" Cursor="Hand"
                        Background="#CC333333">
                    <TextBlock Text="✎" FontSize="11" Foreground="White"
                               HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <!-- 削除ボタン（右上） -->
                <Border x:Name="DeleteButton"
                        Width="22" Height="22"
                        HorizontalAlignment="Right" VerticalAlignment="Top"
                        Margin="4" CornerRadius="4" Cursor="Hand"
                        Background="#CCE05252">
                    <TextBlock Text="✕" FontSize="11" Foreground="White"
                               HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <!-- リサイズハンドル（右下） -->
                <Border x:Name="ResizeHandle"
                        Width="16" Height="16"
                        HorizontalAlignment="Right" VerticalAlignment="Bottom"
                        Margin="4" CornerRadius="3" Cursor="SizeNWSE"
                        Background="#CCF0F0F0"/>
            </Grid>
        </Grid>
    </Border>
</UserControl>
```

---

### 7. TileControl.xaml.cs の変更

編集モード時のオーバーレイ表示・D&D ドラッグ開始・リサイズ開始イベント発火を追加する。
通常モードのクリック起動・ホバーアニメーション動作は変えない。

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AppLauncher.Models;
using AppLauncher.Services;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class TileControl : UserControl
{
    private TileViewModel? _tile;
    private Color _pageBackgroundColor;
    private Point _mouseDownPos;
    private bool _dragStarted;

    // TileGridControl が購読するイベント
    public event Action<TileViewModel>? EditRequested;
    public event Action<TileViewModel>? DeleteRequested;
    public event Action<TileViewModel, MouseButtonEventArgs>? ResizeStarted;

    public TileControl()
    {
        InitializeComponent();
        EditButton.MouseLeftButtonUp   += OnEditButtonClick;
        DeleteButton.MouseLeftButtonUp += OnDeleteButtonClick;
        ResizeHandle.MouseLeftButtonDown += OnResizeHandleMouseDown;
    }

    public void Apply(TileViewModel tile, int cornerRadius, Color pageBackgroundColor)
    {
        _tile = tile;
        _pageBackgroundColor = pageBackgroundColor;

        var bgColor = ColorPalette.GetColor(tile.Color);
        double alpha = ColorPalette.OpacityToDouble(tile.Opacity);
        TileBorder.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), bgColor.R, bgColor.G, bgColor.B));
        TileBorder.CornerRadius = new CornerRadius(cornerRadius);

        TitleText.Text     = tile.Title;
        TitleText.FontSize = tile.FontSizePt * 4.0 / 3.0;
        TitleText.Foreground = new SolidColorBrush(ColorPalette.GetColor(tile.FontColor));
        if (!string.IsNullOrEmpty(tile.FontName))
            TitleText.FontFamily = new FontFamily(tile.FontName);
    }

    // ─── ボタン・ハンドルイベント ──────────────────────────────────────
    private void OnEditButtonClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_tile != null) EditRequested?.Invoke(_tile);
    }

    private void OnDeleteButtonClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_tile != null) DeleteRequested?.Invoke(_tile);
    }

    private void OnResizeHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_tile == null) return;
        e.Handled = true;
        ResizeStarted?.Invoke(_tile, e);
    }

    // ─── マウスボタン ─────────────────────────────────────────────────
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _mouseDownPos = e.GetPosition(this);
        _dragStarted  = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (App.LauncherViewModel?.Mode != AppMode.Edit) return;
        if (_tile == null || _dragStarted) return;
        if ((e.GetPosition(this) - _mouseDownPos).Length < 5.0) return;

        _dragStarted = true;
        DragDrop.DoDragDrop(this, _tile, DragDropEffects.Move);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_tile == null) return;
        if (App.LauncherViewModel?.Mode != AppMode.Normal) return;

        if ((e.GetPosition(this) - _mouseDownPos).Length < 5.0)
        {
            e.Handled = true;
            TileLaunchService.Launch(_tile);
        }
    }

    // ─── ホバー ───────────────────────────────────────────────────────
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (_tile == null) return;

        var mode = App.LauncherViewModel?.Mode;

        if (mode == AppMode.Edit)
        {
            EditOverlay.Visibility = Visibility.Visible;
            return;
        }
        if (mode != AppMode.Normal) return;

        if (_tile.Args.Contains("{drop}"))
        {
            DndOverlay.Background = new SolidColorBrush(_pageBackgroundColor);
            DndOverlay.Visibility = Visibility.Visible;
            return;
        }

        Panel.SetZIndex(this, 100);
        AnimateScale(1.13);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        EditOverlay.Visibility = Visibility.Collapsed;
        DndOverlay.Visibility  = Visibility.Collapsed;
        Panel.SetZIndex(this, 0);
        AnimateScale(1.0);
    }

    private void AnimateScale(double to)
    {
        var duration = TimeSpan.FromMilliseconds(120);
        HoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, duration));
        HoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, duration));
    }
}
```

---

### 8. TileGridControl.xaml の変更

`TileGrid` を親 `Grid` でラップし、D&D・リサイズ共用のプレビュー矩形（`DragPreview`）を乗せる `Canvas` を追加する。

```xml
<UserControl x:Class="AppLauncher.Views.Controls.TileGridControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <Grid x:Name="TileGrid" AllowDrop="True"/>

        <!-- D&D / リサイズプレビュー用オーバーレイ（ヒットテスト無効） -->
        <Canvas x:Name="OverlayCanvas" IsHitTestVisible="False">
            <Rectangle x:Name="DragPreview"
                       Visibility="Collapsed"
                       StrokeThickness="2"
                       StrokeDashArray="6,4"
                       Fill="#33FFFFFF"/>
        </Canvas>
    </Grid>
</UserControl>
```

---

### 9. TileGridControl.xaml.cs の変更

以下の機能をすべて実装する。全面改修となる。

**空スロット**：編集モード時はクリックでタイル作成・マウスオーバーでハイライト  
**D&D 移動**：ドラッグ中はプレビュー枠表示、ドロップ位置が有効なら移動、同サイズのタイル上でドロップなら入れ替え  
**リサイズ**：ResizeHandle ドラッグでプレビュー枠表示、マウスアップで確定  
**競合チェック**：`CanPlace` でグリッド外・他タイルとの重なりを検出

```csharp
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AppLauncher.Models;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class TileGridControl : UserControl
{
    private TileViewModel? _resizingTile;
    private bool _modeSubscribed;

    public TileGridControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        TileGrid.DragOver  += OnTileGridDragOver;
        TileGrid.DragLeave += (_, _) => HidePreview();
        TileGrid.Drop      += OnTileGridDrop;
        TileGrid.MouseMove += OnTileGridMouseMove;
        TileGrid.MouseLeftButtonUp += OnTileGridMouseLeftButtonUp;
    }

    // ─── DataContext（ページ切り替え）────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PageViewModel oldPage)
            oldPage.Tiles.CollectionChanged -= OnTilesChanged;

        if (DataContext is PageViewModel newPage)
        {
            newPage.Tiles.CollectionChanged += OnTilesChanged;
            if (!_modeSubscribed && App.LauncherViewModel is { } vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                _modeSubscribed = true;
            }
        }
        Rebuild();
    }

    private void OnTilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.Mode)) Rebuild();
    }

    // ─── グリッド再構築 ───────────────────────────────────────────────

    private void Rebuild()
    {
        TileGrid.Children.Clear();
        TileGrid.ColumnDefinitions.Clear();
        TileGrid.RowDefinitions.Clear();

        if (DataContext is not PageViewModel page) return;

        var config  = App.ConfigService.Current;
        var layout  = config.Layout;
        var global  = config.Global;
        int cols        = global.TileCountCols;
        int rows        = global.TileCountRows;
        int tileSize    = layout.TileSize;
        int tileMargin  = layout.TileMargin;
        int cornerRadius = layout.TileCornerRadius;
        bool isEditMode = App.LauncherViewModel?.Mode == AppMode.Edit;

        // 列・行定義（タイルとスペーサーを交互）
        for (int c = 0; c < cols; c++)
        {
            TileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(tileSize) });
            if (c < cols - 1)
                TileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(tileMargin) });
        }
        for (int r = 0; r < rows; r++)
        {
            TileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(tileSize) });
            if (r < rows - 1)
                TileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(tileMargin) });
        }

        // 占有セルのマーク
        var occupied = new bool[cols, rows];
        foreach (var tile in page.Tiles)
            for (int dc = 0; dc < tile.ColSpan; dc++)
                for (int dr = 0; dr < tile.RowSpan; dr++)
                {
                    int c = tile.Col + dc, r = tile.Row + dr;
                    if (c < cols && r < rows) occupied[c, r] = true;
                }

        // 空スロット
        var bgColor   = ColorPalette.GetColor(page.BackgroundColor);
        var slotBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * 0.4), bgColor.R, bgColor.G, bgColor.B));

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (occupied[c, r]) continue;
                var rect = new Rectangle
                {
                    Stroke          = slotBrush,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection([6.0, 4.0]),
                    Fill            = Brushes.Transparent,
                };
                Grid.SetColumn(rect, c * 2);
                Grid.SetRow(rect, r * 2);

                if (isEditMode)
                {
                    int col = c, row = r;
                    rect.Cursor = Cursors.Hand;
                    rect.MouseEnter += (_, _) => rect.Fill = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                    rect.MouseLeave += (_, _) => rect.Fill = Brushes.Transparent;
                    rect.MouseLeftButtonUp += (_, _) => CreateTileAt(col, row);
                }

                TileGrid.Children.Add(rect);
            }
        }

        // タイル配置
        foreach (var tile in page.Tiles)
        {
            var control = new TileControl();
            control.Apply(tile, cornerRadius, bgColor);
            control.EditRequested   += OnTileEditRequested;
            control.DeleteRequested += OnTileDeleteRequested;
            control.ResizeStarted   += OnTileResizeStarted;
            Grid.SetColumn(control, tile.Col * 2);
            Grid.SetRow(control, tile.Row * 2);
            Grid.SetColumnSpan(control, tile.ColSpan * 2 - 1);
            Grid.SetRowSpan(control, tile.RowSpan * 2 - 1);
            TileGrid.Children.Add(control);
        }
    }

    // ─── タイル操作 ───────────────────────────────────────────────────

    private void CreateTileAt(int col, int row)
    {
        if (DataContext is not PageViewModel page) return;
        page.Tiles.Add(TileViewModel.CreateNew(col, row));
        // CollectionChanged → Rebuild() が自動発火
    }

    private void OnTileEditRequested(TileViewModel tile)
        => App.LauncherViewModel?.OpenTileEditCommand.Execute(tile);

    private void OnTileDeleteRequested(TileViewModel tile)
        => App.LauncherViewModel?.RequestDeleteTileCommand.Execute(tile);

    // ─── リサイズ ──────────────────────────────────────────────────────

    private void OnTileResizeStarted(TileViewModel tile, MouseButtonEventArgs e)
    {
        _resizingTile = tile;
        TileGrid.CaptureMouse();
        e.Handled = true;
    }

    private void OnTileGridMouseMove(object sender, MouseEventArgs e)
    {
        if (_resizingTile == null) return;
        var (cs, rs) = ComputeResizeSpan(e.GetPosition(TileGrid));
        bool valid = CanPlace(_resizingTile.Col, _resizingTile.Row, cs, rs, _resizingTile);
        ShowPreview(_resizingTile.Col, _resizingTile.Row, cs, rs, valid);
    }

    private void OnTileGridMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizingTile == null) return;
        var (cs, rs) = ComputeResizeSpan(e.GetPosition(TileGrid));
        if (CanPlace(_resizingTile.Col, _resizingTile.Row, cs, rs, _resizingTile))
        {
            _resizingTile.ColSpan = cs;
            _resizingTile.RowSpan = rs;
            Rebuild();
        }
        HidePreview();
        _resizingTile = null;
        TileGrid.ReleaseMouseCapture();
    }

    private (int colSpan, int rowSpan) ComputeResizeSpan(Point mousePos)
    {
        var layout = App.ConfigService.Current.Layout;
        var global = App.ConfigService.Current.Global;
        int step = layout.TileSize + layout.TileMargin;
        double relX = mousePos.X - _resizingTile!.Col * step;
        double relY = mousePos.Y - _resizingTile!.Row * step;
        int cs = Math.Max(1, (int)Math.Ceiling(relX / step));
        int rs = Math.Max(1, (int)Math.Ceiling(relY / step));
        cs = Math.Min(cs, global.TileCountCols - _resizingTile.Col);
        rs = Math.Min(rs, global.TileCountRows - _resizingTile.Row);
        return (cs, rs);
    }

    // ─── D&D ──────────────────────────────────────────────────────────

    private void OnTileGridDragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not PageViewModel) return;
        if (e.Data.GetData(typeof(TileViewModel)) is not TileViewModel drag) return;

        var pos = e.GetPosition(TileGrid);
        var (col, row) = PositionToCell(pos);
        bool valid = CanPlace(col, row, drag.ColSpan, drag.RowSpan, drag);
        ShowPreview(col, row, drag.ColSpan, drag.RowSpan, valid);
        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTileGridDrop(object sender, DragEventArgs e)
    {
        HidePreview();
        if (DataContext is not PageViewModel page) return;
        if (e.Data.GetData(typeof(TileViewModel)) is not TileViewModel drag) return;

        var pos = e.GetPosition(TileGrid);
        var (col, row) = PositionToCell(pos);

        // 同サイズタイルとの入れ替え
        var target = page.Tiles.FirstOrDefault(t =>
            t != drag &&
            t.Col == col && t.Row == row &&
            t.ColSpan == drag.ColSpan && t.RowSpan == drag.RowSpan);
        if (target != null)
        {
            (target.Col, target.Row) = (drag.Col, drag.Row);
            (drag.Col,   drag.Row)   = (col, row);
            Rebuild();
            return;
        }

        // 通常移動
        if (!CanPlace(col, row, drag.ColSpan, drag.RowSpan, drag)) return;
        drag.Col = col;
        drag.Row = row;
        Rebuild();
    }

    // ─── 競合チェック ─────────────────────────────────────────────────

    private bool CanPlace(int col, int row, int colSpan, int rowSpan, TileViewModel? exclude = null)
    {
        if (DataContext is not PageViewModel page) return false;
        var global = App.ConfigService.Current.Global;
        if (col < 0 || row < 0 ||
            col + colSpan > global.TileCountCols ||
            row + rowSpan > global.TileCountRows) return false;

        foreach (var tile in page.Tiles)
        {
            if (tile == exclude) continue;
            bool ox = col < tile.Col + tile.ColSpan && col + colSpan > tile.Col;
            bool oy = row < tile.Row + tile.RowSpan && row + rowSpan > tile.Row;
            if (ox && oy) return false;
        }
        return true;
    }

    // ─── ユーティリティ ───────────────────────────────────────────────

    private (int col, int row) PositionToCell(Point pt)
    {
        var layout = App.ConfigService.Current.Layout;
        var global = App.ConfigService.Current.Global;
        int step = layout.TileSize + layout.TileMargin;
        int col  = Math.Clamp((int)(pt.X / step), 0, global.TileCountCols - 1);
        int row  = Math.Clamp((int)(pt.Y / step), 0, global.TileCountRows - 1);
        return (col, row);
    }

    private void ShowPreview(int col, int row, int colSpan, int rowSpan, bool valid)
    {
        var layout = App.ConfigService.Current.Layout;
        int step = layout.TileSize + layout.TileMargin;
        Canvas.SetLeft(DragPreview, col * step);
        Canvas.SetTop(DragPreview, row * step);
        DragPreview.Width  = layout.TileSize * colSpan + layout.TileMargin * (colSpan - 1);
        DragPreview.Height = layout.TileSize * rowSpan + layout.TileMargin * (rowSpan - 1);
        DragPreview.Stroke = valid ? Brushes.White : Brushes.OrangeRed;
        DragPreview.Visibility = Visibility.Visible;
    }

    private void HidePreview() => DragPreview.Visibility = Visibility.Collapsed;
}
```

---

### 10. PageNameControl.xaml の変更

`TextBlock`（表示用）に加え、インライン編集用 `TextBox` を重ねて配置する。
通常時は TextBox を `Collapsed` にし、編集開始時に切り替える。

```xml
<UserControl x:Class="AppLauncher.Views.Controls.PageNameControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <!-- 通常表示：TextBlock -->
        <TextBlock x:Name="TextLabel"
                   Text="{Binding CurrentPage.Name}"
                   FontSize="{Binding PageNameFontSize}"
                   Foreground="#F0F0F0"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   TextTrimming="CharacterEllipsis"/>

        <!-- インライン編集：TextBox（編集モードでのクリック後に表示） -->
        <TextBox x:Name="EditTextBox"
                 Visibility="Collapsed"
                 FontSize="{Binding PageNameFontSize}"
                 Foreground="#F0F0F0"
                 Background="Transparent"
                 BorderThickness="0,0,0,1"
                 BorderBrush="#80F0F0F0"
                 HorizontalAlignment="Stretch"
                 VerticalAlignment="Center"
                 HorizontalContentAlignment="Center"
                 TextAlignment="Center"
                 CaretBrush="#F0F0F0"/>
    </Grid>
</UserControl>
```

---

### 11. PageNameControl.xaml.cs の変更

`LauncherViewModel.Mode` と `CurrentPageIndex` の変化を監視し、編集中テキストを適切なタイミングで確定する。

**確定タイミング**：
- 編集モードから他モードへ遷移したとき（`Mode` が `Edit` 以外へ変化）
- ページが切り替わったとき（`CurrentPageIndex` が変化）

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppLauncher.Models;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class PageNameControl : UserControl
{
    private LauncherViewModel? _vm;
    private bool _isEditing;

    public PageNameControl()
    {
        InitializeComponent();
        DataContextChanged         += OnDataContextChanged;
        TextLabel.MouseLeftButtonUp += OnTextLabelClicked;
        EditTextBox.KeyDown        += OnEditTextBoxKeyDown;
        EditTextBox.LostFocus      += (_, _) => CommitEdit();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = DataContext as LauncherViewModel;
        if (_vm != null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.Mode))
            HandleModeChange();
        else if (e.PropertyName is nameof(LauncherViewModel.CurrentPageIndex)
                                or nameof(LauncherViewModel.CurrentPage))
            CommitEdit();
    }

    private void HandleModeChange()
    {
        if (_vm?.Mode == AppMode.Edit)
        {
            TextLabel.Cursor = Cursors.IBeam;
        }
        else
        {
            CommitEdit();
            TextLabel.Cursor = Cursors.Arrow;
        }
    }

    private void OnTextLabelClicked(object sender, MouseButtonEventArgs e)
    {
        if (_vm?.Mode != AppMode.Edit) return;
        _isEditing = true;
        EditTextBox.Text = _vm.CurrentPage.Name;
        TextLabel.Visibility  = Visibility.Collapsed;
        EditTextBox.Visibility = Visibility.Visible;
        EditTextBox.Focus();
        EditTextBox.SelectAll();
    }

    private void OnEditTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) CommitEdit();
    }

    private void CommitEdit()
    {
        if (!_isEditing) return;
        if (_vm != null)
            _vm.CurrentPage.Name = EditTextBox.Text;
        _isEditing             = false;
        EditTextBox.Visibility = Visibility.Collapsed;
        TextLabel.Visibility   = Visibility.Visible;
    }
}
```

---

### 12. LauncherWindow.xaml の変更

Frame の内部 Grid に削除確認ダイアログオーバーレイを追加する。
`Grid.RowSpan="7"` で全行を覆い、`Panel.ZIndex="100"` で最前面に表示する。

追加位置：Frame の `<Grid>` 内、`<controls:BottomBarControl .../>` の後。

```xml
<!-- 削除確認ダイアログ（Frame 内コンテンツ全体に重なるオーバーレイ） -->
<Grid x:Name="DeleteConfirmOverlay"
      Grid.RowSpan="7"
      Visibility="Collapsed"
      Panel.ZIndex="100"
      Background="#80000000">
    <Border Width="300" Height="140"
            HorizontalAlignment="Center" VerticalAlignment="Center"
            Background="#FF8C9BAB" CornerRadius="10">
        <StackPanel VerticalAlignment="Center" Margin="20">
            <TextBlock Text="タイルを削除しますがよろしいですか？"
                       Foreground="White" TextWrapping="Wrap"
                       TextAlignment="Center" Margin="0,0,0,16"/>
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                <Border x:Name="DeleteOkButton"
                        Width="80" Height="32" Margin="0,0,8,0"
                        Background="#FFE05252" CornerRadius="6" Cursor="Hand">
                    <TextBlock Text="OK" Foreground="White"
                               HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <Border x:Name="DeleteCancelButton"
                        Width="80" Height="32"
                        Background="#FF6D7880" CornerRadius="6" Cursor="Hand">
                    <TextBlock Text="キャンセル" Foreground="White"
                               HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </StackPanel>
        </StackPanel>
    </Border>
</Grid>
```

変更後の Frame 内 Grid 全体：

```xml
<Border x:Name="Frame" Grid.Column="2" CornerRadius="20"
        BorderBrush="#FF000000" BorderThickness="2" ClipToBounds="True">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="8"/>    <!-- pageNameTopMargin -->
            <RowDefinition Height="Auto"/> <!-- PageNameControl -->
            <RowDefinition Height="8"/>    <!-- pageNameBottomMargin -->
            <RowDefinition Height="*"/>    <!-- TileGridControl -->
            <RowDefinition Height="8"/>    <!-- bottomBarTopMargin -->
            <RowDefinition Height="Auto"/> <!-- BottomBarControl -->
            <RowDefinition Height="8"/>    <!-- bottomBarBottomMargin -->
        </Grid.RowDefinitions>

        <controls:PageNameControl Grid.Row="1" Margin="20,0"/>
        <controls:TileGridControl Grid.Row="3" Margin="20,0"
                                  DataContext="{Binding CurrentPage}"/>
        <controls:BottomBarControl Grid.Row="5"/>

        <!-- 削除確認ダイアログ -->
        <Grid x:Name="DeleteConfirmOverlay"
              Grid.RowSpan="7"
              Visibility="Collapsed"
              Panel.ZIndex="100"
              Background="#80000000">
            <Border Width="300" Height="140"
                    HorizontalAlignment="Center" VerticalAlignment="Center"
                    Background="#FF8C9BAB" CornerRadius="10">
                <StackPanel VerticalAlignment="Center" Margin="20">
                    <TextBlock Text="タイルを削除しますがよろしいですか？"
                               Foreground="White" TextWrapping="Wrap"
                               TextAlignment="Center" Margin="0,0,0,16"/>
                    <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                        <Border x:Name="DeleteOkButton"
                                Width="80" Height="32" Margin="0,0,8,0"
                                Background="#FFE05252" CornerRadius="6" Cursor="Hand">
                            <TextBlock Text="OK" Foreground="White"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <Border x:Name="DeleteCancelButton"
                                Width="80" Height="32"
                                Background="#FF6D7880" CornerRadius="6" Cursor="Hand">
                            <TextBlock Text="キャンセル" Foreground="White"
                                       HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                    </StackPanel>
                </StackPanel>
            </Border>
        </Grid>
    </Grid>
</Border>
```

---

### 13. LauncherWindow.xaml.cs の変更

`SetViewModel` に `PendingDeleteTile` の購読とダイアログボタンの配線を追加する。
既存の `PropertyChanged` ラムダを拡張する形で記述する。

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AppLauncher.Models;
using AppLauncher.ViewModels;
using AppLauncher.Views.Controls;

namespace AppLauncher.Views;

public partial class LauncherWindow : Window
{
    public LauncherWindow() => InitializeComponent();

    public void SetViewModel(LauncherViewModel vm)
    {
        var layout = App.ConfigService.Current.Layout;

        RootGrid.ColumnDefinitions[0].Width = new GridLength(layout.HandleShortSide);
        RootGrid.ColumnDefinitions[1].Width = new GridLength(layout.HandleFrameMargin);
        Frame.CornerRadius = new CornerRadius(layout.FrameCornerRadius);

        DataContext = vm;
        ApplyPageColors(vm.CurrentPage);
        ApplyPinBorder(vm);

        // 削除確認ダイアログボタン配線
        DeleteOkButton.MouseLeftButtonUp     += (_, _) => vm.ConfirmDeleteTileCommand.Execute(null);
        DeleteCancelButton.MouseLeftButtonUp += (_, _) => vm.CancelDeleteTileCommand.Execute(null);

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LauncherViewModel.CurrentPage))
            {
                ApplyPageColors(vm.CurrentPage);
                ApplyPinBorder(vm);
            }
            if (e.PropertyName == nameof(LauncherViewModel.IsPinned))
                ApplyPinBorder(vm);
            if (e.PropertyName == nameof(LauncherViewModel.PendingDeleteTile))
                DeleteConfirmOverlay.Visibility = vm.PendingDeleteTile != null
                    ? Visibility.Visible : Visibility.Collapsed;
        };
    }

    private void ApplyPageColors(PageViewModel page)
    {
        var bgColor  = ColorPalette.GetColor(page.BackgroundColor);
        double bgAlpha = ColorPalette.OpacityToDouble(page.BackgroundOpacity);
        Frame.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * bgAlpha), bgColor.R, bgColor.G, bgColor.B));
        Handle.SetAppearance(page.HandleColor, page.HandleOpacity);
    }

    private void ApplyPinBorder(LauncherViewModel vm)
    {
        var layout = App.ConfigService.Current.Layout;
        if (!vm.IsPinned)
        {
            Frame.BorderBrush     = new SolidColorBrush(Colors.Black);
            Frame.BorderThickness = new Thickness(layout.FrameBorderThickness);
            Handle.ResetBorder();
            return;
        }
        var page  = vm.CurrentPage;
        var color = ColorPalette.GetColor(page.PinFrameColor);
        double alpha = ColorPalette.OpacityToDouble(page.PinFrameOpacity);
        int bt    = layout.PinBorderThickness;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(255 * alpha), color.R, color.G, color.B));
        Frame.BorderBrush     = brush;
        Frame.BorderThickness = new Thickness(bt);
        Handle.SetPinBorder(brush, bt);
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var vm = App.LauncherViewModel;
        if (vm == null || vm.Mode != AppMode.Normal) return;
        if (IsInteractiveTarget(e.OriginalSource)) return;
        vm.IsPinned = !vm.IsPinned;
        e.Handled = true;
    }

    private static bool IsInteractiveTarget(object source)
    {
        var dep = source as DependencyObject;
        while (dep != null)
        {
            if (dep is TileControl or Button or RepeatButton or BottomBarControl) return true;
            dep = VisualTreeHelper.GetParent(dep);
        }
        return false;
    }
}
```

---

## 完了条件

- [ ] 編集モードで空スロットにマウスオーバーすると半透明のハイライトが表示される
- [ ] 編集モードで空スロットをクリックするとデフォルト設定（青・透過率20%）のタイルが作成される
- [ ] 編集モードでタイルにマウスオーバーすると編集ボタン（✎）・削除ボタン（✕）・リサイズハンドルが表示される
- [ ] 削除ボタンをクリックすると削除確認ダイアログが Frame 中央に表示される
- [ ] 確認ダイアログで OK を押すとタイルが削除され、キャンセルを押すとダイアログが閉じる
- [ ] 編集モードでタイルをドラッグすると白色破線のプレビュー枠が追従する
- [ ] プレビュー枠が有効位置では白、無効位置（競合・グリッド外）ではオレンジ赤で表示される
- [ ] ドロップした位置が有効なら移動が確定される
- [ ] 同サイズのタイル上にドロップすると2つのタイルの位置が入れ替わる
- [ ] 異なるサイズのタイルと競合する位置へのドロップは無視される（移動キャンセル）
- [ ] 編集モードでリサイズハンドルをドラッグするとプレビュー枠でサイズ変更が確認できる
- [ ] マウスを離すと有効サイズの場合のみリサイズが確定される（1×1 ～ グリッド境界まで）
- [ ] 通常モードのタイルクリック起動・ホバーアニメーションは変わらない
- [ ] 編集モードでページ名称エリアをクリックすると TextBox が現れてテキスト編集できる
- [ ] TextBox 編集中にページを切り替えるか編集モードを終了すると入力内容でページ名が確定される
- [ ] Enter キーを押すとページ名編集が確定される
- [ ] 編集ボタン（✎）をクリックすると `AppMode.TileEdit` に遷移し `EditingTile` に対象タイルがセットされる（UIはPh.8で実装）
- [ ] `dotnet build` がエラーなく成功する

---

## 参照定義書

- `definition/function/アプリケーション機能定義.md § 2.3` — ページ名インライン編集の確定タイミング
- `definition/function/アプリケーション機能定義.md § 2.4` — タイル編集モードの遷移条件
- `definition/function/アプリケーション機能定義.md § 3.3` — タイル操作（新規作成・移動・リサイズ・削除・競合チェック・入れ替え）の詳細仕様
- `definition/ui/配置位置数値定義.md § 1` — タイルサイズ・マージン（セル座標計算の基準値）
