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
    /// タイル・ボタン上のダブルクリックはピンモード切り替えに使わない。
    /// </summary>
    private static bool IsInteractiveTarget(object source)
    {
        var dep = source as DependencyObject;
        while (dep != null)
        {
            if (dep is TileControl or Button or RepeatButton) return true;
            dep = VisualTreeHelper.GetParent(dep);
        }
        return false;
    }
}
