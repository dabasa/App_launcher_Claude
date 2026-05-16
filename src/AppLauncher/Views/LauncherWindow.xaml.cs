using System.Windows;
using System.Windows.Media;
using AppLauncher.Models;
using AppLauncher.ViewModels;

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
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LauncherViewModel.CurrentPage))
                ApplyPageColors(vm.CurrentPage);
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
}
