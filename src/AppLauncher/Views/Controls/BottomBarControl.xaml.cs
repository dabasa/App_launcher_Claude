using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using AppLauncher.Models;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class BottomBarControl : UserControl
{
    private LauncherViewModel? _vm;
    private DispatcherTimer?   _dragSwitchTimer;
    private int                _dragSwitchTargetIndex;

    private static readonly BitmapImage ImgNormal   = LoadImage("mode01chg.png");
    private static readonly BitmapImage ImgEdit      = LoadImage("mode02chg.png");
    private static readonly BitmapImage ImgSettings  = LoadImage("mode03chg.png");

    private static BitmapImage LoadImage(string fileName)
        => new(new Uri($"pack://application:,,,/resources/image/{fileName}"));

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
        ModeButton.MouseLeftButtonUp           += (_, _) => _vm?.ToggleModeCommand.Execute(null);
        PageEditButton.MouseLeftButtonUp       += (_, _) => _vm?.OpenPageEditCommand.Execute(null);
        GlobalSettingsButton.MouseLeftButtonUp += (_, _) => _vm?.OpenGlobalSettingsCommand.Execute(null);
        MoveLeftButton.MouseLeftButtonUp       += (_, _) => _vm?.MovePageLeftCommand.Execute(null);
        AddPageButton.MouseLeftButtonUp        += (_, _) => _vm?.AddPageCommand.Execute(null);
        RemovePageButton.MouseLeftButtonUp     += (_, _) => _vm?.RemovePageCommand.Execute(null);
        MoveRightButton.MouseLeftButtonUp      += (_, _) => _vm?.MovePageRightCommand.Execute(null);
    }

    private void Rebuild()
    {
        if (_vm == null) return;

        bool isEdit     = _vm.Mode == AppMode.Edit;
        bool isSettings = _vm.Mode is AppMode.Settings or AppMode.GlobalSettings;
        var layout      = App.ConfigService.Current.Layout;

        // ─── モードボタン背景色 ＋ 画像切替 ──────────────────────────
        System.Windows.Media.Color modeColor;
        if (isEdit)
            modeColor = ColorPalette.GetColor("orange");
        else if (isSettings)
            modeColor = ColorPalette.GetColor("teal");
        else
            modeColor = ColorPalette.GetColor(_vm.CurrentPage.BackgroundColor);
        ModeButton.Background   = new SolidColorBrush(modeColor);
        ModeButton.CornerRadius = new CornerRadius(layout.SettingsButtonCornerRadius);
        ModeButtonImage.Source  = isEdit ? ImgEdit : isSettings ? ImgSettings : ImgNormal;

        // ─── ページ編集ボタンは Edit モードのみ表示 ─────────────────
        PageEditButton.Visibility = isEdit ? Visibility.Visible : Visibility.Hidden;
        if (isEdit) SetPageBtn(PageEditButton, "🗒", true);

        GlobalSettingsButton.Visibility = Visibility.Hidden;

        // ─── 行2 は Edit モードのみ表示 ──────────────────────────────
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

    private void StartDragSwitchTimer(int pageIndex)
    {
        StopDragSwitchTimer();
        if (_vm == null || pageIndex == _vm.CurrentPageIndex) return;
        _dragSwitchTargetIndex = pageIndex;
        _dragSwitchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _dragSwitchTimer.Tick += (_, _) =>
        {
            StopDragSwitchTimer();
            _vm?.NavigateToPageCommand.Execute(_dragSwitchTargetIndex);
        };
        _dragSwitchTimer.Start();
    }

    private void StopDragSwitchTimer()
    {
        _dragSwitchTimer?.Stop();
        _dragSwitchTimer = null;
    }

    private void RebuildIndicators()
    {
        if (_vm == null) return;
        IndicatorPanel.Children.Clear();

        bool isSettings  = _vm.Mode is AppMode.Settings or AppMode.GlobalSettings;
        var pages        = isSettings ? (IReadOnlyList<PageViewModel>)_vm.SettingsPages : _vm.Pages;
        int currentIndex = isSettings ? _vm.SettingsPageIndex : _vm.CurrentPageIndex;

        for (int i = 0; i < pages.Count; i++)
        {
            var page      = pages[i];
            bool isCurrent = i == currentIndex;
            var color     = isSettings
                ? ColorPalette.GetColor("teal")
                : ColorPalette.GetColor(page.BackgroundColor);
            var brush     = new SolidColorBrush(color);
            int pageIndex = i;

            var indicator = new Grid
            {
                Width      = 18,
                Height     = 18,
                Cursor     = Cursors.Hand,
                Background = Brushes.Transparent,
                Margin     = i < pages.Count - 1
                    ? new Thickness(0, 0, 8, 0)
                    : new Thickness(0),
            };
            indicator.MouseLeftButtonUp += (_, _) =>
            {
                if (isSettings)
                    _vm.NavigateToSettingsPageCommand.Execute(pageIndex);
                else
                    _vm.NavigateToPageCommand.Execute(pageIndex);
            };

            if (isCurrent)
            {
                indicator.Children.Add(new Ellipse { Width = 18, Height = 18, Fill = Brushes.White });
                indicator.Children.Add(new Ellipse
                {
                    Width               = 7,
                    Height              = 7,
                    Fill                = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
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
                    Stroke          = Brushes.White,
                    StrokeThickness = 2,
                    Fill            = Brushes.Transparent,
                });
            }

            // ドラッグホバーでページ切り替え（通常モードのページのみ）
            if (!isSettings)
            {
                var highlight = new Ellipse
                {
                    Width      = 18,
                    Height     = 18,
                    Fill       = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    Visibility = Visibility.Collapsed,
                };
                indicator.Children.Add(highlight);
                indicator.AllowDrop = true;

                int idx = pageIndex;
                indicator.DragEnter += (_, _) =>
                {
                    highlight.Visibility = Visibility.Visible;
                    StartDragSwitchTimer(idx);
                };
                indicator.DragLeave += (_, _) =>
                {
                    highlight.Visibility = Visibility.Collapsed;
                    StopDragSwitchTimer();
                };
                indicator.Drop += (_, _) =>
                {
                    highlight.Visibility = Visibility.Collapsed;
                    StopDragSwitchTimer();
                };
            }

            IndicatorPanel.Children.Add(indicator);
        }
    }
}
