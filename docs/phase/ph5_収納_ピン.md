# Ph.5 実装指示書 ─ 収納モード・ピンモード

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.5 収納モード・ピンモード |
| 実装目的 | マウス離脱後の自動収納アニメーション・ハンドルホバーによる展開・ピン固定・ピンモード枠の表示 |
| 未確定事項 | なし |
| 既存影響 | `SnapService.cs` に収納ロジックを追加。`LauncherViewModel` に 2 プロパティ追加。`LauncherWindow` にピン枠 Border と ダブルクリック処理を追加 |
| 追加ライブラリ | なし |
| 実装範囲 | 右端吸着時の収納・展開・ピンモード。タスクバー下端吸着時の特殊処理・多方向吸着は対象外（Ph.10） |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `ViewModels/LauncherViewModel.cs` | 変更：`IsPinned` / `IsStored` プロパティ追加 |
| `Services/SnapService.cs` | 変更：収納タイマー・アニメーション・隙間チェック・格納位置計算 |
| `Views/LauncherWindow.xaml` | 変更：ピンモード枠 Border 追加 |
| `Views/LauncherWindow.xaml.cs` | 変更：ダブルクリック処理・ピン枠色更新 |

---

## 実装仕様

### 1. LauncherViewModel の変更

`IsPinned` と `IsStored` を追加する。どちらも `[ObservableProperty]` で実装する。

```csharp
[ObservableProperty]
private bool _isPinned;

[ObservableProperty]
private bool _isStored;
```

---

### 2. SnapService の変更（全体）

Ph.5 で SnapService に大きな変更が入るため、ファイル全体を以下で置き換える。

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AppLauncher.Helpers;
using AppLauncher.Views;

namespace AppLauncher.Services;

public class SnapService
{
    private LauncherWindow _window = null!;

    // ─── ドラッグ ─────────────────────────────────────────────────────────
    private double _dragStartTop;
    private double _dragStartMouseY; // 物理ピクセル
    private bool _isDragging;
    private const double DragThresholdPhysical = 5.0;

    // ─── 収納位置 ─────────────────────────────────────────────────────────
    private double _normalLeft;  // 通常表示時の Left
    private double _storedLeft;  // 収納時の Left（フレームが画面端外へ）

    // ─── タイマー ─────────────────────────────────────────────────────────
    private DispatcherTimer? _storageTimer;
    private DispatcherTimer? _animTimer;

