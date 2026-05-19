using System.ComponentModel;
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
            if (dep is TileControl or Button or RepeatButton or BottomBarControl) return true;
            dep = VisualTreeHelper.GetParent(dep);
        }
        return false;
    }
}
