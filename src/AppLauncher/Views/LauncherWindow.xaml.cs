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

    public void SetViewModel(LauncherViewModel vm)
    {
        var layout = App.ConfigService.Current.Layout;

        RootGrid.ColumnDefinitions[0].Width = new GridLength(layout.HandleShortSide);
        RootGrid.ColumnDefinitions[1].Width = new GridLength(layout.HandleFrameMargin);
        Frame.CornerRadius = new CornerRadius(layout.FrameCornerRadius);

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
        var vm = App.LauncherViewModel;
        if (vm == null || vm.Mode != AppMode.Normal) return;
        if (IsInteractiveTarget(e.OriginalSource)) return;
        vm.IsPinned = !vm.IsPinned;
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
