using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AppLauncher.Helpers;
using AppLauncher.Models;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class PageEditView : UserControl
{
    private PageEditViewModel? _subscribedVm;

    public PageEditView()
    {
        InitializeComponent();
        OkButton.MouseLeftButtonUp     += (_, _) => App.LauncherViewModel?.ConfirmPageEditCommand.Execute(null);
        CancelButton.MouseLeftButtonUp += (_, _) => App.LauncherViewModel?.ClosePageEditCommand.Execute(null);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyUiElementColor();

    public void ApplyUiElementColor()
    {
        var launcherVm = App.LauncherViewModel;
        if (launcherVm == null) return;

        var baseColor = ColorPalette.GetColor(launcherVm.CurrentPage.BackgroundColor);
        var uiBrush   = new SolidColorBrush(ColorHelper.ComputeUiElementColor(baseColor));

        Background          = new SolidColorBrush(Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B));
        OkButton.Background     = uiBrush;
        CancelButton.Background = uiBrush;

        // リアルタイムプレビューの購読を更新
        var pageEditVm = launcherVm.PageEditVm;
        if (!ReferenceEquals(_subscribedVm, pageEditVm))
        {
            if (_subscribedVm != null)
                _subscribedVm.PropertyChanged -= OnPageEditVmPropertyChanged;
            _subscribedVm = pageEditVm;
            if (_subscribedVm != null)
                _subscribedVm.PropertyChanged += OnPageEditVmPropertyChanged;
        }

        UpdatePreview();
    }

    private void OnPageEditVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PageEditViewModel.BackgroundColor)
                           or nameof(PageEditViewModel.BackgroundOpacity))
            UpdatePreview();
    }

    private void UpdatePreview()
    {
        var launcherVm = App.LauncherViewModel;
        var pageEditVm = launcherVm?.PageEditVm;
        if (pageEditVm == null) return;

        var bgColor = ColorPalette.GetColor(pageEditVm.BackgroundColor);
        double alpha = ColorPalette.OpacityToDouble(pageEditVm.BackgroundOpacity);
        PreviewBorder.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), bgColor.R, bgColor.G, bgColor.B));

        PreviewName.Text = launcherVm?.CurrentPage.Name ?? "";
    }
}
