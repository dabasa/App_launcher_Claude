using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
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

    // TOPプロセス Grid テーブル（コードビハインドで動的生成）
    private Border?        _topProcessPanel;
    private TextBlock[,]?  _topProcCells;  // [row 0=header / 1-3=data, col 0-3]

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

        var arcBrush   = ColorPalette.GetBrush(_si.MainColor);

        // タイトルフォント設定
        var titleBrush = new SolidColorBrush(ColorPalette.GetColor(tile.TitleFont.FontColor));
        double titlePx = tile.TitleFont.FontSizePt * 4.0 / 3.0;
        TitleText.Text       = tile.Title ?? "";
        TitleText.FontSize   = titlePx;
        TitleText.Foreground = titleBrush;
        if (!string.IsNullOrEmpty(tile.TitleFont.FontName))
            TitleText.FontFamily = new FontFamily(tile.TitleFont.FontName);

        // コンテンツフォント設定（ContentFont があれば優先、なければ TitleFont を流用）
        var cf        = tile.ContentFont ?? tile.TitleFont;
        double cfPx   = cf.FontSizePt * 4.0 / 3.0;
        var cfBrush   = new SolidColorBrush(ColorPalette.GetColor(cf.FontColor));
        if (!string.IsNullOrEmpty(cf.FontName))
        {
            var cfFamily = new FontFamily(cf.FontName);
            MainText.FontFamily         = cfFamily;
            SubText.FontFamily          = cfFamily;
            CircleCenterText.FontFamily = cfFamily;
            CircleLabel.FontFamily      = cfFamily;
        }
        MainText.FontSize         = cfPx;
        SubText.FontSize          = Math.Max(10, cfPx - 4);
        CircleCenterText.FontSize = cfPx;
        CircleLabel.FontSize      = Math.Max(8, cfPx - 4);
        MainText.Foreground         = cfBrush;
        UpdateContentFontSize();
        SubText.Foreground          = cfBrush;
        CircleCenterText.Foreground = cfBrush;
        CircleLabel.Foreground      = cfBrush;
        CircleArc.Stroke            = arcBrush;

        // 画像設定
        _imagePath = tile.ImagePath ?? "";
        SetupImageAndLayout(tile);

        if (_si.Category == "top_process")
        {
            EnsureTopProcessPanel();
            ApplyTopProcFont();
        }
    }

    // ─── 画像レイアウト ────────────────────────────────────────────────────
    private void SetupImageAndLayout(TileViewModel tile)
    {
        bool hasImage = !string.IsNullOrEmpty(tile.ImagePath);
        bool hasTitle = !string.IsNullOrEmpty(tile.Title);
        TileImage.Visibility  = hasImage ? Visibility.Visible  : Visibility.Collapsed;
        TitleText.Visibility  = hasTitle ? Visibility.Visible  : Visibility.Collapsed;

        if (hasImage)
        {
            TileImage.Opacity = tile.ImageTransparent
                ? ColorPalette.OpacityToDouble(tile.Opacity)
                : 1.0;
        }

        ArrangeImageAndContent(tile.ImagePosition ?? "top", hasImage, hasTitle);
    }

    private void ArrangeImageAndContent(string position, bool hasImage, bool hasTitle)
    {
        // デフォルト：ContentGrid が全領域を占有
        LayoutGrid.RowDefinitions[0].Height    = new GridLength(1, GridUnitType.Star);
        LayoutGrid.RowDefinitions[1].Height    = new GridLength(0);
        LayoutGrid.ColumnDefinitions[1].Width  = new GridLength(0);
        Grid.SetRow(ContentGrid, 0);    Grid.SetRowSpan(ContentGrid, 2);
        Grid.SetColumn(ContentGrid, 0); Grid.SetColumnSpan(ContentGrid, 2);
        Grid.SetRow(TileImage, 0);    Grid.SetRowSpan(TileImage, 1);
        Grid.SetColumn(TileImage, 0); Grid.SetColumnSpan(TileImage, 1);
        Grid.SetRow(TitleText, 0);    Grid.SetRowSpan(TitleText, 1);
        Grid.SetColumn(TitleText, 0); Grid.SetColumnSpan(TitleText, 1);
        TitleText.HorizontalAlignment = HorizontalAlignment.Center;
        TitleText.VerticalAlignment   = VerticalAlignment.Center;

        if (!hasImage && !hasTitle) return;

        Grid.SetRowSpan(ContentGrid, 1);
        Grid.SetColumnSpan(ContentGrid, 1);

        // 画像エリアのサイズ: 画像ありなら*（画像優先）、タイトルのみならAuto
        var imageAreaRowLen   = hasImage ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        var contentAreaRowLen = hasImage ? GridLength.Auto : new GridLength(1, GridUnitType.Star);

        switch (position)
        {
            case "top":
                LayoutGrid.RowDefinitions[0].Height   = imageAreaRowLen;
                LayoutGrid.RowDefinitions[1].Height   = contentAreaRowLen;
                LayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
                Grid.SetColumnSpan(TileImage, 2);
                Grid.SetColumnSpan(TitleText, 2);
                Grid.SetColumnSpan(ContentGrid, 2);
                Grid.SetRow(TileImage, 0);    Grid.SetColumn(TileImage, 0);
                Grid.SetRow(TitleText, 0);    Grid.SetColumn(TitleText, 0);
                Grid.SetRow(ContentGrid, 1);  Grid.SetColumn(ContentGrid, 0);
                break;

            case "bottom":
                LayoutGrid.RowDefinitions[0].Height   = contentAreaRowLen;
                LayoutGrid.RowDefinitions[1].Height   = imageAreaRowLen;
                LayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
                Grid.SetColumnSpan(TileImage, 2);
                Grid.SetColumnSpan(TitleText, 2);
                Grid.SetColumnSpan(ContentGrid, 2);
                Grid.SetRow(ContentGrid, 0); Grid.SetColumn(ContentGrid, 0);
                Grid.SetRow(TileImage, 1);   Grid.SetColumn(TileImage, 0);
                Grid.SetRow(TitleText, 1);   Grid.SetColumn(TitleText, 0);
                break;

            case "left":
            case "right":
                LayoutGrid.RowDefinitions[1].Height   = new GridLength(0);
                bool imgLeft = position == "left";
                LayoutGrid.ColumnDefinitions[imgLeft ? 0 : 1].Width = hasImage
                    ? new GridLength(1, GridUnitType.Star)
                    : GridLength.Auto;
                LayoutGrid.ColumnDefinitions[imgLeft ? 1 : 0].Width = new GridLength(1, GridUnitType.Star);
                Grid.SetRowSpan(TileImage, 2);
                Grid.SetRowSpan(TitleText, 2);
                Grid.SetRowSpan(ContentGrid, 2);
                Grid.SetColumn(TileImage,   imgLeft ? 0 : 1);
                Grid.SetColumn(TitleText,   imgLeft ? 0 : 1);
                Grid.SetColumn(ContentGrid, imgLeft ? 1 : 0);
                Grid.SetRow(TileImage, 0);
                Grid.SetRow(TitleText, 0);
                Grid.SetRow(ContentGrid, 0);
                break;

            case "center":
                // 画像が全体を覆い ContentGrid(ZIndex=1) が前面、TitleText(ZIndex=2) は上部に
                Grid.SetRowSpan(TileImage, 2);    Grid.SetColumnSpan(TileImage, 2);
                Grid.SetRowSpan(ContentGrid, 2);  Grid.SetColumnSpan(ContentGrid, 2);
                Grid.SetRowSpan(TitleText, 2);    Grid.SetColumnSpan(TitleText, 2);
                Grid.SetRow(TileImage, 0);    Grid.SetColumn(TileImage, 0);
                Grid.SetRow(ContentGrid, 0);  Grid.SetColumn(ContentGrid, 0);
                Grid.SetRow(TitleText, 0);    Grid.SetColumn(TitleText, 0);
                TitleText.VerticalAlignment = VerticalAlignment.Top;
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

        SizeChanged += OnSizeChanged;
        UpdateAllFontSizes();
        FetchAndUpdate();
        StartTimer();
        SubscribeMode();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        StopGif();
        _timer?.Stop();
        _timer = null;
        UnsubscribeMode();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateAllFontSizes();

    private void UpdateAllFontSizes()
    {
        UpdateTitleFontSize();
        if (_si?.Category == "top_process")
            UpdateTopProcFontSize();
        else
        {
            if (CirclePanel.Visibility == Visibility.Visible)
                UpdateCircleLayout();
            else
                UpdateContentFontSize();
        }
    }

    // ─── タイトルフォント自動調整 ──────────────────────────────────────────
    private void UpdateTitleFontSize()
    {
        if (_tile == null || !_tile.TitleFont.AutoFontSize) return;
        if (TitleText.Visibility != Visibility.Visible) return;

        double totalW = ActualWidth;
        double totalH = ActualHeight;
        if (totalW <= 0 || totalH <= 0) return;

        string pos = _tile.ImagePosition ?? "top";
        // left/right: タイトル列はタイル幅の半分、高さはフル
        // それ以外:   タイトル行はタイル高さの35%、幅はフル
        double availW = pos is "left" or "right" ? totalW / 2.0 : totalW;
        double availH = pos is "left" or "right" ? totalH       : totalH * 0.35;

        var measure = new TextBlock
        {
            Text         = TitleText.Text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily   = TitleText.FontFamily,
            FontStyle    = TitleText.FontStyle,
            FontWeight   = TitleText.FontWeight,
            FontStretch  = TitleText.FontStretch,
            Padding      = TitleText.Padding,
        };
        double startPt = Math.Clamp(_tile.TitleFont.FontSizePt, 6, 72);
        for (double size = startPt; size >= 6; size--)
        {
            measure.FontSize = size * 4.0 / 3.0;
            measure.Measure(new Size(availW, double.PositiveInfinity));
            if (measure.DesiredSize.Height <= availH)
            {
                TitleText.FontSize = size * 4.0 / 3.0;
                return;
            }
        }
        TitleText.FontSize = 6 * 4.0 / 3.0;
    }

    // ─── コンテンツフォント自動調整 ────────────────────────────────────────
    private void UpdateContentFontSize()
    {
        if (_tile == null) return;
        var cf = _tile.ContentFont ?? _tile.TitleFont;

        // 円グラフはキャンバス固定サイズのため AutoFontSize に関わらず常に再フィット
        if (TextPanel.Visibility != Visibility.Visible)
        {
            UpdateCircleLayout();
            return;
        }

        // テキストモードは AutoFontSize フラグに従う
        if (!cf.AutoFontSize) return;

        double totalW = ActualWidth;
        double totalH = ActualHeight;
        if (totalW <= 0 || totalH <= 0) return;

        string pos      = _tile.ImagePosition ?? "top";
        bool   hasTitle = !string.IsNullOrEmpty(_tile.Title);

        // コンテンツ領域のサイズ見積もり
        // left/right: コンテンツ列はタイル幅の半分、高さはフル
        // それ以外でタイトルあり: タイル高さの65%（残り35%がタイトル分）
        double availW = pos is "left" or "right" ? totalW / 2.0 : totalW;
        double availH = (pos is "left" or "right" || !hasTitle) ? totalH : totalH * 0.65;

        // テキストが未設定（初回フェッチ前）は指定サイズをそのまま使う
        if (string.IsNullOrEmpty(MainText.Text))
        {
            double cfPx = cf.FontSizePt * 4.0 / 3.0;
            MainText.FontSize = cfPx;
            SubText.FontSize  = Math.Max(10, cfPx - 4);
            return;
        }

        // StackPanel Margin="8" が両側に付くため内部幅を縮小
        double innerW  = Math.Max(0, availW - 16);
        double startPt = Math.Clamp(cf.FontSizePt, 6, 72);
        for (double size = startPt; size >= 6; size--)
        {
            double mainPx = size * 4.0 / 3.0;
            double subPx  = Math.Max(10, mainPx - 4);

            var mMain = new TextBlock
            {
                Text         = MainText.Text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily   = MainText.FontFamily,
                FontWeight   = FontWeights.SemiBold,
                FontSize     = mainPx,
            };
            var mSub = new TextBlock
            {
                Text         = SubText.Text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily   = SubText.FontFamily,
                FontSize     = subPx,
            };
            mMain.Measure(new Size(innerW, double.PositiveInfinity));
            mSub.Measure(new Size(innerW, double.PositiveInfinity));

            // StackPanel の Margin=8(上下) + SubText の Margin="0,2,0,0" 分を加算
            double totalNeeded = mMain.DesiredSize.Height + mSub.DesiredSize.Height + 18;
            if (totalNeeded <= availH)
            {
                MainText.FontSize = mainPx;
                SubText.FontSize  = subPx;
                return;
            }
        }
        MainText.FontSize = 6 * 4.0 / 3.0;
        SubText.FontSize  = 10;
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

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        e.Handled = true;
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
    private async void FetchAndUpdate()
    {
        if (_si == null || _tile == null) return;
        var si = _si;

        if (si.Category == "top_process")
        {
            var entries = await Task.Run(() => SystemInfoService.Instance.GetTopProcessEntries());
            if (_si == si) RenderTopProcess(entries);
        }
        else
        {
            var data = await Task.Run(() => SystemInfoService.Instance.GetData(si));
            if (_si == si) Render(data);
        }
    }

    private void Render(SystemData data)
    {
        if (_si == null || _tile == null) return;

        // top_process カテゴリ以外から呼ばれた場合は Grid パネルを隠す
        if (_topProcessPanel != null)
            _topProcessPanel.Visibility = Visibility.Collapsed;

        bool isCircle = _si.DisplayFormat == "circle" &&
                        (_si.Category == "storage" || (_si.Category == "usage" && _si.DeviceType != "lan"));

        // アクセント切替（グラフ用色 / テキスト用色を分離）
        var arcBrush  = ColorPalette.GetBrush(
            data.ThresholdExceeded ? _si.AccentColor : _si.MainColor);
        // コンテンツフォントカラー（ContentFont 優先、なければ TitleFont を流用）
        var cf        = _tile.ContentFont ?? _tile.TitleFont;
        var textBrush = new SolidColorBrush(ColorPalette.GetColor(cf.FontColor));

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

            CircleArc.Stroke            = arcBrush;
            CircleArc.StrokeDashArray   = new DoubleCollection([used, unused]);
            CircleCenterText.Text       = $"{data.Percentage:F0}%";
            CircleCenterText.Foreground = textBrush;
            CircleLabel.Text            = data.SubText;
            CircleLabel.Foreground      = textBrush;
            UpdateCircleLayout();
        }
        else
        {
            TextPanel.Visibility   = Visibility.Visible;
            CirclePanel.Visibility = Visibility.Collapsed;

            MainText.TextAlignment        = TextAlignment.Center;
            SubText.Visibility            = Visibility.Visible;
            TextPanel.HorizontalAlignment = HorizontalAlignment.Center;

            MainText.Text       = data.MainText;
            SubText.Text        = data.SubText;
            MainText.Foreground = textBrush;
            SubText.Foreground  = textBrush;
            // テキスト内容が確定したタイミングでフォントサイズを再計算
            UpdateContentFontSize();
        }
    }

    // ─── 円グラフ Canvas スケール + フォントサイズを一括調整 ─────────────────
    // ① Canvas を LayoutTransform で縮小してタイルからのはみ出しを防ぐ
    // ② フォントサイズは「描画後の実ピクセル」が統一されるよう scale を補正する
    // ③ 常に "100%" を基準に計測 → タイル間でサイズが揃う
    private void UpdateCircleLayout()
    {
        if (_tile == null) return;

        // ── Step 1: Canvas scale ────────────────────────────────────────────
        double tileH = ActualHeight;
        double tileW = ActualWidth;

        bool   hasTitle = TitleText.Visibility == Visibility.Visible;
        string pos      = _tile.ImagePosition ?? "top";

        // タイトル高さを差し引いてコンテンツエリアを算出
        double titleH  = hasTitle && pos is not "left" and not "right"
            ? Math.Max(TitleText.ActualHeight, 20.0) : 0.0;
        double contentH = tileH > 0 ? tileH - titleH : 80.0;
        double contentW = pos is "left" or "right" ? (tileW > 0 ? tileW * 0.5 : 80.0) : (tileW > 0 ? tileW : 80.0);

        const double native = 80.0;
        const double margin = 8.0;
        double maxSide = Math.Min(contentH - margin, contentW - margin);
        double scale   = maxSide > 0 ? Math.Min(1.0, maxSide / native) : 1.0;

        CirclePanel.LayoutTransform = scale < 1.0
            ? new ScaleTransform(scale, scale)
            : Transform.Identity;

        // ── Step 2: フォントサイズ（描画後の実ピクセルを統一） ───────────────
        // canvas 座標での font_px × scale = 画面上の描画サイズ
        // → font_px = target_screen_px / scale
        const double targetScreenH = 20.0;  // 画面上の目標高さ (px)
        const double targetScreenW = 40.0;  // 画面上の目標幅  (px)
        double canvasMaxH = targetScreenH / scale;
        double canvasMaxW = targetScreenW / scale;

        var typeface = new Typeface(
            CircleCenterText.FontFamily,
            FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        double ppd = 1.0;
        try { ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }

        string text = CircleCenterText.Text;
        if (!string.IsNullOrEmpty(text))
        {
            for (double pt = 72; pt >= 6; pt--)
            {
                double px = pt * 4.0 / 3.0;
                var ft = new FormattedText(
                    "100%",   // 常に最長値で計測 → 全タイルで統一サイズ
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight, typeface, px, Brushes.White, ppd);

                if (ft.Width <= canvasMaxW && ft.Height <= canvasMaxH)
                {
                    CircleCenterText.FontSize = px;
                    CircleLabel.FontSize      = Math.Max(8.0, px * 0.75);
                    return;
                }
            }
        }
        CircleCenterText.FontSize = 6 * 4.0 / 3.0;
        CircleLabel.FontSize      = 8.0;
    }

    // ─── TOPプロセス Grid テーブル ─────────────────────────────────────────
    private void UpdateTopProcFontSize()
    {
        if (_tile == null || _topProcCells == null) return;
        if (_topProcessPanel?.Visibility != Visibility.Visible) return;

        double totalH = ActualHeight;
        if (totalH <= 0) return;

        // タイトルが上下にある場合はコンテンツ領域を 65% と見積もる
        bool hasTitle = TitleText.Visibility == Visibility.Visible;
        string pos = _tile.ImagePosition ?? "top";
        double availH = hasTitle && pos is not "left" and not "right"
            ? totalH * 0.65 : totalH;

        // 外枠 Margin(4×2=8) + Border(1×2=2) + 行区切り線(3本) を除いた高さ
        availH -= 13;

        // 4行に均等割り: 1行高さ = fontPx × 1.2(行高係数) + 縦パディング 2px
        double rowH   = availH / 4.0;
        double fontPx = Math.Max(6, (rowH - 2) / 1.2);

        // 設定値より大きくしない
        var cf = _tile.ContentFont ?? _tile.TitleFont;
        fontPx = Math.Min(fontPx, cf.FontSizePt * 4.0 / 3.0);

        for (int r = 0; r < 4; r++)
        for (int c = 0; c < 4; c++)
            _topProcCells[r, c].FontSize = fontPx;
    }

    private void EnsureTopProcessPanel()
    {
        if (_topProcessPanel != null) return;

        var lineBrush = new SolidColorBrush(Color.FromArgb(100, 240, 240, 240));

        var innerGrid = new Grid();
        // 比率で幅を配分: Name=3*, CPU=2*, GPU=2*, RAM=2*（常にタイル幅に収まる）
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        for (int i = 0; i < 4; i++)
            innerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        string[] headers = { "Name", "CPU", "GPU", "RAM" };
        _topProcCells = new TextBlock[4, 4];

        for (int row = 0; row < 4; row++)
        {
            bool isHeader = row == 0;
            for (int col = 0; col < 4; col++)
            {
                var tb = new TextBlock
                {
                    Text       = isHeader ? headers[col] : "",
                    FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
                    TextAlignment = col == 0 ? TextAlignment.Left : TextAlignment.Right,
                    Padding    = new Thickness(3, 1, 3, 1),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                var cell = new Border
                {
                    Child            = tb,
                    BorderBrush      = lineBrush,
                    // 左区切り線（2列目以降）＋上区切り線（データ行）
                    BorderThickness  = new Thickness(col > 0 ? 1 : 0, row > 0 ? 1 : 0, 0, 0),
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                innerGrid.Children.Add(cell);
                _topProcCells[row, col] = tb;
            }
        }

        _topProcessPanel = new Border
        {
            Child            = innerGrid,
            BorderBrush      = lineBrush,
            BorderThickness  = new Thickness(1),
            Margin           = new Thickness(4),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ContentGrid.Children.Add(_topProcessPanel);
    }

    private void ApplyTopProcFont()
    {
        if (_tile == null || _topProcCells == null) return;
        var cf    = _tile.ContentFont ?? _tile.TitleFont;
        double px = cf.FontSizePt * 4.0 / 3.0;
        var brush = new SolidColorBrush(ColorPalette.GetColor(cf.FontColor));
        FontFamily? family = string.IsNullOrEmpty(cf.FontName) ? null : new FontFamily(cf.FontName);
        for (int r = 0; r < 4; r++)
        for (int c = 0; c < 4; c++)
        {
            var tb = _topProcCells[r, c];
            tb.FontSize   = px;
            tb.Foreground = brush;
            if (family != null) tb.FontFamily = family;
        }
    }

    private void RenderTopProcess(IReadOnlyList<TopProcessEntry> entries)
    {
        if (_tile == null || _si == null) return;
        EnsureTopProcessPanel();
        ApplyTopProcFont();

        for (int i = 0; i < 3; i++)
        {
            if (i < entries.Count)
            {
                var e    = entries[i];
                string n = e.Name.Length > 10 ? e.Name[..9] + "…" : e.Name;
                _topProcCells![i + 1, 0].Text = n;
                _topProcCells![i + 1, 1].Text = e.CpuPercent < 0 ? "─" : $"{e.CpuPercent:F1}%";
                _topProcCells![i + 1, 2].Text = $"{e.GpuPercent:F1}%";
                _topProcCells![i + 1, 3].Text = $"{e.RamMb:F0}MB";
            }
            else
            {
                for (int c = 0; c < 4; c++)
                    _topProcCells![i + 1, c].Text = "─";
            }
        }

        TextPanel.Visibility          = Visibility.Collapsed;
        CirclePanel.Visibility        = Visibility.Collapsed;
        _topProcessPanel!.Visibility  = Visibility.Visible;

        UpdateTopProcFontSize();

        if (_tileBorder != null)
        {
            var bgColor = ColorPalette.GetColor(_tile.Color);
            double alpha = ColorPalette.OpacityToDouble(_tile.Opacity);
            _tileBorder.Background = new SolidColorBrush(
                Color.FromArgb((byte)(255 * alpha), bgColor.R, bgColor.G, bgColor.B));
        }
    }
}