    public void Attach(LauncherWindow window)
    {
        _window = window;
        _window.MouseLeftButtonDown += OnMouseDown;
        _window.MouseMove          += OnMouseMove;
        // handledEventsToo: タイルが Handled にしても受け取る
        _window.AddHandler(UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnMouseUp), handledEventsToo: true);
        _window.MouseEnter += OnWindowMouseEnter;
        _window.MouseLeave += OnWindowMouseLeave;
    }

    /// <summary>
    /// 吸着位置を算出してウィンドウを配置する。右端吸着のみ対応（Ph.10 で全方向対応）。
    /// </summary>
    public void ApplySnap()
    {
        var config = App.ConfigService.Current;
        var layout = config.Layout;
        var global = config.Global;

        var (frameW, frameH) = WindowSizeCalculator.Calculate(global, layout);
        double windowW = layout.HandleShortSide + layout.HandleFrameMargin + frameW;
        double windowH = frameH;

        _window.Width  = windowW;
        _window.Height = windowH;

        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        // 通常位置：フレーム右端 = 画面右端 - screenEdgeDistance
        _normalLeft = screenW - layout.ScreenEdgeDistance - frameW
                      - layout.HandleFrameMargin - layout.HandleShortSide;

        // 収納位置：フレーム全体が画面端外へ退避し、ハンドルのみ残る
        // 移動量 = frameW + handleFrameMargin
        _storedLeft = _normalLeft + frameW + layout.HandleFrameMargin;

        _window.Left = _normalLeft;
        _window.Top  = (screenH - windowH) / 2;
    }

    // ─── ドラッグ（縦方向のみ） ────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        // 収納中はドラッグ不可
        if (App.LauncherViewModel?.IsStored == true) return;
        _dragStartTop    = _window.Top;
        _dragStartMouseY = _window.PointToScreen(e.GetPosition(_window)).Y;
        _isDragging      = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        double currentY = _window.PointToScreen(e.GetPosition(_window)).Y;

        if (!_isDragging)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            double deltaPhysical = Math.Abs(currentY - _dragStartMouseY);
            if (deltaPhysical < DragThresholdPhysical * GetDpiScaleY()) return;

            _isDragging      = true;
            _window.CaptureMouse();
            _dragStartMouseY = currentY;
            _dragStartTop    = _window.Top;
            return;
        }

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

    // ─── 収納 / 展開 ──────────────────────────────────────────────────────

    private void OnWindowMouseEnter(object sender, MouseEventArgs e)
    {
        CancelStorageTimer();
        var vm = App.LauncherViewModel;
        if (vm?.IsStored == true)
            AnimateToNormal();
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        var vm = App.LauncherViewModel;
        if (vm == null || vm.IsPinned || vm.IsStored) return;
        StartStorageTimer();
    }

    private void StartStorageTimer()
    {
        CancelStorageTimer();
        int delay = App.ConfigService.Current.Global.StorageDelayMs;
        _storageTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(delay == 0 ? 1 : delay)
        };
        _storageTimer.Tick += StorageTimerTick;
        _storageTimer.Start();
    }

    private void CancelStorageTimer()
    {
        _storageTimer?.Stop();
        _storageTimer = null;
    }

    private void StorageTimerTick(object? sender, EventArgs e)
    {
        CancelStorageTimer();
        // 隙間にカーソルがある場合は収納しない
        if (IsMouseInWindowOrGap()) return;
        var vm = App.LauncherViewModel;
        if (vm == null || vm.IsPinned) return;
        AnimateToStored();
    }

    /// <summary>
    /// マウスがウィンドウ内または画面端との隙間（screenEdgeDistance 幅）にいるか判定する。
    /// 右端吸着時、隙間はウィンドウの右側にある。
    /// </summary>
    private bool IsMouseInWindowOrGap()
    {
        var pos   = Mouse.GetPosition(_window); // ウィンドウ相対の論理ピクセル
        double gapW = App.ConfigService.Current.Layout.ScreenEdgeDistance;
        return pos.X >= 0 && pos.X <= _window.Width + gapW
               && pos.Y >= 0 && pos.Y <= _window.Height;
    }

    private void AnimateToStored()
    {
        AnimateLeft(_window.Left, _storedLeft, () =>
        {
            if (App.LauncherViewModel != null)
                App.LauncherViewModel.IsStored = true;
        });
    }

    private void AnimateToNormal()
    {
        // IsStored を先に false にしてタイル・ホバー等を即時有効化
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;
        AnimateLeft(_window.Left, _normalLeft, null);
    }

    /// <summary>
    /// ウィンドウ Left プロパティを from → to まで 1 秒でアニメーションする。
    /// 16 ms 間隔の DispatcherTimer で線形補間。
    /// </summary>
    private void AnimateLeft(double from, double to, Action? onComplete)
    {
        _animTimer?.Stop();
        long startTick   = Environment.TickCount64;
        const double dur = 1000.0; // ms

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += (_, _) =>
        {
            double t = Math.Clamp((Environment.TickCount64 - startTick) / dur, 0.0, 1.0);
            _window.Left = from + (to - from) * t;
            if (t >= 1.0)
            {
                _animTimer?.Stop();
                _animTimer = null;
                onComplete?.Invoke();
            }
        };
        _animTimer.Start();
    }

    private double GetDpiScaleY()
    {
        var source = PresentationSource.FromVisual(_window);
        return source?.CompositionTarget.TransformToDevice.M22 ?? 1.0;
    }
}
```

#### 収納位置の算出（右端吸着）

```
_normalLeft = screenW - screenEdgeDistance - frameW - handleFrameMargin - handleShortSide
_storedLeft = _normalLeft + frameW + handleFrameMargin
```

収納後は `handle.RightEdge = normalLeft + frameW + handleFrameMargin + handleShortSide`
= `_normalLeft + frameW + handleFrameMargin + handleShortSide`（旧フレーム右端位置）になり、ハンドルのみが画面内に残る。

---

### 3. LauncherWindow.xaml の変更

ルート `Grid` の **最初の子** として `PinFrame` Border を追加する。`Grid.ColumnSpan="3"` で全列にかかるようにし、他要素より先に描画（最背面）する。

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="15"/>
        <ColumnDefinition Width="8"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>

    <!-- ピンモード枠（最背面。コードビハインドで色・太さを設定） -->
    <Border x:Name="PinFrame"
            Grid.ColumnSpan="3"
            Visibility="Collapsed"/>

    <!-- ハンドル -->
    <controls:HandleControl x:Name="Handle"
                            Grid.Column="0"
                            VerticalAlignment="Center"/>

    <!-- ランチャーフレーム -->
    <Border x:Name="Frame" ...（既存のまま）... />
</Grid>
```

---

### 4. LauncherWindow.xaml.cs の変更

変更点：
- `SetViewModel()` の PropertyChanged 購読に `IsPinned` を追加し、ピン枠を更新する
- `ApplyPinFrame()` メソッドを追加
- ダブルクリックでピンモードを切り替えるハンドラを追加

