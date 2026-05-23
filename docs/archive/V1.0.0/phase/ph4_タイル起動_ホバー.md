# Ph.4 実装指示書 ─ タイル起動・ホバー

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.4 タイル起動・ホバー |
| 実装目的 | タイルクリックで app / url / folder を起動し、通常モードのみホバーアニメーション（底面基準 1.13 倍）を表示する |
| 未確定事項 | なし |
| 既存影響 | `SnapService.cs` のマウスキャプチャ方式を変更する（即時キャプチャ → しきい値後キャプチャ）。`TileControl.Apply()` の引数を追加する |
| 追加ライブラリ | なし |
| 実装範囲 | タイル起動（app / url / folder）・ホバーアニメーション・D&D 専用タイル表示。webview / system 起動・編集モードのタイル操作・ファイルドロップは対象外 |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `Services/TileLaunchService.cs` | 新規：タイル種別に応じた起動処理 |
| `Views/Controls/TileControl.xaml` | 変更：RenderTransform・D&D オーバーレイ追加 |
| `Views/Controls/TileControl.xaml.cs` | 変更：クリック起動・ホバーアニメーション・D&D オーバーレイ制御 |
| `Views/Controls/TileGridControl.xaml.cs` | 変更：`Apply()` にページ背景色引数を追加 |
| `Services/SnapService.cs` | 変更：ドラッグしきい値検出（即時キャプチャ → しきい値超過後にキャプチャ） |
| `App.xaml.cs` | 変更：`LauncherViewModel` を静的プロパティとして公開 |

---

## 実装仕様

### 1. TileLaunchService

`Services/TileLaunchService.cs` を新規作成する。  
`static` クラスとして実装し、外部依存なしで単体動作する。

#### 起動ロジック

| タイル種別 | 動作 |
|---|---|
| `app` | `tile.Args` に `{drop}` を含む場合は何もしない（D&D 専用）。それ以外は `ProcessStartInfo` で起動（`UseShellExecute = true`） |
| `url` | `ProcessStartInfo(tile.Path) { UseShellExecute = true }` でデフォルトブラウザを開く |
| `folder` | `ProcessStartInfo(expandedPath) { UseShellExecute = true }` でエクスプローラーを開く |
| `system` / `webview` / その他 | Ph.4 では何もしない |

**環境変数展開**：`tile.Path` および `tile.WorkDir` は `Environment.ExpandEnvironmentVariables()` で展開してから渡す。

**例外処理**：`Process.Start()` が失敗してもアプリがクラッシュしないよう `try/catch(Exception)` で握りつぶす（ログ不要）。

```csharp
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
```

---

### 2. SnapService の変更（ドラッグしきい値）

#### 問題

現行実装は `MouseLeftButtonDown` で即座に `CaptureMouse()` を呼ぶ。  
マウスキャプチャ後は後続のマウスイベントがすべてウィンドウへ直接届くため、  
タイルの `MouseLeftButtonUp` が発火せず、クリック起動が機能しない。

#### 解決策：しきい値後キャプチャ

- `MouseLeftButtonDown`：開始位置を記録するだけで `CaptureMouse()` を呼ばない
- `MouseMove`：Y 移動量が **5 物理ピクセル** を超えた時点で初めてキャプチャ＆ドラッグ開始
- `MouseLeftButtonUp`：タイルが `e.Handled = true` にしても受け取れるよう `handledEventsToo: true` で登録する

