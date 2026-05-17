using System.Collections.Specialized;
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
        WireButtons();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.Pages.CollectionChanged -= OnPagesCollectionChanged;
        }

        _vm = DataContext as LauncherViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.Pages.CollectionChanged += OnPagesCollectionChanged;
            Rebuild();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherViewModel.CurrentPage)
                           or nameof(LauncherViewModel.CurrentPageIndex)
                           or nameof(LauncherViewModel.Mode))
            Rebuild();
    }

    private void OnPagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Rebuild();

    private void WireButtons()
    {
        ModeButton.MouseLeftButtonUp       += (_, _) => _vm?.ToggleModeCommand.Execute(null);
        MoveLeftButton.MouseLeftButtonUp   += (_, _) => _vm?.MovePageLeftCommand.Execute(null);
        AddPageButton.MouseLeftButtonUp    += (_, _) => _vm?.AddPageCommand.Execute(null);
        RemovePageButton.MouseLeftButtonUp += (_, _) => _vm?.RemovePageCommand.Execute(null);
        MoveRightButton.MouseLeftButtonUp  += (_, _) => _vm?.MovePageRightCommand.Execute(null);
    }

    private void Rebuild()
    {
        if (_vm == null) return;

        bool isEdit = _vm.Mode == AppMode.Edit;
        var layout  = App.ConfigService.Current.Layout;

        // ─── モードボタン色 ───────────────────────────────────────────
        var modeColor = isEdit
            ? ColorPalette.GetColor("orange")
            : ColorPalette.GetColor(_vm.CurrentPage.BackgroundColor);
        ModeButton.Background   = new SolidColorBrush(modeColor);
        ModeButton.CornerRadius = new CornerRadius(layout.SettingsButtonCornerRadius);

        // ─── 行2 表示切替 ────────────────────────────────────────────
        PageMgmtRow.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;

        if (isEdit) UpdatePageMgmtButtons();

        // ─── ページインジケーター再構築 ──────────────────────────────
        RebuildIndicators();
    }

    private void UpdatePageMgmtButtons()
    {
        if (_vm == null) return;

        bool canAdd    = _vm.Pages.Count < 10;
        bool canRemove = _vm.Pages.Count > 1 && _vm.CurrentPage.Tiles.Count == 0;
        bool canLeft   = _vm.CurrentPageIndex > 0;
        bool canRight  = _vm.CurrentPageIndex < _vm.Pages.Count - 1;

        SetPageBtn(AddPageButton,    "⊕", canAdd);
        SetPageBtn(RemovePageButton, "⊖", canRemove);
        SetPageBtn(MoveLeftButton,   "←", canLeft);
        SetPageBtn(MoveRightButton,  "→", canRight);
    }

    private static void SetPageBtn(Border btn, string label, bool active)
    {
        if (btn.Child is not TextBlock tb)
        {
            tb = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Foreground          = Brushes.White,
                FontSize            = 14,
            };
            btn.Child        = tb;
            btn.Background   = new SolidColorBrush(Color.FromArgb(153, 128, 128, 128));
            btn.CornerRadius = new CornerRadius(App.ConfigService.Current.Layout.PageOpButtonSize / 2.0);
        }
        tb.Text              = label;
        btn.Opacity          = active ? 1.0 : 0.3;
        btn.IsHitTestVisible = active;
    }

    private void RebuildIndicators()
    {
        if (_vm == null) return;
        IndicatorPanel.Children.Clear();

        for (int i = 0; i < _vm.Pages.Count; i++)
        {
            var page      = _vm.Pages[i];
            bool isCurrent = i == _vm.CurrentPageIndex;
            var color     = ColorPalette.GetColor(page.BackgroundColor);
            var brush     = new SolidColorBrush(color);
            int pageIndex = i;

            var indicator = new Grid
            {
                Width      = 18,
                Height     = 18,
                Cursor     = Cursors.Hand,
                Background = Brushes.Transparent,
                Margin     = i < _vm.Pages.Count - 1
                    ? new Thickness(0, 0, 8, 0)
                    : new Thickness(0),
            };
            indicator.MouseLeftButtonUp += (_, _) =>
                _vm.NavigateToPageCommand.Execute(pageIndex);

            if (isCurrent)
            {
                indicator.Children.Add(new Ellipse { Width = 18, Height = 18, Fill = brush });
                indicator.Children.Add(new Ellipse
                {
                    Width               = 7,
                    Height              = 7,
                    Fill                = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                });
            }
            else
            {
                indicator.Children.Add(new Ellipse
                {
                    Width           = 18,
                    Height          = 18,
                    Stroke          = brush,
                    StrokeThickness = 2,
                    Fill            = Brushes.Transparent,
                });
            }

            IndicatorPanel.Children.Add(indicator);
        }
    }
}
