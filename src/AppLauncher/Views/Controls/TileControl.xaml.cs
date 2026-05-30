using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AppLauncher.Models;
using AppLauncher.Services;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class TileControl : UserControl
{
    private TileViewModel? _tile;
    private Color _pageBackgroundColor;
    private Point _mouseDownPos;
    private bool _dragStarted;

    // GIF 再生用
    private GifBitmapDecoder? _gifDecoder;
    private int               _gifFrameIndex;
    private DispatcherTimer?  _gifTimer;
    private string            _imagePath = "";

    public event Action<TileViewModel>? EditRequested;
    public event Action<TileViewModel>? DeleteRequested;
    public event Action<TileViewModel>? CopyRequested;
    public event Action<TileViewModel, MouseButtonEventArgs>? ResizeStarted;

    public TileControl()
    {
        InitializeComponent();
        EditButton.MouseLeftButtonUp     += OnEditButtonClick;
        DeleteButton.MouseLeftButtonUp   += OnDeleteButtonClick;
        CopyButton.MouseLeftButtonUp     += OnCopyButtonClick;
        ResizeHandle.MouseLeftButtonDown += OnResizeHandleMouseDown;

        AllowDrop = true;
        Drop      += OnFileDrop;
        Loaded    += OnLoaded;
        Unloaded  += OnUnloaded;
    }

    public void Apply(TileViewModel tile, int cornerRadius, Color pageBackgroundColor)
    {
        _tile = tile;
        _pageBackgroundColor = pageBackgroundColor;

        var bgColor = ColorPalette.GetColor(tile.Color);
        double alpha = ColorPalette.OpacityToDouble(tile.Opacity);
        TileBorder.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), bgColor.R, bgColor.G, bgColor.B));
        TileBorder.CornerRadius = new CornerRadius(cornerRadius);

        TitleText.Text       = tile.Title;
        TitleText.Foreground = new SolidColorBrush(ColorPalette.GetColor(tile.FontColor));
        if (!string.IsNullOrEmpty(tile.FontName))
            TitleText.FontFamily = new FontFamily(tile.FontName);
        else
            TitleText.ClearValue(TextBlock.FontFamilyProperty);
        UpdateFontSize();

        // 画像表示・レイアウト設定
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

        ArrangeImageAndText(tile.ImagePosition ?? "top", hasImage);
    }

    private void ArrangeImageAndText(string position, bool hasImage)
    {
        // デフォルト：テキストが全領域を占有（"bottom" で変更した Row 0 を必ず * に戻す）
        LayoutGrid.RowDefinitions[0].Height   = new GridLength(1, GridUnitType.Star);
        LayoutGrid.RowDefinitions[1].Height   = new GridLength(0);
        LayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
        Grid.SetRow(TitleText, 0);    Grid.SetRowSpan(TitleText, 2);
        Grid.SetColumn(TitleText, 0); Grid.SetColumnSpan(TitleText, 2);
        Grid.SetRow(TileImage, 0);    Grid.SetRowSpan(TileImage, 1);
        Grid.SetColumn(TileImage, 0); Grid.SetColumnSpan(TileImage, 1);

        if (!hasImage) return;

        Grid.SetRowSpan(TitleText, 1);
        Grid.SetColumnSpan(TitleText, 1);

        switch (position)
        {
            case "top":
                // Row 0 (*): 画像（制約あり）、Row 1 (Auto): テキスト（必要分）
                LayoutGrid.RowDefinitions[1].Height  = GridLength.Auto;
                LayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
                Grid.SetColumnSpan(TileImage, 2);
                Grid.SetColumnSpan(TitleText, 2);
                Grid.SetRow(TileImage, 0);
                Grid.SetRow(TitleText, 1);
                Grid.SetColumn(TileImage, 0);
                Grid.SetColumn(TitleText, 0);
                break;

            case "bottom":
                // Row 0 (Auto): テキスト（必要分）、Row 1 (*): 画像（制約あり）
                // 画像を Auto 行に置くと自然サイズで測定されテキスト行が潰れるため逆にする
                LayoutGrid.RowDefinitions[0].Height  = GridLength.Auto;
                LayoutGrid.RowDefinitions[1].Height  = new GridLength(1, GridUnitType.Star);
                LayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
                Grid.SetColumnSpan(TileImage, 2);
                Grid.SetColumnSpan(TitleText, 2);
                Grid.SetRow(TitleText, 0);
                Grid.SetRow(TileImage, 1);
                Grid.SetColumn(TileImage, 0);
                Grid.SetColumn(TitleText, 0);
                break;

            case "left":
            case "right":
                LayoutGrid.RowDefinitions[1].Height   = new GridLength(0);
                LayoutGrid.ColumnDefinitions[0].Width  = new GridLength(1, GridUnitType.Star);
                LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(1, GridUnitType.Star);
                Grid.SetRowSpan(TileImage, 2);
                Grid.SetRowSpan(TitleText, 2);
                bool imgLeft = position == "left";
                Grid.SetColumn(TileImage, imgLeft ? 0 : 1);
                Grid.SetColumn(TitleText, imgLeft ? 1 : 0);
                Grid.SetRow(TileImage, 0);
                Grid.SetRow(TitleText, 0);
                break;

            case "center":
                // 画像とテキストを同一領域に重ねる。TitleText の Panel.ZIndex=1 でテキストが前面
                Grid.SetRowSpan(TileImage, 2);
                Grid.SetColumnSpan(TileImage, 2);
                Grid.SetRowSpan(TitleText, 2);
                Grid.SetColumnSpan(TitleText, 2);
                Grid.SetRow(TileImage, 0);    Grid.SetColumn(TileImage, 0);
                Grid.SetRow(TitleText, 0);    Grid.SetColumn(TitleText, 0);
                break;
        }
    }

    // ─── GIF タイマー ─────────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SizeChanged += OnSizeChanged;
        UpdateFontSize();
        if (_tile?.AutoFontSize == true && TileBorder.ActualWidth <= 0)
            Dispatcher.BeginInvoke(UpdateFontSize, DispatcherPriority.Loaded);

        if (string.IsNullOrEmpty(_imagePath)) return;

        string ext = Path.GetExtension(_imagePath).ToLowerInvariant();
        if (ext == ".gif")
        {
            StartGif(_imagePath);
        }
        else
        {
            try { TileImage.Source = new BitmapImage(new Uri(_imagePath, UriKind.Absolute)); }
            catch { TileImage.Source = null; }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        StopGif();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_tile?.AutoFontSize == true) UpdateFontSize();
    }

    private void UpdateFontSize()
    {
        if (_tile == null) return;
        if (!_tile.AutoFontSize)
        {
            TitleText.FontSize = _tile.FontSizePt * 4.0 / 3.0;
            return;
        }
        // AutoFontSize=true: set specified size as initial placeholder so the tile
        // never shows at the inherited default before layout completes.
        TitleText.FontSize = _tile.FontSizePt * 4.0 / 3.0;
        if (TileBorder.ActualWidth <= 0 || TileBorder.ActualHeight <= 0) return;

        const double pad = 16.0;
        double w = TileBorder.ActualWidth  - pad;
        double h = TileBorder.ActualHeight - pad;
        bool hasImage = !string.IsNullOrEmpty(_tile.ImagePath);
        if (hasImage)
        {
            switch (_tile.ImagePosition ?? "top")
            {
                case "top":
                case "bottom":
                    h = (TileBorder.ActualHeight - pad) * 0.35;
                    break;
                case "left":
                case "right":
                    w = TileBorder.ActualWidth / 2.0 - pad;
                    break;
                // "center": 画像とテキストが重なるため全領域をそのまま使う
            }
        }

        TitleText.FontSize = CalcOverflowFontSize(_tile.Title, w, h) * 4.0 / 3.0;
    }

    private double CalcOverflowFontSize(string text, double w, double h)
    {
        if (string.IsNullOrEmpty(text) || w <= 0 || h <= 0) return 6;

        // FormattedText は WPF TextBlock の実描画より行高が小さく出るため（FontFamily.LineSpacing 分の誤差）、
        // TextBlock を直接 Measure して正確に収まりを判定する
        var measure = new TextBlock
        {
            Text         = text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily   = TitleText.FontFamily,
            FontStyle    = TitleText.FontStyle,
            FontWeight   = TitleText.FontWeight,
            FontStretch  = TitleText.FontStretch,
            Padding      = new Thickness(8),
        };
        // w/h はパディング(8×2=16)を除いたコンテンツ領域。+16 で TextBlock 全体幅・高さに戻す
        double availW = w + 16;
        double availH = h + 16;

        double startPt = _tile != null ? Math.Clamp(_tile.FontSizePt, 6, 72) : 72;
        for (double size = startPt; size >= 6; size--)
        {
            measure.FontSize = size * 4.0 / 3.0;
            measure.Measure(new Size(availW, double.PositiveInfinity));
            if (measure.DesiredSize.Height <= availH) return size;
        }
        return 6;
    }

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

    // ─── D&D ファイルドロップ（通常モード・D&D 専用タイルのみ） ─────────────
    private void OnFileDrop(object sender, DragEventArgs e)
    {
        if (_tile == null) return;
        if (App.LauncherViewModel?.Mode != AppMode.Normal) return;
        if (!_tile.Args.Contains("{drop}")) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        TileLaunchService.LaunchWithDrop(_tile, paths);
    }

    // ─── ボタン・ハンドル ─────────────────────────────────────────────────
    private void OnEditButtonClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_tile != null) EditRequested?.Invoke(_tile);
    }

    private void OnDeleteButtonClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_tile != null) DeleteRequested?.Invoke(_tile);
    }

    private void OnCopyButtonClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_tile != null) CopyRequested?.Invoke(_tile);
    }

    private void OnResizeHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_tile == null) return;
        e.Handled = true;
        ResizeStarted?.Invoke(_tile, e);
    }

    // ─── マウスボタン ─────────────────────────────────────────────────────
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        e.Handled = true;
    }

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

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_tile == null) return;

        var mode = App.LauncherViewModel?.Mode;
        // 通常モード：全タイル起動可、設定モード：settings タイルのみ起動可
        bool canLaunch = mode == AppMode.Normal
                      || (mode == AppMode.Settings && _tile.Type == "settings");
        if (!canLaunch) return;

        if ((e.GetPosition(this) - _mouseDownPos).Length < 5.0)
        {
            e.Handled = true;
            TileLaunchService.Launch(_tile);
        }
    }

    // ─── ホバー ───────────────────────────────────────────────────────────
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (_tile == null) return;

        var mode = App.LauncherViewModel?.Mode;

        if (mode == AppMode.Edit)
        {
            EditOverlay.Visibility = Visibility.Visible;
            return;
        }
        if (mode != AppMode.Normal && mode != AppMode.Settings) return;

        if (_tile.Args.Contains("{drop}"))
        {
            DndOverlay.Background = new SolidColorBrush(_pageBackgroundColor);
            DndOverlay.Visibility = Visibility.Visible;
            return;
        }

        Panel.SetZIndex(this, 100);
        AnimateScale(1.13);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        EditOverlay.Visibility = Visibility.Collapsed;
        DndOverlay.Visibility  = Visibility.Collapsed;
        Panel.SetZIndex(this, 0);
        AnimateScale(1.0);
    }

    private void AnimateScale(double to)
    {
        var duration = TimeSpan.FromMilliseconds(120);
        HoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, duration));
        HoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, duration));
    }
}