```csharp
using System.Windows;
using System.Windows.Controls;
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
        var bt = App.ConfigService.Current.Layout.FrameBorderThickness;
        Frame.BorderThickness = new Thickness(bt);
        DataContext = vm;
        ApplyPageColors(vm.CurrentPage);
        ApplyPinFrame(vm);

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LauncherViewModel.CurrentPage))
            {
                ApplyPageColors(vm.CurrentPage);
                ApplyPinFrame(vm);
            }
            if (e.PropertyName == nameof(LauncherViewModel.IsPinned))
                ApplyPinFrame(vm);
        };
    }

    private void ApplyPageColors(PageViewModel page)
    {
        var bgColor = ColorPalette.GetColor(page.BackgroundColor);
        double bgAlpha = ColorPalette.OpacityToDouble(page.BackgroundOpacity);
        Frame.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * bgAlpha), bgColor.R, bgColor.G, bgColor.B));

        Handle.SetAppearance(page.HandleColor, page.HandleOpacity);
    }

    private void ApplyPinFrame(LauncherViewModel vm)
    {
        if (!vm.IsPinned)
        {
            PinFrame.Visibility = Visibility.Collapsed;
            return;
        }

        var page   = vm.CurrentPage;
        var layout = App.ConfigService.Current.Layout;
        var color  = ColorPalette.GetColor(page.PinFrameColor);
        double alpha = ColorPalette.OpacityToDouble(page.PinFrameOpacity);

        PinFrame.BorderBrush = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), color.R, color.G, color.B));

        // 右端吸着：ハンドルは左側 → 左辺が WithHandle 幅、他は NoHandle 幅
        PinFrame.BorderThickness = new Thickness(
            layout.PinFrameWidthWithHandle,  // 左（ハンドルあり側）
            layout.PinFrameWidthNoHandle,    // 上
            layout.PinFrameWidthNoHandle,    // 右
            layout.PinFrameWidthNoHandle);   // 下

        PinFrame.Visibility = Visibility.Visible;
    }

    // ─── ピンモードのダブルクリック切り替え ───────────────────────────────

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var vm = App.LauncherViewModel;
        if (vm == null || vm.Mode != AppMode.Normal) return;
        if (IsInteractiveTarget(e.OriginalSource)) return;

        vm.IsPinned = !vm.IsPinned;
        e.Handled = true;
    }

    /// <summary>
    /// ダブルクリック対象がタイルまたはボタン類か判定する。
    /// タイル・ボタン以外の領域のダブルクリックのみピンモード切り替えに使う。
    /// </summary>
    private static bool IsInteractiveTarget(object source)
    {
        var dep = source as DependencyObject;
        while (dep != null)
        {
            if (dep is TileControl or Button or RepeatButton) return true;
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        }
        return false;
    }
}
```

必要な using：

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using AppLauncher.Models;
using AppLauncher.ViewModels;
using AppLauncher.Views.Controls;
```

---

## 完了条件

- [ ] ランチャーからマウスが離れると `storageDelayMs`（デフォルト 1000 ms）後にフレームが画面端へ収納される
- [ ] `storageDelayMs = 0` のとき、マウスが離れた瞬間に即座に収納アニメーションが始まる
- [ ] ハンドルにマウスオーバーすると 1 秒で展開アニメーションが再生され通常表示へ戻る
- [ ] 収納・展開アニメーションがそれぞれ 1 秒で完了する
- [ ] 収納中はタイルクリック・ホバーが発生しない（`IsStored = true` の間、収納状態）
- [ ] 収納中にウィンドウをドラッグできない
- [ ] ランチャーと画面端の間の 20 px 隙間にマウスがあっても収納されない
- [ ] 空エリアをダブルクリックするとピンモードが ON になりフレーム外周に枠が表示される
- [ ] ピンモード ON 中はマウスが離れても収納されない
- [ ] ピンモード ON 中に再び空エリアをダブルクリックするとピンモードが OFF になり枠が消える
- [ ] タイル・ボタン上のダブルクリックはピンモードを切り替えない
- [ ] ページを切り替えるとピン枠色が新しいページの `PinFrameColor` / `PinFrameOpacity` に更新される
- [ ] `dotnet build` がエラーなく成功する

---

## 参照定義書

- `definition/function/アプリケーション機能定義.md § 2.2` — ピンモード遷移条件・ドラッグ可否
- `definition/function/アプリケーション機能定義.md § 2.7` — 収納モード遷移条件・動作仕様・アニメーション時間
- `definition/ui/配置位置数値定義.md § 7` — ピンモード枠幅（ハンドルあり側 23 px・なし側 20 px）
