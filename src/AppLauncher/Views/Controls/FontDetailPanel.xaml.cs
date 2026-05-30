using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppLauncher.Helpers;
using AppLauncher.Models;
using AppLauncher.Models.Config;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class FontDetailPanel : UserControl
{
    private Action<FontConfig>? _onConfirm;

    public FontDetailPanel()
    {
        InitializeComponent();
        DialogOkButton.MouseLeftButtonUp     += OnOkClick;
        DialogCancelButton.MouseLeftButtonUp += OnCancelClick;
        FontNameCombo.DropDownClosed         += (_, _) => Mouse.Capture(null);
    }

    // ─── 公開 API ─────────────────────────────────────────────────────────
    /// <summary>
    /// ダイアログを開く。OK 時に <paramref name="onConfirm"/> が呼ばれる。
    /// </summary>
    public void Open(FontConfig src, string previewText, string dialogTitle,
                     Action<FontConfig> onConfirm, Color uiColor)
    {
        _onConfirm = onConfirm;
        DataContext = new FontDetailViewModel(src, previewText, dialogTitle);

        var uiBrush = new SolidColorBrush(uiColor);
        DialogOkButton.Background     = uiBrush;
        DialogCancelButton.Background = new SolidColorBrush(
            ColorHelper.ComputeUiElementColor(uiColor));

        Visibility = Visibility.Visible;
    }

    // ─── ボタンハンドラー ─────────────────────────────────────────────────
    private void OnOkClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (DataContext is FontDetailViewModel vm)
            _onConfirm?.Invoke(vm.ToConfig());
        Close();
    }

    private void OnCancelClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Close();
    }

    private void Close()
    {
        _onConfirm  = null;
        DataContext = null;
        Visibility  = Visibility.Collapsed;
    }
}
