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

    // ─── 収納位置（左右吸着用） ───────────────────────────────────────────
    private double _normalLeft;
    private double _storedLeft;

    // ─── 収納位置（上下吸着用） ───────────────────────────────────────────
    private double _normalTop;
    private double _storedTop;

    // ─── タイマー・アニメーション ───────────────────────────────────────────
    private DispatcherTimer? _storageTimer;
    private bool _isAnimating;
    private int  _animGeneration;

    // ─── フレーム高さ ─────────────────────────────────────────────────────
    private double _baseFrameH;

    // ─── 吸着方向 ─────────────────────────────────────────────────────────
    private string _direction = "right";

    private bool IsVerticalSnap => _direction is "top" or "bottom";

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
        {
            UpdateWindowHeight();
            CheckStorageAfterModeChange();
        }
        if (e.PropertyName == nameof(LauncherViewModel.IsPinned))
            OnIsPinnedChanged();
    }

    // 収納禁止モードを抜けた際、マウスが既にウィンドウ外にいれば収納タイマーを起動する。
    // MouseLeave はウィンドウが移動したとき・モード変更時には再発火しないため手動チェックが必要。
    private void CheckStorageAfterModeChange()
    {
        _window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            var vm = App.LauncherViewModel;
            if (vm == null || vm.IsPinned || vm.IsStored || _isAnimating) return;
            if (vm.Mode is AppMode.Settings or AppMode.GlobalSettings or AppMode.PageEdit) return;
            if (!IsMouseInWindowOrGap())
                StartStorageTimer();
        });
    }

    private void OnIsPinnedChanged()
    {
        var vm = App.LauncherViewModel;
        if (vm == null) return;
        if (vm.IsPinned && vm.IsStored)
            ForceExpand();
    }

    private void UpdateWindowHeight()
    {
        var layout = App.ConfigService.Current.Layout;
        bool isExpanded = App.LauncherViewModel?.Mode is AppMode.Edit or AppMode.TileEdit or AppMode.PageEdit;
        double baseH = _baseFrameH + (isExpanded ? 33 : 0);

        if (IsVerticalSnap)
            _window.Height = baseH + layout.HandleFrameMargin + layout.HandleShortSide;
        else
            _window.Height = baseH;
    }

    /// <summary>
    /// 設定に基づきウィンドウ列レイアウト・位置・サイズを再計算する。
    /// アニメーションを停止して通常位置（展開状態）に再配置する。
    /// </summary>
    public void ApplySnap(bool preserveOrthogonal = false)
    {
        double savedTop  = _window.Top;
        double savedLeft = _window.Left;
        double? configSavedTop  = App.ConfigService.Current.Global.SavedWindowTop;
        double? configSavedLeft = App.ConfigService.Current.Global.SavedWindowLeft;

        StopAnimation(); // 実行中アニメーションを先に停止

        // 展開状態にリセット
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;

        var config  = App.ConfigService.Current;
        var layout  = config.Layout;
        var global  = config.Global;
        _direction  = global.SnapPosition;

        // 吸着方向に応じてハンドル・フレームの列/行を切り替え
        _window.ApplySnapLayout(_direction);

        var (frameW, frameH) = WindowSizeCalculator.Calculate(global, layout);
        var monitor = GetTargetMonitor(global.SnapMonitor);
        double screenLeft   = monitor.Left;
        double screenTop    = monitor.Top;
        double screenW      = monitor.Width;
        double screenH      = monitor.Height;
        double screenBottom = screenTop + screenH;
        double screenRight  = screenLeft + screenW;

        _baseFrameH = frameH;

        switch (_direction)
        {
            case "top":
                _window.Width = frameW;
                UpdateWindowHeight();
                _normalTop = screenTop + layout.ScreenEdgeDistance;
                _storedTop = screenTop - frameH - layout.HandleFrameMargin;
                _window.Top  = _normalTop;
                _window.Left = preserveOrthogonal
                    ? Math.Clamp(savedLeft, screenLeft, screenRight - _window.Width)
                    : configSavedLeft.HasValue
                        ? Math.Clamp(configSavedLeft.Value, screenLeft, screenRight - _window.Width)
                        : screenLeft + (screenW - _window.Width) / 2;
                break;

            case "bottom":
                _window.Width = frameW;
                UpdateWindowHeight();
                _normalTop = monitor.WorkBottom - layout.ScreenEdgeDistance - frameH
                             - layout.HandleFrameMargin - layout.HandleShortSide;
                _storedTop = monitor.WorkBottom - layout.HandleShortSide;
                _window.Top  = _normalTop;
                _window.Left = preserveOrthogonal
                    ? Math.Clamp(savedLeft, screenLeft, screenRight - _window.Width)
                    : configSavedLeft.HasValue
                        ? Math.Clamp(configSavedLeft.Value, screenLeft, screenRight - _window.Width)
                        : screenLeft + (screenW - _window.Width) / 2;
                break;

            case "left":
                _window.Width = layout.HandleShortSide + layout.HandleFrameMargin + frameW;
                UpdateWindowHeight();
                _normalLeft = screenLeft + layout.ScreenEdgeDistance;
                // Stored: handle (col2) at screen left edge → window.Left = screenLeft - frameW - gap
                _storedLeft = screenLeft - frameW - layout.HandleFrameMargin;
                _window.Left = _normalLeft;
                _window.Top  = preserveOrthogonal
                    ? Math.Clamp(savedTop, screenTop, screenBottom - _window.Height)
                    : configSavedTop.HasValue
                        ? Math.Clamp(configSavedTop.Value, screenTop, screenBottom - _window.Height)
                        : screenTop + (screenH - _window.Height) / 2;
                break;

            case "right":
            default:
                _window.Width = layout.HandleShortSide + layout.HandleFrameMargin + frameW;
                UpdateWindowHeight();
                _normalLeft = screenRight - layout.ScreenEdgeDistance - frameW
                              - layout.HandleFrameMargin - layout.HandleShortSide;
                // Stored: handle (col0) at screen right edge → window.Left = screenRight - handleW
                _storedLeft = screenRight - layout.HandleShortSide;
                _window.Left = _normalLeft;
                _window.Top  = preserveOrthogonal
                    ? Math.Clamp(savedTop, screenTop, screenBottom - _window.Height)
                    : configSavedTop.HasValue
                        ? Math.Clamp(configSavedTop.Value, screenTop, screenBottom - _window.Height)
                        : screenTop + (screenH - _window.Height) / 2;
                break;
        }

        // ウィンドウ移動後、マウスが窓外ならLeaveイベントが来ないので収納タイマーを手動起動
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var vm = App.LauncherViewModel;
            if (vm == null || vm.IsPinned || vm.IsStored || _isAnimating) return;
            if (vm.Mode is AppMode.Settings or AppMode.GlobalSettings or AppMode.PageEdit) return;
            if (!IsMouseInWindowOrGap())
                StartStorageTimer();
        });
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
            double delta  = IsVerticalSnap ? deltaX : deltaY;
            double dpi    = IsVerticalSnap ? dpiX   : dpiY;
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

        if (IsVerticalSnap)
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
        bool wasDragging = _isDragging;
        if (_isDragging) _window.ReleaseMouseCapture();
        _isDragging = false;
        if (wasDragging) SavePosition();
    }

    private void SavePosition()
    {
        var global = App.ConfigService.Current.Global;
        if (IsVerticalSnap)
            global.SavedWindowLeft = _window.Left;
        else
            global.SavedWindowTop = _window.Top;
        App.ConfigService.Save();
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
        if (vm.Mode is AppMode.Settings or AppMode.GlobalSettings or AppMode.PageEdit) return;
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
        if (vm.Mode is AppMode.Settings or AppMode.GlobalSettings or AppMode.PageEdit) return;
        AnimateToStored();
    }

    public void ForceExpand(Action? callback = null)
    {
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;

        if (IsVerticalSnap)
        {
            AnimateTop(_window.Top, _normalTop, () =>
            {
                if (!IsMouseInWindowOrGap())
                    StartStorageTimer();
                callback?.Invoke();
            });
        }
        else
        {
            AnimateLeft(_window.Left, _normalLeft, () =>
            {
                if (!IsMouseInWindowOrGap())
                    StartStorageTimer();
                callback?.Invoke();
            });
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    /// <summary>
    /// 吸着方向に応じてギャップ側を判定。
    /// </summary>
    private bool IsMouseInWindowOrGap()
    {
        GetCursorPos(out var screenPt);
        var pos  = _window.PointFromScreen(new Point(screenPt.X, screenPt.Y));
        double gap = App.ConfigService.Current.Layout.ScreenEdgeDistance;

        bool inX = _direction switch
        {
            "left"   => pos.X >= -gap && pos.X <= _window.Width,
            "top"    => pos.X >= 0    && pos.X <= _window.Width,
            "bottom" => pos.X >= 0    && pos.X <= _window.Width,
            _        => pos.X >= 0    && pos.X <= _window.Width + gap,  // right
        };
        bool inY = _direction switch
        {
            "top"    => pos.Y >= -gap && pos.Y <= _window.Height,        // gap above (between screen top and frame)
            "bottom" => pos.Y >= 0    && pos.Y <= _window.Height + gap,  // gap below (between frame and work area bottom)
            _        => pos.Y >= 0    && pos.Y <= _window.Height,        // left/right
        };
        return inX && inY;
    }

    private void AnimateToStored()
    {
        if (IsVerticalSnap)
        {
            AnimateTop(_window.Top, _storedTop, () =>
            {
                if (App.LauncherViewModel != null)
                    App.LauncherViewModel.IsStored = true;
            });
        }
        else
        {
            AnimateLeft(_window.Left, _storedLeft, () =>
            {
                if (App.LauncherViewModel != null)
                    App.LauncherViewModel.IsStored = true;
            });
        }
    }

    private void AnimateToNormal()
    {
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;

        if (IsVerticalSnap)
        {
            AnimateTop(_window.Top, _normalTop, () =>
            {
                if (!IsMouseInWindowOrGap())
                    StartStorageTimer();
            });
        }
        else
        {
            AnimateLeft(_window.Left, _normalLeft, () =>
            {
                if (!IsMouseInWindowOrGap())
                    StartStorageTimer();
            });
        }
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

    private void AnimateTop(double from, double to, Action? onComplete)
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
        _window.BeginAnimation(Window.TopProperty, anim);
    }

    private void StopAnimation()
    {
        _animGeneration++;
        _isAnimating = false;
        double left = _window.Left;
        double top  = _window.Top;
        _window.BeginAnimation(Window.LeftProperty, null);
        _window.BeginAnimation(Window.TopProperty,  null);
        _window.Left = left;
        _window.Top  = top;
    }

    // ─── マルチモニター ────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
        MonitorEnumProc lpfnEnum, IntPtr dwData);

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor,
        ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    private record MonitorRect(double Left, double Top, double Width, double Height, double WorkBottom);

    public int GetMonitorCount()
    {
        int count = 0;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (IntPtr _, IntPtr _, ref RECT _, IntPtr _) => { count++; return true; }, IntPtr.Zero);
        return Math.Max(1, count);
    }

    public string[] GetMonitorNames()
    {
        var names = new List<string>();
        int idx = 0;
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr _, ref RECT _, IntPtr _) =>
        {
            idx++;
            var info = new MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>() };
            string device = GetMonitorInfo(hMon, ref info)
                ? info.szDevice.TrimEnd('\0')
                : $"Display{idx}";
            if (device.StartsWith(@"\\.\"))
                device = device[4..];
            names.Add($"{idx} {device}");
            return true;
        }, IntPtr.Zero);

        if (names.Count == 0)
            names.Add("1");

        return [.. names];
    }

    private MonitorRect GetTargetMonitor(int snapMonitor)
    {
        var monitors = new List<(RECT rcMon, RECT rcWork)>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr _, ref RECT rc, IntPtr _) =>
        {
            var info = new MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>() };
            RECT rcWork = rc;
            if (GetMonitorInfo(hMon, ref info))
                rcWork = info.rcWork;
            monitors.Add((rc, rcWork));
            return true;
        }, IntPtr.Zero);

        if (monitors.Count == 0)
            return new MonitorRect(0, 0,
                SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight,
                SystemParameters.PrimaryScreenHeight);

        int idx = Math.Clamp(snapMonitor - 1, 0, monitors.Count - 1);
        var (rcMon, rcWrk) = monitors[idx];
        double dpiX = GetDpiScaleX();
        double dpiY = GetDpiScaleY();
        return new MonitorRect(
            rcMon.Left              / dpiX,
            rcMon.Top               / dpiY,
            (rcMon.Right - rcMon.Left) / dpiX,
            (rcMon.Bottom - rcMon.Top) / dpiY,
            rcWrk.Bottom            / dpiY);
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
