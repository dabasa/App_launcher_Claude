using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AppLauncher.Helpers;
using AppLauncher.Models;
using AppLauncher.ViewModels;
using AppLauncher.Views;

namespace AppLauncher.Services;

public class SnapService
{
    private LauncherWindow _window = null!;

    // Drag
    private double _dragStartTop;
    private double _dragStartLeft;
    private double _dragStartMouseY;
    private double _dragStartMouseX;
    private bool _isDragging;
    private const double DragThresholdPhysical = 5.0;

    // Window position for left/right snaps
    private double _normalLeft;
    private double _storedLeft;

    // Window position for top/bottom snaps
    private double _normalTop;
    private double _storedTop;
    private double _normalOffset;
    private double _storedOffset;
    private double _contentWidth;
    private double _contentHeight;

    // Timer and animation state
    private DispatcherTimer? _storageTimer;
    private bool    _isAnimating;
    private int     _animGeneration;
    private double  _animFrom, _animTo;
    private long    _animStartTs;   // Stopwatch.GetTimestamp() at animation start
    private int     _animDurMs;     // duration in ms
    private Action? _animOnComplete;

    // Frame height
    private double _baseFrameH;

    // Snap direction
    private string _direction = "right";

    private bool IsVerticalSnap => _direction is "top" or "bottom";

    // Monitor clipping
    private bool   _hasSnap;        // ApplySnap() 螳御ｺ・ｾ・true
    private double _monBoundary;
    public void Attach(LauncherWindow window)
    {
        _window = window;
        _window.AddHandler(UIElement.MouseLeftButtonDownEvent,
            new MouseButtonEventHandler(OnMouseDown), handledEventsToo: true);
        _window.MouseMove += OnMouseMove;
        _window.AddHandler(UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnMouseUp), handledEventsToo: true);
        _window.MouseEnter    += OnWindowMouseEnter;
        _window.MouseLeave    += OnWindowMouseLeave;
        System.Windows.Media.CompositionTarget.Rendering += OnCompositionRendering;
        _window.Closed += (_, _) =>
            System.Windows.Media.CompositionTarget.Rendering -= OnCompositionRendering;

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
        double contentH = IsVerticalSnap
            ? baseH + layout.HandleFrameMargin + layout.HandleShortSide
            : baseH;
        double windowH = IsVerticalSnap
            ? contentH + layout.ScreenEdgeDistance
            : contentH;

        if (_direction == "bottom")
        {
            double bottom = _window.Top + _window.Height;
            _window.Height = windowH;
            _window.Top = bottom - _window.Height;
            _normalTop = _window.Top;
            _storedTop = _window.Top;
        }
        else
        {
            _window.Height = windowH;
        }

