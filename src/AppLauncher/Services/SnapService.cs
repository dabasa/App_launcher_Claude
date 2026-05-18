using System.ComponentModel;
using System.Runtime.InteropServices;
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
    private double _dragStartLeft;
    private double _dragStartMouseY;
    private double _dragStartMouseX;
    private bool _isDragging;
    private const double DragThresholdPhysical = 5.0;

    // ─── 収納位置 ─────────────────────────────────────────────────────────
    private double _normalLeft;
    private double _storedLeft;

    // ─── タイマー・アニメーション ───────────────────────────────────────────
    private DispatcherTimer? _storageTimer;
    private bool _isAnimating;
    private int  _animGeneration;

    // ─── フレーム高さ ─────────────────────────────────────────────────────
    private double _baseFrameH;

    // ─── 吸着方向 ─────────────────────────────────────────────────────────
    private string _direction = "right";

    public void Attach(LauncherWindow window)
    {
        _window = window;
        _window.AddHandler(UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnMouseDown), handledEventsToo: true);
        _window.MouseMove += OnMouseMove;
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
    /// 設定に基づきウィンドウ列レイアウト・位置・サイズを再計算する。
    /// アニメーションを停止して通常位置（展開状態）に再配置する。
    /// </summary>
    public void ApplySnap()
    {
        StopAnimation(); // 実行中アニメーションを先に停止

        // 展開状態にリセット
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;

        var config  = App.ConfigService.Current;
        var layout  = config.Layout;
        var global  = config.Global;
        _direction  = global.SnapPosition;

        // 吸着方向に応じてハンドル・フレームの列を切り替え
        _window.ApplySnapLayout(_direction);

        var (frameW, frameH) = WindowSizeCalculator.Calculate(global, layout);
        var monitor = GetTargetMonitor(global.SnapMonitor);
        double screenLeft = monitor.Left;
        double screenTop  = monitor.Top;
        double screenW    = monitor.Width;
        double screenH    = monitor.Height;

        _window.Width = layout.HandleShortSide + layout.HandleFrameMargin + frameW;
        _baseFrameH   = frameH;
        UpdateWindowHeight();

        switch (_direction)
        {
            case "left":
                // 左吸着：フレームが左（col 0）、ハンドルが右（col 2）
                // 通常位置：フレーム左端 = 画面左端 + screenEdgeDistance
                _normalLeft = screenLeft + layout.ScreenEdgeDistance;
                // 収納位置：frameW + gap だけ左へスライド → ハンドルが screen 左端に残る
                _storedLeft = _normalLeft - frameW - layout.HandleFrameMargin;
                break;

            case "right":
            default:
                // 右吸着：ハンドルが左（col 0）、フレームが右（col 2）
                _normalLeft = screenLeft + screenW - layout.ScreenEdgeDistance - frameW
                              - layout.HandleFrameMargin - layout.HandleShortSide;
                _storedLeft = _normalLeft + frameW + layout.HandleFrameMargin;
                break;
        }

        _window.Left = _normalLeft;
        _window.Top  = screenTop + (screenH - _window.Height) / 2;
    }

    // ─── ドラッグ ──────────────────────────────────────────────────────────

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (App.LauncherViewModel?.IsStored == true) return;
        _dragStartTop    = _window.Top;
        _dragStartLeft   = _window.Left;
        var screenPos    = _window.PointToScreen(e.GetPosition(_window));
        _dragStartMouseY = screenPos.Y;
        _dragStartMouseX = screenPos.X;
        _isDragging      = false;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var screenPos   = _window.PointToScreen(e.GetPosition(_window));
        double currentX = screenPos.X;
        double currentY = screenPos.Y;

        if (_isDragging && e.LeftButton != MouseButtonState.Pressed)
        {
            _window.ReleaseMouseCapture();
            _isDragging = false;
            return;
        }

        if (!_isDragging)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            var captured = Mouse.Captured;
            if (captured != null && captured != _window) return;

            double dpiX   = GetDpiScaleX();
            double dpiY   = GetDpiScaleY();
            double deltaX = Math.Abs(currentX - _dragStartMouseX);
            double deltaY = Math.Abs(currentY - _dragStartMouseY);
            double delta  = _direction is "top" or "bottom" ? deltaX : deltaY;
            double dpi    = _direction is "top" or "bottom" ? dpiX : dpiY;
            if (delta < DragThresholdPhysical * dpi) return;

            _isDragging      = true;
            _window.CaptureMouse();
            _dragStartMouseY = currentY;
            _dragStartMouseX = currentX;
            _dragStartTop    = _window.Top;
            _dragStartLeft   = _window.Left;
            return;
        }

        if (Mouse.Captured != _window)
        {
            _window.CaptureMouse();
            _dragStartMouseY = currentY;
            _dragStartMouseX = currentX;
            _dragStartTop    = _window.Top;
            _dragStartLeft   = _window.Left;
            return;
        }

        if (_direction is "top" or "bottom")
        {
            double dpiX    = GetDpiScaleX();
            double newLeft = _dragStartLeft + (currentX - _dragStartMouseX) / dpiX;
            _window.Left   = Math.Clamp(newLeft, 0, SystemParameters.PrimaryScreenWidth - _window.Width);
        }
        else
        {
            double dpiY   = GetDpiScaleY();
            double newTop = _dragStartTop + (currentY - _dragStartMouseY) / dpiY;
            _window.Top   = Math.Clamp(newTop, 0, SystemParameters.PrimaryScreenHeight - _window.Height);
        }
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
        if (vm == null || vm.IsPinned || vm.IsStored || _isAnimating) return;
        if (vm.Mode is AppMode.Settings or AppMode.GlobalSettings) return;
        if (IsMouseInWindowOrGap()) return;
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
        if (vm.Mode is AppMode.Settings or AppMode.GlobalSettings) return;
        AnimateToStored();
    }

    public void ForceExpand(Action? callback = null)
    {
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;
        AnimateLeft(_window.Left, _normalLeft, () =>
        {
            if (!IsMouseInWindowOrGap())
                StartStorageTimer();
            callback?.Invoke();
        });
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    /// <summary>
    /// 吸着方向に応じてギャップ側を判定。
    /// 左吸着：ギャップはウィンドウ左側（負の X 領域）
    /// 右吸着：ギャップはウィンドウ右側（Width を超えた X 領域）
    /// </summary>
    private bool IsMouseInWindowOrGap()
    {
        GetCursorPos(out var screenPt);
        var pos  = _window.PointFromScreen(new Point(screenPt.X, screenPt.Y));
        double gap = App.ConfigService.Current.Layout.ScreenEdgeDistance;

        bool inY = pos.Y >= 0 && pos.Y <= _window.Height;
        bool inX = _direction switch
        {
            "left"  => pos.X >= -gap && pos.X <= _window.Width,
            _       => pos.X >= 0    && pos.X <= _window.Width + gap,
        };
        return inX && inY;
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
            if (!IsMouseInWindowOrGap())
                StartStorageTimer();
        });
    }

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
            if (_animGeneration != gen) return;
            _isAnimating = false;
            onComplete?.Invoke();
        };
        _window.BeginAnimation(Window.LeftProperty, anim);
    }

    private void StopAnimation()
    {
        _animGeneration++;
        _isAnimating = false;
        double pos = _window.Left;
        _window.BeginAnimation(Window.LeftProperty, null);
        _window.Left = pos;
    }

    // ─── マルチモニター ────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
        MonitorEnumProc lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor,
        ref RECT lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private record MonitorRect(double Left, double Top, double Width, double Height);

    public int GetMonitorCount()
    {
        int count = 0;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr _, IntPtr _, ref RECT _, IntPtr _) => { count++; return true; }, IntPtr.Zero);
        return Math.Max(1, count);
    }

    private MonitorRect GetTargetMonitor(int snapMonitor)
    {
        var monitors = new List<RECT>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr _, ref RECT rc, IntPtr _) =>
        {
            monitors.Add(rc);
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
            return new MonitorRect(0, 0,
                SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);

        int idx = Math.Clamp(snapMonitor - 1, 0, monitors.Count - 1);
        var rc  = monitors[idx];
        double dpiX = GetDpiScaleX();
        double dpiY = GetDpiScaleY();
        return new MonitorRect(
            rc.Left   / dpiX,
            rc.Top    / dpiY,
            (rc.Right  - rc.Left) / dpiX,
            (rc.Bottom - rc.Top)  / dpiY);
    }

    private double GetDpiScaleX()
    {
        var source = PresentationSource.FromVisual(_window);
        return source?.CompositionTarget.TransformToDevice.M11 ?? 1.0;
    }

    private double GetDpiScaleY()
    {
        var source = PresentationSource.FromVisual(_window);
        return source?.CompositionTarget.TransformToDevice.M22 ?? 1.0;
    }
}
