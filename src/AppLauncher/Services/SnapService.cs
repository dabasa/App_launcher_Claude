using System.Windows;
using System.Windows.Input;
using AppLauncher.Helpers;
using AppLauncher.Views;

namespace AppLauncher.Services;

public class SnapService
{
    private LauncherWindow _window = null!;
    private double _dragStartTop;
    private double _dragStartMouseY; // 物理ピクセル
    private bool _isDragging;

    private const double DragThresholdPhysical = 5.0;

    public void Attach(LauncherWindow window)
    {
        _window = window;
        _window.MouseLeftButtonDown += OnMouseDown;
        _window.MouseMove          += OnMouseMove;
        // handledEventsToo: タイルが Handled にしても OnMouseUp を確実に受け取る
        _window.AddHandler(UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnMouseUp), handledEventsToo: true);
    }

    /// <summary>
    /// 吸着位置を算出してウィンドウを配置する。現在は右端吸着のみ対応（Ph.10 で全方向対応）。
    /// </summary>
    public void ApplySnap()
    {
        var config = App.ConfigService.Current;
        var layout = config.Layout;
        var global = config.Global;

        var (frameW, frameH) = WindowSizeCalculator.Calculate(global, layout);

        double windowW = layout.HandleShortSide + layout.HandleFrameMargin + frameW;
        double windowH = frameH;

        _window.Width = windowW;
        _window.Height = windowH;

        // 主モニターの論理ピクセル座標（WPF は DPI スケールを自動補正済み）
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        // 右端吸着：フレーム右端 = 画面右端 - screenEdgeDistance
        // ウィンドウ左端 = 画面右端 - screenEdgeDistance - frameW - handleFrameMargin - handleShortSide
        _window.Left = screenW - layout.ScreenEdgeDistance - frameW
                       - layout.HandleFrameMargin - layout.HandleShortSide;
        _window.Top = (screenH - windowH) / 2;
    }

    // ─── ドラッグ（右端吸着時：縦方向のみ移動可能） ───────────────────────

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

    private double GetDpiScaleY()
    {
        var source = PresentationSource.FromVisual(_window);
        return source?.CompositionTarget.TransformToDevice.M22 ?? 1.0;
    }
}