        double contentW = double.IsNaN(_window.RootGrid.Width) ? _window.Width : _window.RootGrid.Width;
        SetSnapContentSize(contentW, contentH);
    }

    /// <summary>
    /// Recalculate layout, window bounds, and content offsets from the current snap settings.
    /// </summary>
    public void ApplySnap(bool preserveOrthogonal = false)
    {
        double savedTop  = _window.Top;
        double savedLeft = _window.Left;
        double? configSavedTop  = App.ConfigService.Current.Global.SavedWindowTop;
        double? configSavedLeft = App.ConfigService.Current.Global.SavedWindowLeft;

        StopAnimation();

        // Reset to expanded state.
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;

        var config  = App.ConfigService.Current;
        var layout  = config.Layout;
        var global  = config.Global;
        _direction  = global.SnapPosition;

        _window.ApplySnapLayout(_direction);

        var (frameW, frameH) = WindowSizeCalculator.Calculate(global, layout);
        var monitor = GetTargetMonitor(global.SnapMonitor);
        double screenLeft   = monitor.Left;
        double screenTop    = monitor.Top;
        double screenW      = monitor.Width;
        double screenH      = monitor.Height;
        double screenBottom = screenTop + screenH;
        double screenRight  = screenLeft + screenW;

        _baseFrameH  = frameH;
        _monBoundary = _direction switch
        {
            "right"  => screenRight,
            "left"   => screenLeft,
            "top"    => screenTop,
            "bottom" => monitor.WorkBottom,
            _        => screenRight,
        };
        _hasSnap = true;

        double edgeDistance = layout.ScreenEdgeDistance;
        double contentW = IsVerticalSnap
            ? frameW
            : layout.HandleShortSide + layout.HandleFrameMargin + frameW;
        double contentH = IsVerticalSnap
            ? frameH + layout.HandleFrameMargin + layout.HandleShortSide
            : frameH;

        switch (_direction)
        {
            case "top":
                _window.Width = frameW;
                _window.Height = contentH + edgeDistance;
                SetSnapContentSize(contentW, contentH);
                _normalOffset = edgeDistance;
                _storedOffset = -frameH - layout.HandleFrameMargin;
                UpdateWindowHeight();
                _window.Height = contentH + edgeDistance;
                SetSnapContentSize(contentW, contentH);
                _normalTop = screenTop;
                _storedTop = screenTop;
                _window.Top  = _normalTop;
                _window.Left = preserveOrthogonal
                    ? Math.Clamp(savedLeft, screenLeft, screenRight - _window.Width)
                    : configSavedLeft.HasValue
                        ? Math.Clamp(configSavedLeft.Value, screenLeft, screenRight - _window.Width)
                        : screenLeft + (screenW - _window.Width) / 2;
                break;

            case "bottom":
                _window.Width = frameW;
                _window.Height = contentH + edgeDistance;
                SetSnapContentSize(contentW, contentH);
                _normalOffset = 0;
                _storedOffset = frameH + layout.HandleFrameMargin + edgeDistance;
                UpdateWindowHeight();
                _window.Height = contentH + edgeDistance;
                SetSnapContentSize(contentW, contentH);
                _normalTop = monitor.WorkBottom - _window.Height;
                _storedTop = _normalTop;
                _window.Top  = _normalTop;
                _window.Left = preserveOrthogonal
                    ? Math.Clamp(savedLeft, screenLeft, screenRight - _window.Width)
                    : configSavedLeft.HasValue
                        ? Math.Clamp(configSavedLeft.Value, screenLeft, screenRight - _window.Width)
                        : screenLeft + (screenW - _window.Width) / 2;
                break;

            case "left":
                _window.Width = contentW + edgeDistance;
                UpdateWindowHeight();
                SetSnapContentSize(contentW, _window.Height);
                _normalOffset = edgeDistance;
                _storedOffset = -frameW - layout.HandleFrameMargin;
                _normalLeft = screenLeft;
                _storedLeft = screenLeft;
                _window.Left = _normalLeft;
                _window.Top  = preserveOrthogonal
                    ? Math.Clamp(savedTop, screenTop, screenBottom - _window.Height)
                    : configSavedTop.HasValue
                        ? Math.Clamp(configSavedTop.Value, screenTop, screenBottom - _window.Height)
                        : screenTop + (screenH - _window.Height) / 2;
                break;

            case "right":
            default:
                _window.Width = contentW + edgeDistance;
                UpdateWindowHeight();
                SetSnapContentSize(contentW, _window.Height);
                _normalOffset = 0;
                _storedOffset = frameW + layout.HandleFrameMargin + edgeDistance;
                _normalLeft = screenRight - _window.Width;
                _storedLeft = _normalLeft;
                _window.Left = _normalLeft;
                _window.Top  = preserveOrthogonal
                    ? Math.Clamp(savedTop, screenTop, screenBottom - _window.Height)
                    : configSavedTop.HasValue
                        ? Math.Clamp(configSavedTop.Value, screenTop, screenBottom - _window.Height)
                        : screenTop + (screenH - _window.Height) / 2;
                break;
        }

        SetSnapOffset(_normalOffset);

        _window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            var vm = App.LauncherViewModel;
            if (vm == null || vm.IsPinned || vm.IsStored || _isAnimating) return;
            if (vm.Mode is AppMode.Settings or AppMode.GlobalSettings or AppMode.PageEdit) return;
            if (!IsMouseInWindowOrGap())
                StartStorageTimer();
        });

        _window.SuppressDpiChange = false;
        UpdateMonitorClip();
    }

    // Drag

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

    // Store / expand

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

        AnimateOffset(GetSnapOffset(), _normalOffset, () =>
        {
            _window.SuppressDpiChange = false;
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
    /// Checks the window and snap-edge gap in content-local coordinates.
    /// </summary>
    private bool IsMouseInWindowOrGap()
    {
        if (PresentationSource.FromVisual(_window) == null) return true;
        GetCursorPos(out var screenPt);
        var pos  = _window.PointFromScreen(new Point(screenPt.X, screenPt.Y));
        var offset = _window.GetSnapContentOffset();
        pos = new Point(pos.X - offset.X, pos.Y - offset.Y);
        double gap = App.ConfigService.Current.Layout.ScreenEdgeDistance;

        bool inX = _direction switch
        {
            "left"   => pos.X >= -gap && pos.X <= _contentWidth,
            "top"    => pos.X >= 0    && pos.X <= _contentWidth,
            "bottom" => pos.X >= 0    && pos.X <= _contentWidth,
            _        => pos.X >= 0    && pos.X <= _contentWidth + gap,
        };
        bool inY = _direction switch
        {
            "top"    => pos.Y >= -gap && pos.Y <= _contentHeight,
            "bottom" => pos.Y >= 0    && pos.Y <= _contentHeight + gap,
            _        => pos.Y >= 0    && pos.Y <= _contentHeight,
        };
        return inX && inY;
    }

    private void SetSnapContentSize(double width, double height)
    {
        _contentWidth = width;
        _contentHeight = height;
        _window.SetSnapContentSize(width, height);
    }

    private void AnimateToStored()
    {
        AnimateOffset(GetSnapOffset(), _storedOffset, () =>
        {
            if (App.LauncherViewModel != null)
                App.LauncherViewModel.IsStored = true;
        });
    }

    private void AnimateToNormal()
    {
        if (App.LauncherViewModel != null)
            App.LauncherViewModel.IsStored = false;

        AnimateOffset(GetSnapOffset(), _normalOffset, () =>
        {
            _window.SuppressDpiChange = false;
            if (!IsMouseInWindowOrGap())
                StartStorageTimer();
        });
    }

    private double GetSnapOffset()
    {
        var offset = _window.GetSnapContentOffset();
        return IsVerticalSnap ? offset.Y : offset.X;
    }

    private void SetSnapOffset(double offset)
    {
        if (IsVerticalSnap)
            _window.SetSnapContentOffset(0, offset);
        else
            _window.SetSnapContentOffset(offset, 0);
    }

    private void AnimateOffset(double from, double to, Action? onComplete)
    {
        StopAnimation();
        _animFrom       = from;
        _animTo         = to;
        _window.SuppressDpiChange = true;
        _animDurMs      = App.ConfigService.Current.Global.AnimationDurationMs;
        if (_animDurMs == 0) _animDurMs = 1;
        _animStartTs    = Stopwatch.GetTimestamp();
        _animOnComplete = onComplete;
        _isAnimating    = true;
        ++_animGeneration;
    }

    private void StopAnimation()
    {
        _isAnimating    = false;
        _animOnComplete = null;
        _animGeneration++;
    }

    // Monitor clipping

    private void OnCompositionRendering(object? sender, EventArgs e)
    {
        if (!_hasSnap) return;

        double winL = _window.Left;
        double winT = _window.Top;
        double winW = _window.Width;
        double winH = _window.Height;

        if (_isAnimating)
        {
            double elapsed = (Stopwatch.GetTimestamp() - _animStartTs)
                             / (double)Stopwatch.Frequency * 1000.0;
            double t   = Math.Clamp(elapsed / _animDurMs, 0.0, 1.0);
            double pos = _animFrom + (_animTo - _animFrom) * t;

            // Move only the content; the HWND stays inside the selected monitor.
            SetSnapOffset(pos);

            if (t >= 1.0)
            {
                _isAnimating = false;
                var cb = _animOnComplete;
                _animOnComplete = null;
                cb?.Invoke();
            }
        }

        UpdateMonitorClip(winL, winT, winW, winH);
    }

    private void UpdateMonitorClip(double winL, double winT, double winW, double winH)
    {
        double b = _monBoundary;
        double clipL = 0, clipT = 0, clipW = winW, clipH = winH;

        switch (_direction)
        {
            case "right":  clipW = Math.Clamp(b - winL, 0, winW); break;
            case "left":   clipL = Math.Clamp(b - winL, 0, winW); clipW = winW - clipL; break;
            case "top":    clipT = Math.Clamp(b - winT, 0, winH); clipH = winH - clipT; break;
            case "bottom": clipH = Math.Clamp(b - winT, 0, winH); break;
        }

        _window.SetMonitorClip(new Rect(clipL, clipT, clipW, clipH));
    }

    private void UpdateMonitorClip()
    {
        if (!_hasSnap) return;
        UpdateMonitorClip(_window.Left, _window.Top, _window.Width, _window.Height);
    }

    // Monitors

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