```csharp
private const double DragThresholdPhysical = 5.0;
private bool _isDragging;

public void Attach(LauncherWindow window)
{
    _window = window;
    _window.MouseLeftButtonDown += OnMouseDown;
    _window.MouseMove          += OnMouseMove;
    // handledEventsToo: タイルが Handled にしても OnMouseUp を確実に受け取る
    _window.AddHandler(UIElement.MouseLeftButtonUpEvent,
        new MouseButtonEventHandler(OnMouseUp), handledEventsToo: true);
}

private void OnMouseDown(object sender, MouseButtonEventArgs e)
{
    _dragStartTop    = _window.Top;
    _dragStartMouseY = _window.PointToScreen(e.GetPosition(_window)).Y;
    _isDragging      = false;
    // CaptureMouse はしきい値超過後に行う
}

private void OnMouseMove(object sender, MouseEventArgs e)
{
    double currentY = _window.PointToScreen(e.GetPosition(_window)).Y;

    if (!_isDragging)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        double deltaPhysical = Math.Abs(currentY - _dragStartMouseY);
        if (deltaPhysical < DragThresholdPhysical * GetDpiScaleY()) return;

        // しきい値超過 → ドラッグ開始
        _isDragging      = true;
        _window.CaptureMouse();
        // キャプチャ直後の位置を基点に再設定してガタつきを防ぐ
        _dragStartMouseY = currentY;
        _dragStartTop    = _window.Top;
        return;
    }

    // ドラッグ中：縦移動のみ
    double dpiScale = GetDpiScaleY();
    double newTop   = _dragStartTop + (currentY - _dragStartMouseY) / dpiScale;
    double screenH  = SystemParameters.PrimaryScreenHeight;
    _window.Top     = Math.Clamp(newTop, 0, screenH - _window.Height);
}

private void OnMouseUp(object sender, MouseButtonEventArgs e)
{
    if (_isDragging) _window.ReleaseMouseCapture();
    _isDragging = false;
}
```

---

### 3. App.xaml.cs の変更

`LauncherViewModel` を静的プロパティとして公開し、`TileControl` のイベントハンドラで現在モードを参照できるようにする。

```csharp
public static LauncherViewModel? LauncherViewModel { get; private set; }

protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    ConfigService.Load();

    var vm = new LauncherViewModel(ConfigService.Current);
    LauncherViewModel = vm;        // ← 追加
    var window    = new LauncherWindow();
    var snapService = new SnapService();
    // 以下は既存のまま
    ...
}
```

---

### 4. TileGridControl の変更

`Rebuild()` 内の `TileControl.Apply()` 呼び出しに、D&D オーバーレイ用のページ背景色を追加する。

```csharp
// Rebuild() 内の既存コード（変更箇所のみ抜粋）
var bgColor = ColorPalette.GetColor(page.BackgroundColor); // ← 既存の slotBrush 生成と兼用

foreach (var tile in page.Tiles)
{
    var control = new TileControl();
    control.Apply(tile, cornerRadius, bgColor);   // ← 第3引数を追加
    ...
}
```

---

### 5. TileControl の変更

#### 5.1 XAML（`TileControl.xaml`）

変更点：
- `UserControl` に `RenderTransformOrigin="0.5,1.0"` と `ScaleTransform` を追加（**底面基準**スケール）
- `TileBorder` の子要素を `Grid` に変更し、`TitleText` と `DndOverlay` を重ねる

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
                           FontSize="21.333"
                           Foreground="#F0F0F0"/>
            </Border>
        </Grid>
    </Border>
</UserControl>
```

#### 5.2 コードビハインド（`TileControl.xaml.cs`）

変更点：
- `_tile` / `_pageBackgroundColor` フィールド追加
- `Apply()` に `Color pageBackgroundColor` 引数を追加
- クリック（`OnMouseLeftButtonDown` / `OnMouseLeftButtonUp`）実装
- ホバー（`OnMouseEnter` / `OnMouseLeave`）実装

```csharp
public partial class TileControl : UserControl
{
    private TileViewModel? _tile;
    private Color _pageBackgroundColor;
    private Point _mouseDownPos;

    public TileControl() => InitializeComponent();

