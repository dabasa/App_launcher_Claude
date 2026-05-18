using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AppLauncher.Models;
using AppLauncher.Models.Config;
using AppLauncher.Services;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class SystemTileControl : UserControl
{
    private TileViewModel?    _tile;
    private SystemInfoConfig? _si;
    private Border?           _tileBorder;
    private DispatcherTimer?  _timer;
    private bool              _modeSubscribed;

    public event Action<TileViewModel>? EditRequested;
    public event Action<TileViewModel>? DeleteRequested;
    public event Action<TileViewModel, MouseButtonEventArgs>? ResizeStarted;

    public SystemTileControl()
    {
        InitializeComponent();
        EditButton.MouseLeftButtonUp     += (_, e) => { e.Handled = true; if (_tile != null) EditRequested?.Invoke(_tile); };
        DeleteButton.MouseLeftButtonUp   += (_, e) => { e.Handled = true; if (_tile != null) DeleteRequested?.Invoke(_tile); };
        ResizeHandle.MouseLeftButtonDown += (_, e) => { if (_tile != null) { e.Handled = true; ResizeStarted?.Invoke(_tile, e); } };
    }

    // tileBorder: TileGridControl が生成した角丸・背景付き外枠（アクセント切替で背景を変更する）
    public void Apply(TileViewModel tile, int cornerRadius, Color pageBackgroundColor, Border tileBorder)
    {
        _tile       = tile;
        _si         = tile.SystemInfo;
        _tileBorder = tileBorder;

        if (_si == null) return;

        var mainBrush = ColorPalette.GetBrush(_si.MainColor);
        MainText.Foreground         = mainBrush;
        SubText.Foreground          = mainBrush;
        CircleCenterText.Foreground = mainBrush;
        CircleLabel.Foreground      = mainBrush;
        MainText.FontSize           = tile.FontSizePt * 4.0 / 3.0;
        SubText.FontSize            = Math.Max(10, tile.FontSizePt * 4.0 / 3.0 - 4);
        CircleArc.Stroke            = mainBrush;
    }

    // ─── ライフサイクル ────────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FetchAndUpdate();
        StartTimer();
        SubscribeMode();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        UnsubscribeMode();
    }

    private void StartTimer()
    {
        if (_si == null) return;
        int ms = Math.Clamp(_si.UpdateIntervalMs, 100, 3_600_000);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        _timer.Tick += (_, _) => FetchAndUpdate();
        _timer.Start();
    }

    // ─── 編集モードオーバーレイ ────────────────────────────────────────────
    private void SubscribeMode()
    {
        if (_modeSubscribed || App.LauncherViewModel is not { } vm) return;
        vm.PropertyChanged += OnVmPropertyChanged;
        _modeSubscribed = true;
    }

    private void UnsubscribeMode()
    {
        if (!_modeSubscribed || App.LauncherViewModel is not { } vm) return;
        vm.PropertyChanged -= OnVmPropertyChanged;
        _modeSubscribed = false;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.Mode))
            UpdateEditOverlay();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateEditOverlay();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        EditOverlay.Visibility = Visibility.Collapsed;
    }

    private void UpdateEditOverlay()
    {
        EditOverlay.Visibility = IsMouseOver && App.LauncherViewModel?.Mode == AppMode.Edit
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── クリック（クリック動作：表示更新） ───────────────────────────────
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (App.LauncherViewModel?.Mode != AppMode.Normal) return;
        FetchAndUpdate();
    }

    // ─── データ取得・描画 ──────────────────────────────────────────────────
    private void FetchAndUpdate()
    {
        if (_si == null || _tile == null) return;
        var data = SystemInfoService.Instance.GetData(_si);
        Render(data);
    }

    private void Render(SystemData data)
    {
        if (_si == null || _tile == null) return;

        bool isCircle = _si.DisplayFormat == "circle" &&
                        _si.Category is "storage" or "usage";

        // アクセント切替
        var mainBrush = ColorPalette.GetBrush(
            data.ThresholdExceeded ? _si.AccentColor : _si.MainColor);

        // タイル背景アクセント
        if (_tileBorder != null)
        {
            var bgColorName = data.ThresholdExceeded ? _si.BackgroundAccent : _tile.Color;
            var bgColor     = ColorPalette.GetColor(bgColorName);
            double alpha    = ColorPalette.OpacityToDouble(_tile.Opacity);
            _tileBorder.Background = new SolidColorBrush(
                Color.FromArgb((byte)(255 * alpha), bgColor.R, bgColor.G, bgColor.B));
        }

        if (isCircle)
        {
            TextPanel.Visibility   = Visibility.Collapsed;
            CirclePanel.Visibility = Visibility.Visible;

            // 円弧計算：Ellipse Width=60, StrokeThickness=8 → 中心線 radius=26
            const double strokeT     = 8.0;
            const double radius      = 26.0;
            double circumference     = 2 * Math.PI * radius / strokeT;
            double pct               = Math.Clamp(data.Percentage / 100.0, 0.0, 1.0);
            double used              = pct * circumference;
            double unused            = circumference - used;

            CircleArc.Stroke          = mainBrush;
            CircleArc.StrokeDashArray = new DoubleCollection([used, unused]);
            CircleCenterText.Text     = $"{data.Percentage:F0}%";
            CircleCenterText.Foreground = mainBrush;
            CircleLabel.Text          = data.SubText;
            CircleLabel.Foreground    = mainBrush;
        }
        else
        {
            TextPanel.Visibility   = Visibility.Visible;
            CirclePanel.Visibility = Visibility.Collapsed;
            MainText.Text          = data.MainText;
            SubText.Text           = data.SubText;
            MainText.Foreground    = mainBrush;
        }
    }
}
