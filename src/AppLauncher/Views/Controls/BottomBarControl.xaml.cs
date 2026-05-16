using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AppLauncher.Models;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class BottomBarControl : UserControl
{
    private LauncherViewModel? _vm;

    public BottomBarControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as LauncherViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            Rebuild();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.CurrentPage))
            Rebuild();
    }

    private void Rebuild()
    {
        if (_vm == null) return;

        var layout = App.ConfigService.Current.Layout;

        // モードボタン設定
        var bgColor = ColorPalette.GetColor(_vm.CurrentPage.BackgroundColor);
        ModeButton.Background = new SolidColorBrush(bgColor);
        ModeButton.CornerRadius = new CornerRadius(layout.SettingsButtonCornerRadius);
        ModeButton.Margin = new Thickness(0, 0, layout.SettingsButtonRightMargin, 0);

        // インジケーター再構築
        IndicatorPanel.Children.Clear();

        for (int i = 0; i < _vm.Pages.Count; i++)
        {
            var page = _vm.Pages[i];
            bool isCurrent = (i == _vm.CurrentPageIndex);
            var color = ColorPalette.GetColor(page.BackgroundColor);
            var brush = new SolidColorBrush(color);
            int pageIndex = i;

            var indicator = new Grid
            {
                Width = 18,
                Height = 18,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Margin = i < _vm.Pages.Count - 1
                    ? new Thickness(0, 0, 8, 0)
                    : new Thickness(0),
            };
            indicator.MouseLeftButtonUp += (_, _) =>
                _vm.NavigateToPageCommand.Execute(pageIndex);

            if (isCurrent)
            {
                // 塗りつぶし円
                indicator.Children.Add(new Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Fill = brush,
                });
                // 中心の白ドット（半径 3.5 px = 直径 7 px）
                indicator.Children.Add(new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }
            else
            {
                // 外枠のみ
                indicator.Children.Add(new Ellipse
                {
                    Width = 18,
                    Height = 18,
                    Stroke = brush,
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                });
            }

            IndicatorPanel.Children.Add(indicator);
        }
    }
}