    public void Apply(TileViewModel tile, int cornerRadius, Color pageBackgroundColor)
    {
        _tile = tile;
        _pageBackgroundColor = pageBackgroundColor;

        var bgColor = ColorPalette.GetColor(tile.Color);
        double alpha = ColorPalette.OpacityToDouble(tile.Opacity);
        TileBorder.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), bgColor.R, bgColor.G, bgColor.B));
        TileBorder.CornerRadius = new CornerRadius(cornerRadius);

        TitleText.Text = tile.Title;
        TitleText.FontSize = tile.FontSizePt * 4.0 / 3.0;
        TitleText.Foreground = new SolidColorBrush(ColorPalette.GetColor(tile.FontColor));
        if (!string.IsNullOrEmpty(tile.FontName))
            TitleText.FontFamily = new FontFamily(tile.FontName);
    }

    // ─── クリック ─────────────────────────────────────────────────
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _mouseDownPos = e.GetPosition(this);
        // e.Handled = false のまま → SnapService がドラッグ開始を検知できるようにする
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_tile == null) return;
        if (App.LauncherViewModel?.Mode != AppMode.Normal) return;

        double dist = (e.GetPosition(this) - _mouseDownPos).Length;
        if (dist < 5.0)
        {
            e.Handled = true; // ドラッグではなくクリックと判定
            TileLaunchService.Launch(_tile);
        }
    }

    // ─── ホバー ──────────────────────────────────────────────────
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (_tile == null) return;
        if (App.LauncherViewModel?.Mode != AppMode.Normal) return;

        if (_tile.Args.Contains("{drop}"))
        {
            // D&D 専用タイル：オーバーレイを表示（アニメーションなし）
            DndOverlay.Background = new SolidColorBrush(_pageBackgroundColor);
            DndOverlay.Visibility = Visibility.Visible;
            return;
        }

        // 通常タイル：底面基準 1.13 倍へアニメーション
        Panel.SetZIndex(this, 100);
        AnimateScale(1.13);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        DndOverlay.Visibility = Visibility.Collapsed;
        Panel.SetZIndex(this, 0);
        AnimateScale(1.0);
    }

    private void AnimateScale(double to)
    {
        var duration = TimeSpan.FromMilliseconds(120);
        HoverScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(to, duration));
        HoverScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(to, duration));
    }
}
```

必要な using：

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AppLauncher.Models;
using AppLauncher.Services;
using AppLauncher.ViewModels;
```

---

## 完了条件

- [ ] `app` タイル（`notepad.exe` / `calc.exe`）をクリックするとアプリが起動する
- [ ] `url` タイル（Google）をクリックするとデフォルトブラウザで URL が開く
- [ ] `folder` タイル（Desktop / Documents）をクリックするとエクスプローラーが開く
- [ ] `{drop}` を含む `app` タイル（D&D開く）をクリックしても何も起動しない
- [ ] 通常モードでタイルにマウスオーバーすると底面を基点に 1.13 倍へアニメーションする
- [ ] ホバー拡大時、隣接タイルに視覚的に重なって表示される（隣接タイルは移動しない）
- [ ] D&D 専用タイルにホバーすると「D&D専用」テキストが中央に表示され、拡大アニメーションは発生しない
- [ ] タイルをクリックせずにランチャーをドラッグで縦移動できる（しきい値 5px でドラッグ判定）
- [ ] 起動後もランチャーが最前面に残る（`Topmost="True"` 維持）
- [ ] `dotnet build` がエラーなく成功する

---

## 参照定義書

- `definition/function/アプリケーション機能定義.md § 3.2` — タイル仕様（ホバーアニメーション・1.13 倍・底面基準）
- `definition/function/アプリケーション機能定義.md § 3.4` — タイル種別クリック動作（app / url / folder）
- `definition/function/アプリケーション機能定義.md § 3.5` — D&D 構文（`{drop}`）の動作仕様
- `definition/ui/配置位置数値定義.md § 1` — ホバー時拡大率 1.13・タイルサイズ
- `definition/ui/配置位置数値定義.md § 10` — D&D 専用テキストの数値（フォントサイズ・角丸・背景色）
