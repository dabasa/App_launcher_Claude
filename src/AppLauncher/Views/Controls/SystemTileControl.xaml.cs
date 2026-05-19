using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private Point             _mouseDownPos;
    private bool              _dragStarted;

    // 画像・GIF 再生用
    private string            _imagePath = "";
    private GifBitmapDecoder? _gifDecoder;
    private int               _gifFrameIndex;
    private DispatcherTimer?  _gifTimer;

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

        // 画像設定
        _imagePath = tile.ImagePath ?? "";
        SetupImageAndLayout(tile);
    }

    // ─── 画像レイアウト ────────────────────────────────────────────────────
    private void SetupImageAndLayout(TileViewModel tile)
    {
        bool hasImage = !string.IsNullOrEmpty(tile.ImagePath);
        TileImage.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;

        if (hasImage)
        {
            TileImage.Opacity = tile.ImageTransparent
                ? ColorPalette.OpacityToDouble(tile.Opacity)
                : 1.0;
        }

        ArrangeImageAndContent(tile.ImagePosition ?? "top", hasImage);
    }

    private void ArrangeImageAndContent(string position, bool hasImage)
    {
        // デフォルト：ContentGrid が全領域を占有
        LayoutGrid.RowDefinitions[0].Height    = new GridLength(1, GridUnitType.Star);
        LayoutGrid.RowDefinitions[1].Height    = new GridLength(0);
        LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(0);
        Grid.SetRow(ContentGrid, 0);    Grid.SetRowSpan(ContentGrid, 2);
        Grid.SetColumn(ContentGrid, 0); Grid.SetColumnSpan(ContentGrid, 2);
        Grid.SetRow(TileImage, 0);    Grid.SetRowSpan(TileImage, 1);
        Grid.SetColumn(TileImage, 0); Grid.SetColumnSpan(TileImage, 1);

        if (!hasImage) return;

        Grid.SetRowSpan(ContentGrid, 1);
        Grid.SetColumnSpan(ContentGrid, 1);

        switch (position)
        {
            case "top":
                LayoutGrid.RowDefinitions[1].Height    = GridLength.Auto;
                LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(0);
                Grid.SetColumnSpan(TileImage, 2);
                Grid.SetColumnSpan(ContentGrid, 2);
                Grid.SetRow(TileImage, 0);
                Grid.SetRow(ContentGrid, 1);
                Grid.SetColumn(TileImage, 0);
                Grid.SetColumn(ContentGrid, 0);
                break;

            case "bottom":
                LayoutGrid.RowDefinitions[0].Height    = GridLength.Auto;
                LayoutGrid.RowDefinitions[1].Height    = new GridLength(1, GridUnitType.Star);
                LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(0);
                Grid.SetColumnSpan(TileImage, 2);
                Grid.SetColumnSpan(ContentGrid, 2);
                Grid.SetRow(ContentGrid, 0);
                Grid.SetRow(TileImage, 1);
                Grid.SetColumn(TileImage, 0);
                Grid.SetColumn(ContentGrid, 0);
                break;

            case "left":
            case "right":
                LayoutGrid.RowDefinitions[1].Height    = new GridLength(0);
                LayoutGrid.ColumnDefinitions[0].Width  = new GridLength(1, GridUnitType.Star);
                LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(1, GridUnitType.Star);
                Grid.SetRowSpan(TileImage, 2);
                Grid.SetRowSpan(ContentGrid, 2);
                bool imgLeft = position == "left";
                Grid.SetColumn(TileImage, imgLeft ? 0 : 1);
                Grid.SetColumn(ContentGrid, imgLeft ? 1 : 0);
                Grid.SetRow(TileImage, 0);
                Grid.SetRow(ContentGrid, 0);
                break;

            case "center":
                // 画像が全体を覆い、ContentGrid (Panel.ZIndex=1) が前面
                Grid.SetRowSpan(TileImage, 2);
                Grid.SetColumnSpan(TileImage, 2);
                Grid.SetRowSpan(ContentGrid, 2);
                Grid.SetColumnSpan(ContentGrid, 2);
                Grid.SetRow(TileImage, 0);    Grid.SetColumn(TileImage, 0);
                Grid.SetRow(ContentGrid, 0);  Grid.SetColumn(ContentGrid, 0);
                break;
        }
    }

    // ─── GIF タイマー ─────────────────────────────────────────────────────
    private void StartGif(string path)
    {
        try
        {
            _gifDecoder = new GifBitmapDecoder(
                new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (_gifDecoder.Frames.Count == 0) return;

            _gifFrameIndex   = 0;
            TileImage.Source = _gifDecoder.Frames[0];

            if (_gifDecoder.Frames.Count > 1)
            {
                _gifTimer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(100),
                };
                _gifTimer.Tick += (_, _) =>
                {
                    if (_gifDecoder == null) return;
                    _gifFrameIndex   = (_gifFrameIndex + 1) % _gifDecoder.Frames.Count;
                    TileImage.Source = _gifDecoder.Frames[_gifFrameIndex];
                };
                _gifTimer.Start();
            }
        }
        catch { }
    }

    private void StopGif()
    {
        _gifTimer?.Stop();
        _gifTimer   = null;
        _gifDecoder = null;
    }

    // ─── ライフサイクル ────────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_imagePath))
        {
            string ext = Path.GetExtension(_imagePath).ToLowerInvariant();
            if (ext == ".gif")
                StartGif(_imagePath);
            else
            {
                try { TileImage.Source = new BitmapImage(new Uri(_imagePath, UriKind.Absolute)); }
                catch { TileImage.Source = null; }
            }
        }

        FetchAndUpdate();
        StartTimer();
        SubscribeMode();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopGif();
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

    // ─── タイル移動 D&D ──────────────────────────────────────────────────
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _mouseDownPos = e.GetPosition(this);
        _dragStarted  = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (App.LauncherViewModel?.Mode != AppMode.Edit) return;
        if (_tile == null || _dragStarted) return;
        if ((e.GetPosition(this) - _mouseDownPos).Length < 5.0) return;

        _dragStarted = true;
        DragDrop.DoDragDrop(this, _tile, DragDropEffects.Move);
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
