using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using AppLauncher.Helpers;
using AppLauncher.Models;
using AppLauncher.ViewModels;
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

    // ─── タイマー・アニメーション ───────────────────────────────────────────
    private DispatcherTimer? _storageTimer;
    private bool _isAnimating;
    private int  _animGeneration; // Completed コールバックのキャンセル用世代カウンタ

    // ─── フレーム高さ ─────────────────────────────────────────────────────
    private double _baseFrameH; // 通常モード時のフレーム高さ

    public void Attach(LauncherWindow window)
    {
        _window = window;
        _window.MouseLeftButtonDown += OnMouseDown;
        _window.MouseMove           += OnMouseMove;
        // handledEventsToo: タイルが Handled にしても受け取る
        _window.AddHandler(UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnMouseUp), handledEventsToo: true);
        _window.MouseEnter += OnWindowMouseEnter;
        _window.MouseLeave += OnWindowMouseLeave;

        App.LauncherViewModel?.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.Mode))
            UpdateWindowHeight();
    }

    private void UpdateWindowHeight()
    {
        bool isExpanded = App.LauncherViewModel?.Mode is AppMode.Edit or AppMode.TileEdit;
        _window.Height = _baseFrameH + (isExpanded ? 33 : 0);
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

        _window.Width = windowW;
        _baseFrameH   = frameH;
        UpdateWindowHeight();

        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        // 通常位置：フレーム右端 = 画面右端 - screenEdgeDistance
        _normalLeft = screenW - layout.ScreenEdgeDistance - frameW
                      - layout.HandleFrameMargin - layout.HandleShortSide;

        // 収納位置：フレーム全体が画面端外へ退避し、ハンドルのみ残る
        _storedLeft = _normalLeft + frameW + layout.HandleFrameMargin;

        _window.Left = _normalLeft;
        _window.Top  = (screenH - _window.Height) / 2;
    }

    // ─── ドラッグ（縦方向のみ） ────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
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
            // 別の要素（TileGrid リサイズ等）がキャプチャ中はウィンドウドラッグを開始しない
            var captured = Mouse.Captured;
            if (captured != null && captured != _window) return;

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
        // 完全収納済みのときのみ展開（アニメーション途中は無視）
        if (vm?.IsStored == true)
            AnimateToNormal();
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e)
    {
        var vm = App.LauncherViewModel;
        // アニメーション中・ピン中・収納済みのときはタイマーを起動しない
        if (vm == null || vm.IsPinned || vm.IsStored || _isAnimating) return;
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
        if (IsMouseInWindowOrGap()) return;
        var vm = App.LauncherViewModel;
        if (vm == null || vm.IsPinned || vm.IsStored) return;
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
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;
        AnimateLeft(_window.Left, _normalLeft, () =>
        {
            // 展開完了後にマウスがウィンドウ外にいれば収納タイマーを再開
            if (!IsMouseInWindowOrGap())
                StartStorageTimer();
        });
    }

    /// <summary>
    /// Window.LeftProperty を WPF DoubleAnimation で 1 秒かけてアニメーションする。
    /// _animGeneration で Completed コールバックの二重呼び出しを防ぐ。
    /// </summary>
    private void AnimateLeft(double from, double to, Action? onComplete)
    {
        StopAnimation();
        _isAnimating = true;
        int gen = ++_animGeneration;

        int dur = App.ConfigService.Current.Global.AnimationDurationMs;
        var anim = new DoubleAnimation(from, to, new Duration(TimeSpan.FromMilliseconds(dur == 0 ? 1 : dur)))
        {
            FillBehavior = FillBehavior.HoldEnd,
        };
        anim.Completed += (_, _) =>
        {
            // 後から別アニメーションで上書きされた場合は無視
            if (_animGeneration != gen) return;
            _isAnimating = false;
            onComplete?.Invoke();
        };
        _window.BeginAnimation(Window.LeftProperty, anim);
    }

    /// <summary>
    /// 実行中のアニメーションを停止し、現在位置をローカル値として固定する。
    /// </summary>
    private void StopAnimation()
    {
        _animGeneration++; // 実行中の Completed コールバックを無効化
        _isAnimating = false;
        double pos = _window.Left; // HoldEnd 中はアニメーション値が返る
        _window.BeginAnimation(Window.LeftProperty, null); // アニメーション解除
        _window.Left = pos; // ローカル値として固定
    }

    private double GetDpiScaleY()
    {
        var source = PresentationSource.FromVisual(_window);
        return source?.CompositionTarget.TransformToDevice.M22 ?? 1.0;
    }
}
