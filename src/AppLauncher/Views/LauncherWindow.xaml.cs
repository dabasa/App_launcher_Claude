using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AppLauncher.Models;
using AppLauncher.ViewModels;
using AppLauncher.Views.Controls;

namespace AppLauncher.Views;

public partial class LauncherWindow : Window
{
    public LauncherWindow() => InitializeComponent();

    // ─── DPI 変更抑制 ─────────────────────────────────────────────────────────
    // HwndSource.AddHook はWPF内部処理の後に呼ばれるため WM_DPICHANGED を抑制できない。
    // SetWindowLongPtr でWPFのウィンドウプロシージャの前段に割り込む（Win32サブクラス化）。
    internal bool SuppressDpiChange { get; set; }

    private const int WM_DPICHANGED  = 0x02E0;
    private const int GWLP_WNDPROC   = -4;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int nIndex, IntPtr newProc);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr prevProc, IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProcDelegate; // GC に回収されないよう保持
    private IntPtr _prevWndProc = IntPtr.Zero;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _wndProcDelegate = SubclassWndProc;
        _prevWndProc = SetWindowLongPtr(
            new WindowInteropHelper(this).Handle,
            GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    private IntPtr SubclassWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        // WPF より先に実行されるため WM_DPICHANGED をここで止めれば WPF に届かない
        if (msg == WM_DPICHANGED && SuppressDpiChange)
            return IntPtr.Zero;
        return CallWindowProc(_prevWndProc, hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// 吸着方向に合わせてハンドルとフレームの列・行を入れ替える。
    /// 左右吸着: 列ベースレイアウト
    /// 上下吸着: 行ベースレイアウト
    /// </summary>
    public void ApplySnapLayout(string direction)
    {
        var layout = App.ConfigService.Current.Layout;

        switch (direction)
        {
            case "left":
                // col0=frame(*), col1=gap, col2=handle(fixed) / row0=*(full height)
                RootGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                RootGrid.ColumnDefinitions[1].Width = new GridLength(layout.HandleFrameMargin);
                RootGrid.ColumnDefinitions[2].Width = new GridLength(layout.HandleShortSide);
                RootGrid.RowDefinitions[0].Height   = new GridLength(1, GridUnitType.Star);
                RootGrid.RowDefinitions[1].Height   = new GridLength(0);
                RootGrid.RowDefinitions[2].Height   = new GridLength(0);
                Grid.SetColumn(Frame,  0); Grid.SetColumnSpan(Frame,  1);
                Grid.SetColumn(Handle, 2); Grid.SetColumnSpan(Handle, 1);
                Grid.SetRow(Frame,  0); Grid.SetRowSpan(Frame,  1);
                Grid.SetRow(Handle, 0); Grid.SetRowSpan(Handle, 1);
                Handle.VerticalAlignment   = VerticalAlignment.Center;
                Handle.HorizontalAlignment = HorizontalAlignment.Stretch;
                Handle.SetOrientation(horizontal: false);
                break;

            case "top":
                // row0=frame(*), row1=gap, row2=handle(fixed) / col0=*(full width)
                RootGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                RootGrid.ColumnDefinitions[1].Width = new GridLength(0);
                RootGrid.ColumnDefinitions[2].Width = new GridLength(0);
                RootGrid.RowDefinitions[0].Height   = new GridLength(1, GridUnitType.Star);
                RootGrid.RowDefinitions[1].Height   = new GridLength(layout.HandleFrameMargin);
                RootGrid.RowDefinitions[2].Height   = new GridLength(layout.HandleShortSide);
                Grid.SetColumn(Frame,  0); Grid.SetColumnSpan(Frame,  3);
                Grid.SetColumn(Handle, 0); Grid.SetColumnSpan(Handle, 3);
                Grid.SetRow(Frame,  0); Grid.SetRowSpan(Frame,  1);
                Grid.SetRow(Handle, 2); Grid.SetRowSpan(Handle, 1);
                Handle.VerticalAlignment   = VerticalAlignment.Stretch;
                Handle.HorizontalAlignment = HorizontalAlignment.Center;
                Handle.SetOrientation(horizontal: true);
                break;

            case "bottom":
                // row0=handle(fixed), row1=gap, row2=frame(*) / col0=*(full width)
                RootGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                RootGrid.ColumnDefinitions[1].Width = new GridLength(0);
                RootGrid.ColumnDefinitions[2].Width = new GridLength(0);
                RootGrid.RowDefinitions[0].Height   = new GridLength(layout.HandleShortSide);
                RootGrid.RowDefinitions[1].Height   = new GridLength(layout.HandleFrameMargin);
                RootGrid.RowDefinitions[2].Height   = new GridLength(1, GridUnitType.Star);
                Grid.SetColumn(Frame,  0); Grid.SetColumnSpan(Frame,  3);
                Grid.SetColumn(Handle, 0); Grid.SetColumnSpan(Handle, 3);
                Grid.SetRow(Frame,  2); Grid.SetRowSpan(Frame,  1);
                Grid.SetRow(Handle, 0); Grid.SetRowSpan(Handle, 1);
                Handle.VerticalAlignment   = VerticalAlignment.Stretch;
                Handle.HorizontalAlignment = HorizontalAlignment.Center;
                Handle.SetOrientation(horizontal: true);
                break;

            case "right":
            default:
                // col0=handle(fixed), col1=gap, col2=frame(*) / row0=*(full height)
                RootGrid.ColumnDefinitions[0].Width = new GridLength(layout.HandleShortSide);
                RootGrid.ColumnDefinitions[1].Width = new GridLength(layout.HandleFrameMargin);
                RootGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                RootGrid.RowDefinitions[0].Height   = new GridLength(1, GridUnitType.Star);
                RootGrid.RowDefinitions[1].Height   = new GridLength(0);
                RootGrid.RowDefinitions[2].Height   = new GridLength(0);
                Grid.SetColumn(Handle, 0); Grid.SetColumnSpan(Handle, 1);
                Grid.SetColumn(Frame,  2); Grid.SetColumnSpan(Frame,  1);
                Grid.SetRow(Handle, 0); Grid.SetRowSpan(Handle, 1);
                Grid.SetRow(Frame,  0); Grid.SetRowSpan(Frame,  1);
                Handle.VerticalAlignment   = VerticalAlignment.Center;
                Handle.HorizontalAlignment = HorizontalAlignment.Stretch;
                Handle.SetOrientation(horizontal: false);
                break;
        }
    }

    // ─── マルチディスプレイ クリッピング ──────────────────────────────────────
    // AllowsTransparency=True のため SetWindowRgn は機能しない。
    // RootGrid.Clip を使用する。透明ピクセルは WPF が自動的にクリックスルーにする。
    // RectangleGeometry を使い回してフレーム毎の GC を回避する。
    private RectangleGeometry? _clipGeo;
    private readonly TranslateTransform _snapTransform = new();

    public void SetMonitorClip(Rect clipRect)
    {
        if (clipRect.Width > 0 && clipRect.Height > 0)
        {
            if (_clipGeo == null)
            {
                _clipGeo = new RectangleGeometry(clipRect);
                WindowRoot.Clip = _clipGeo;
            }
            else
            {
                _clipGeo.Rect = clipRect;
                if (WindowRoot.Clip != _clipGeo)
                    WindowRoot.Clip = _clipGeo;
            }
        }
        else if (WindowRoot.Clip != null)
        {
            WindowRoot.Clip = null;
        }
    }

    public void SetSnapContentSize(double width, double height)
    {
        RootGrid.HorizontalAlignment = HorizontalAlignment.Left;
        RootGrid.VerticalAlignment = VerticalAlignment.Top;
        RootGrid.Width = width;
        RootGrid.Height = height;
        if (RootGrid.RenderTransform != _snapTransform)
            RootGrid.RenderTransform = _snapTransform;
    }

    public void SetSnapContentOffset(double x, double y)
    {
        if (RootGrid.RenderTransform != _snapTransform)
            RootGrid.RenderTransform = _snapTransform;
        _snapTransform.X = x;
        _snapTransform.Y = y;
    }

    public Point GetSnapContentOffset() => new(_snapTransform.X, _snapTransform.Y);

    public void SetViewModel(LauncherViewModel vm)
    {
        var layout = App.ConfigService.Current.Layout;

        RootGrid.ColumnDefinitions[0].Width = new GridLength(layout.HandleShortSide);
        RootGrid.ColumnDefinitions[1].Width = new GridLength(layout.HandleFrameMargin);
        Frame.CornerRadius = new CornerRadius(layout.FrameCornerRadius);

        Topmost = App.ConfigService.Current.Global.AlwaysOnTop;

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
            if (e.PropertyName == nameof(LauncherViewModel.Mode))
            {
                bool isTileEdit = vm.Mode == AppMode.TileEdit;
                TileEditPanel.Visibility = isTileEdit ? Visibility.Visible : Visibility.Collapsed;
                if (isTileEdit)
                    TileEditPanel.ApplyUiElementColor();

                bool isPageEdit = vm.Mode == AppMode.PageEdit;
                PageEditPanel.Visibility = isPageEdit ? Visibility.Visible : Visibility.Collapsed;
                if (isPageEdit)
                    PageEditPanel.ApplyUiElementColor();

                bool isGlobalSettings = vm.Mode == AppMode.GlobalSettings;
                GlobalSettingsPanel.Visibility = isGlobalSettings ? Visibility.Visible : Visibility.Collapsed;
                if (isGlobalSettings)
                    GlobalSettingsPanel.ApplyUiElementColor();
            }
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

    private void ApplyPinBorder(LauncherViewModel vm)
    {
        var layout = App.ConfigService.Current.Layout;
        if (!vm.IsPinned)
        {
            Frame.BorderBrush     = new SolidColorBrush(Colors.Black);
            Frame.BorderThickness = new Thickness(layout.FrameBorderThickness);
            Frame.Padding         = new Thickness(layout.PinBorderThickness - layout.FrameBorderThickness);
            Handle.ResetBorder();
            return;
        }
        var page  = vm.CurrentPage;
        var color = ColorPalette.GetColor(page.PinFrameColor);
        double alpha = ColorPalette.OpacityToDouble(page.PinFrameOpacity);
        int bt    = layout.PinBorderThickness;
        var brush = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), color.R, color.G, color.B));
        Frame.BorderBrush     = brush;
        Frame.BorderThickness = new Thickness(bt);
        Frame.Padding         = new Thickness(0);
        Handle.SetPinBorder(brush, bt);
    }

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Handled) return; // TileControl 等が処理済みの場合はスキップ
        var vm = App.LauncherViewModel;
        if (vm == null || vm.Mode != AppMode.Normal) return;
        if (IsInteractiveTarget(e.OriginalSource)) return;
        vm.IsPinned = !vm.IsPinned;
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (IsInteractiveTarget(e.OriginalSource)) return;

        var trayIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon)App.Current.Resources["TrayIcon"];
        var menu = trayIcon.ContextMenu;
        if (menu == null) return;
        menu.PlacementTarget = this;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        RoutedEventHandler? onClosed = null;
        onClosed = (_, _) =>
        {
            menu.Closed -= onClosed;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                // ContextMenu が Mouse.Capture(menu, SubTree) を設定したまま残すことがある
                // → キャプチャを解放してから Frame のヒットテストを確実に有効化する
                Mouse.Capture(null);
                Keyboard.ClearFocus();
                Frame.IsHitTestVisible = true;
                Activate();
            }));
        };
        menu.Closed += onClosed;
        menu.IsOpen = true;
        e.Handled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (App.ConfigService.Current.Global.TaskTrayResident)
        {
            e.Cancel = true;
            WindowState = WindowState.Minimized;
            return;
        }
        base.OnClosing(e);
    }

    private static bool IsInteractiveTarget(object source)
    {
        var dep = source as DependencyObject;
        while (dep != null)
        {
            if (dep is TileControl or SystemTileControl or WebViewTileControl
                     or Button or RepeatButton or BottomBarControl) return true;
            dep = VisualTreeHelper.GetParent(dep);
        }
        return false;
    }
}
