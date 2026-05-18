using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AppLauncher.Helpers;
using AppLauncher.Models;

namespace AppLauncher.Views.Controls;

public partial class GlobalSettingsView : UserControl
{
    public GlobalSettingsView()
    {
        InitializeComponent();
        OkButton.MouseLeftButtonUp     += (_, _) => App.LauncherViewModel?.ConfirmGlobalSettingsCommand.Execute(null);
        CancelButton.MouseLeftButtonUp += (_, _) => App.LauncherViewModel?.CloseGlobalSettingsCommand.Execute(null);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyUiElementColor();

    public void ApplyUiElementColor()
    {
        var vm = App.LauncherViewModel;
        if (vm == null) return;
        var baseColor = ColorPalette.GetColor(vm.CurrentPage.BackgroundColor);
        var uiBrush   = new SolidColorBrush(ColorHelper.ComputeUiElementColor(baseColor));

        Background      = new SolidColorBrush(Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B));
        OkButton.Background     = uiBrush;
        CancelButton.Background = uiBrush;
    }
}
