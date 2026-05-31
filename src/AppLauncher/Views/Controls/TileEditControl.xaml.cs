using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppLauncher.Helpers;
using AppLauncher.Models;
using AppLauncher.Models.Config;
using AppLauncher.Services;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class TileEditControl : UserControl
{
    private sealed record ColorItem(string Name, SolidColorBrush Brush);
    private sealed record ValueItem(string Value, string Display);

    private static readonly IReadOnlyList<ColorItem> ColorItems =
        ColorPalette.All.Keys
            .Select(n => new ColorItem(n, ColorPalette.GetBrush(n)))
            .ToList();

    private static readonly IReadOnlyList<ValueItem> TileTypes =
    [
        new("app",     "アプリ (app)"),
        new("url",     "URL (url)"),
        new("folder",  "フォルダ (folder)"),
        new("webview", "ウェブビュー (webview)"),
        new("system",  "システム情報 (system)"),
    ];

    private static readonly IReadOnlyList<ValueItem> SiCategories =
    [
        new("os",          "OS情報"),
        new("storage",     "ストレージ"),
        new("usage",       "使用率"),
        new("top_process", "TOPプロセス"),
    ];

    private static readonly IReadOnlyList<ValueItem> SiDeviceTypes =
    [
        new("cpu",    "CPU"),
        new("memory", "メモリ"),
        new("gpu",    "GPU"),
        new("lan",    "LAN"),
    ];

    private static readonly IReadOnlyList<ValueItem> SiDisplayFormats =
    [
        new("text",   "テキスト表示"),
        new("circle", "円形グラフ"),
    ];

    // usage & (cpu|memory|gpu) のときのみ縦棒グラフを追加
    private static readonly IReadOnlyList<ValueItem> SiDisplayFormatsWithBar =
    [
        new("text",   "テキスト表示"),
        new("circle", "円形グラフ"),
        new("bar",    "縦棒グラフ"),
    ];

    private static readonly IReadOnlyList<ValueItem> ImagePositions =
    [
        new("top",    "上"),
        new("bottom", "下"),
        new("left",   "左"),
        new("right",  "右"),
        new("center", "中心"),
    ];

    private TileEditViewModel? _subscribedTileVm;

    public TileEditControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        TypeCombo.ItemsSource       = TileTypes;
        TypeCombo.DisplayMemberPath = nameof(ValueItem.Display);

        SiCategoryCombo.ItemsSource       = SiCategories;
        SiCategoryCombo.DisplayMemberPath = nameof(ValueItem.Display);

        SiDeviceTypeCombo.ItemsSource       = SiDeviceTypes;
        SiDeviceTypeCombo.DisplayMemberPath = nameof(ValueItem.Display);

        SiDisplayFormatCombo.ItemsSource       = SiDisplayFormats;
        SiDisplayFormatCombo.DisplayMemberPath = nameof(ValueItem.Display);

        SiMainColorCombo.ItemsSource     = ColorItems;
        SiAccentColorCombo.ItemsSource   = ColorItems;
        SiBgAccentColorCombo.ItemsSource = ColorItems;
        TileColorCombo.ItemsSource       = ColorItems;

        ImagePositionCombo.ItemsSource       = ImagePositions;
        ImagePositionCombo.DisplayMemberPath = nameof(ValueItem.Display);

        BasicHeader.MouseLeftButtonUp      += (_, _) => ToggleSection(BasicContent,      BasicArrow,      "基本");
        LaunchHeader.MouseLeftButtonUp     += (_, _) => ToggleSection(LaunchContent,     LaunchArrow,     "起動・コンテンツ");
        SystemHeader.MouseLeftButtonUp     += (_, _) => ToggleSection(SystemContent,     SystemArrow,     "システム情報");
        AppearanceHeader.MouseLeftButtonUp += (_, _) => ToggleSection(AppearanceContent, AppearanceArrow, "外観");
        FontHeader.MouseLeftButtonUp       += (_, _) => ToggleSection(FontContent,       FontArrow,       "フォント");

        BrowsePathButton.MouseLeftButtonUp    += OnBrowsePath;
        BrowseWorkDirButton.MouseLeftButtonUp += OnBrowseWorkDir;
        BrowseImageButton.MouseLeftButtonUp   += OnBrowseImage;

        TitleFontDetailButton.MouseLeftButtonUp   += OnTitleFontDetailClick;
        ContentFontDetailButton.MouseLeftButtonUp += OnContentFontDetailClick;

        OkButton.MouseLeftButtonUp     += (_, _) => App.LauncherViewModel?.ConfirmTileEditCommand.Execute(null);
        CancelButton.MouseLeftButtonUp += (_, _) => App.LauncherViewModel?.CloseTileEditCommand.Execute(null);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyUiElementColor();
        if (DataContext is LauncherViewModel vm)
            SubscribeToTileVm(vm.EditingTileVm);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LauncherViewModel oldVm)
            oldVm.PropertyChanged -= OnLauncherVmPropertyChanged;
        if (e.NewValue is LauncherViewModel newVm)
        {
            newVm.PropertyChanged += OnLauncherVmPropertyChanged;
            SubscribeToTileVm(newVm.EditingTileVm);
        }
    }

    private void OnLauncherVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.EditingTileVm) && sender is LauncherViewModel vm)
            SubscribeToTileVm(vm.EditingTileVm);
    }

    private void SubscribeToTileVm(TileEditViewModel? vm)
    {
        if (_subscribedTileVm != null)
            _subscribedTileVm.PropertyChanged -= OnTileVmPropertyChanged;
        _subscribedTileVm = vm;
        if (vm != null)
        {
            vm.PropertyChanged += OnTileVmPropertyChanged;
            ResetSections();
            UpdatePreviewFontSize(vm);
            UpdatePreviewImage();
            UpdateSiTargetCombo();
            UpdateSiDisplayFormatCombo();
            _ = RefreshSystemPreviewAsync();
        }
    }

    // 編集画面を開くたびに全セクションを閉じた状態にリセット
    private void ResetSections()
    {
        BasicContent.Visibility      = Visibility.Collapsed;
        LaunchContent.Visibility     = Visibility.Collapsed;
        SystemContent.Visibility     = Visibility.Collapsed;
        AppearanceContent.Visibility = Visibility.Collapsed;
        FontContent.Visibility       = Visibility.Collapsed;

        BasicArrow.Text      = "▼ 基本";
        LaunchArrow.Text     = "▼ 起動・コンテンツ";
        SystemArrow.Text     = "▼ システム情報";
        AppearanceArrow.Text = "▼ 外観";
        FontArrow.Text       = "▼ フォント";
    }

    private void OnTileVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TileEditViewModel.ImagePath)
                           or nameof(TileEditViewModel.ImagePosition)
                           or nameof(TileEditViewModel.ImageTransparent))
            UpdatePreviewImage();

        if (e.PropertyName is nameof(TileEditViewModel.SiCategory)
                           or nameof(TileEditViewModel.SiDeviceType))
        {
            UpdateSiTargetCombo();
            UpdateSiDisplayFormatCombo();
        }

        if (e.PropertyName is nameof(TileEditViewModel.Type)
                           or nameof(TileEditViewModel.SiCategory)
                           or nameof(TileEditViewModel.SiDeviceType)
                           or nameof(TileEditViewModel.SiTarget)
                           or nameof(TileEditViewModel.SiDisplayFormat))
            _ = RefreshSystemPreviewAsync();

        if (e.PropertyName is nameof(TileEditViewModel.Title)
                           or nameof(TileEditViewModel.ImagePosition)
                           or nameof(TileEditViewModel.FontSizePt)
                           or nameof(TileEditViewModel.FontColor))
            UpdateSystemPreviewArrangement();

        if (e.PropertyName is nameof(TileEditViewModel.Title)
                           or nameof(TileEditViewModel.FontSizePt)
                           or nameof(TileEditViewModel.FontName)
                           or nameof(TileEditViewModel.AutoFontSize)
                           or nameof(TileEditViewModel.ImagePath)
                           or nameof(TileEditViewModel.ImagePosition))
            UpdatePreviewFontSize();
    }

    // ─── プレビューフォントサイズ（自動調整対応） ─────────────────────────

    // 自動調整を考慮した実効プレビューフォントサイズ(WPF px)を返す共通ヘルパー
    private static double ComputeEffectivePreviewFontSizePx(TileEditViewModel vm)
    {
        if (!vm.AutoFontSize) return vm.PreviewFontSize;

        var layout   = App.ConfigService.Current.Layout;
        double tileW = vm.ColSpan * layout.TileSize + (vm.ColSpan - 1) * layout.TileMargin;
        double tileH = vm.RowSpan * layout.TileSize + (vm.RowSpan - 1) * layout.TileMargin;

        const double pad = 16.0;
        double w = tileW - pad;
        double h = tileH - pad;

        bool hasImage = !string.IsNullOrEmpty(vm.ImagePath);
        if (hasImage)
        {
            switch (vm.ImagePosition ?? "top")
            {
                case "top": case "bottom": h = (tileH - pad) * 0.35; break;
                case "left": case "right": w = tileW / 2.0 - pad;    break;
            }
        }

        if (w <= 0 || h <= 0) return vm.PreviewFontSize;

        var fontFamily = string.IsNullOrEmpty(vm.FontName)
            ? SystemFonts.MessageFontFamily : new FontFamily(vm.FontName);
        var measure = new TextBlock
        {
            Text         = string.IsNullOrEmpty(vm.Title) ? "Aa" : vm.Title,
            TextWrapping = TextWrapping.Wrap,
            FontFamily   = fontFamily,
            Padding      = new Thickness(8),
        };

        double startPt = Math.Clamp(vm.FontSizePt, 6, 72);
        for (double size = startPt; size >= 6; size--)
        {
            measure.FontSize = size * 4.0 / 3.0;
            measure.Measure(new Size(w + 16, double.PositiveInfinity));
            if (measure.DesiredSize.Height <= h + 16) return size * 4.0 / 3.0;
        }
        return 6 * 4.0 / 3.0;
    }

    private void UpdatePreviewFontSize(TileEditViewModel? source = null)
    {
        var vm = source ?? _subscribedTileVm;
        if (vm == null) return;

        double effectivePx = ComputeEffectivePreviewFontSizePx(vm);
        PreviewText.FontSize = effectivePx;
        // システムタイルの SysTitleText は ArrangeSystemPreviewPanels() が管理する
    }

    private void UpdateSiTargetCombo()
    {
        var vm = _subscribedTileVm;
        if (vm == null) return;

        IEnumerable<string>? items = null;
        if (vm.SiCategory == "storage")
            items = SystemInfoService.GetDriveTargets();
        else if (vm.SiCategory == "usage" && vm.SiDeviceType == "lan")
            items = SystemInfoService.GetLanTargets();
        else if (vm.SiCategory == "usage" && vm.SiDeviceType == "gpu")
            items = SystemInfoService.GetGpuTargets();

        if (items != null)
            SiTargetCombo.ItemsSource = items.ToList();
    }

    private void UpdateSiDisplayFormatCombo()
    {
        var vm = _subscribedTileVm;
        if (vm == null) return;
        bool hasBar = vm.SiCategory == "usage" && vm.SiDeviceType is "cpu" or "memory" or "gpu";
        SiDisplayFormatCombo.ItemsSource = hasBar ? SiDisplayFormatsWithBar : SiDisplayFormats;
    }

    private async Task RefreshSystemPreviewAsync()
    {
        var vm = _subscribedTileVm;
        if (vm?.Type != "system")
        {
            SystemPreviewPanel.Visibility = Visibility.Collapsed;
            SysTitleText.Visibility       = Visibility.Collapsed;
            PreviewText.Visibility        = Visibility.Visible;
            return;
        }
        var cfg = new SystemInfoConfig
        {
            Category   = vm.SiCategory,
            DeviceType = vm.SiDeviceType,
            Target     = vm.SiTarget,
            Threshold  = vm.SiThreshold,
        };
        try
        {
            var data = await Task.Run(() => SystemInfoService.Instance.GetData(cfg));
            if (_subscribedTileVm != vm) return;
            RenderSystemPreview(vm, data);
        }
        catch
        {
            if (_subscribedTileVm != vm) return;
            ArrangeSystemPreviewPanels(vm.ImagePosition, !string.IsNullOrEmpty(vm.Title));
            SysMainText.Text       = "─";
            SysSubText.Text        = "";
            SysTextPanel.Visibility   = Visibility.Visible;
            SysCirclePanel.Visibility = Visibility.Collapsed;
            SysBarPanel.Visibility    = Visibility.Collapsed;
            SystemPreviewPanel.Visibility = Visibility.Visible;
            FitPreviewSystemTextFont();
        }
    }

    private void RenderSystemPreview(TileEditViewModel vm, SystemData data)
    {
        ArrangeSystemPreviewPanels(vm.ImagePosition, !string.IsNullOrEmpty(vm.Title));

        bool isCircle = vm.SiDisplayFormat == "circle" &&
                        (vm.SiCategory == "storage" ||
                         (vm.SiCategory == "usage" && vm.SiDeviceType != "lan"));
        bool isBar    = vm.SiDisplayFormat == "bar" &&
                        vm.SiCategory == "usage" && vm.SiDeviceType is "cpu" or "memory" or "gpu";

        var fontBrush = vm.PreviewForeground;
        var arcBrush  = data.ThresholdExceeded
            ? ColorPalette.GetBrush(vm.SiAccentColor)
            : ColorPalette.GetBrush(vm.SiMainColor);

        if (isBar)
        {
            SysTextPanel.Visibility   = Visibility.Collapsed;
            SysCirclePanel.Visibility = Visibility.Collapsed;
            SysBarPanel.Visibility    = Visibility.Visible;

            double pct = Math.Clamp(data.Percentage / 100.0, 0.0, 1.0);
            const double barAreaTop = 10.0;
            const double barAreaH   = 44.0;
            double fillH   = barAreaH * pct;
            double fillTop = barAreaTop + barAreaH - fillH;

            SysBarFill.Fill   = arcBrush;
            Canvas.SetTop(SysBarFill, fillTop);
            SysBarFill.Height = fillH;

            SysBarValueText.Text       = $"{data.Percentage:F0}%";
            SysBarValueText.Foreground = fontBrush;
            SysBarLabel.Text           = data.SubText;
            SysBarLabel.Foreground     = fontBrush;
        }
        else if (isCircle)
        {
            SysTextPanel.Visibility   = Visibility.Collapsed;
            SysCirclePanel.Visibility = Visibility.Visible;
            SysBarPanel.Visibility    = Visibility.Collapsed;

            // 48px楕円の中心線半径=20、StrokeThickness=8
            const double strokeT = 8.0;
            const double radius  = 20.0;
            double circ  = 2 * Math.PI * radius / strokeT;
            double pct   = Math.Clamp(data.Percentage / 100.0, 0.0, 1.0);
            double used  = pct * circ;
            double unused = circ - used;

            SysCircleArc.Stroke          = arcBrush;
            SysCircleArc.StrokeDashArray = new DoubleCollection([used, unused]);
            SysCircleCenterText.Text       = $"{data.Percentage:F0}%";
            SysCircleCenterText.Foreground = fontBrush;
            SysCircleLabel.Text            = data.SubText;
            SysCircleLabel.Foreground      = fontBrush;
            FitPreviewCircleFont();
        }
        else
        {
            SysTextPanel.Visibility   = Visibility.Visible;
            SysCirclePanel.Visibility = Visibility.Collapsed;
            SysBarPanel.Visibility    = Visibility.Collapsed;

            bool isTopProc = vm.SiCategory == "top_process";
            SysMainText.TextAlignment    = isTopProc ? TextAlignment.Left   : TextAlignment.Center;
            SysSubText.Visibility        = isTopProc ? Visibility.Collapsed : Visibility.Visible;
            SysTextPanel.HorizontalAlignment = isTopProc
                ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;

            SysMainText.Text       = data.MainText;
            SysMainText.Foreground = fontBrush;
            SysSubText.Text        = data.SubText;
            SysSubText.Foreground  = fontBrush;
            FitPreviewSystemTextFont();
        }

        SystemPreviewPanel.Visibility = Visibility.Visible;
    }

    private void UpdateSystemPreviewArrangement()
    {
        var vm = _subscribedTileVm;
        if (vm?.Type != "system") return;
        ArrangeSystemPreviewPanels(vm.ImagePosition, !string.IsNullOrEmpty(vm.Title));
        if (SysTextPanel.Visibility == Visibility.Visible)
            FitPreviewSystemTextFont();
    }

    private void ArrangeSystemPreviewPanels(string? position, bool hasTitle)
    {
        // システムタイルプレビュー中は通常タイトル TextBlock を隠す
        PreviewText.Visibility = Visibility.Collapsed;

        // デフォルトリセット：SystemPreviewPanel が全領域、SysTitleText は非表示
        PreviewRow0.Height = new GridLength(1, GridUnitType.Star);
        PreviewRow1.Height = new GridLength(0);
        PreviewCol0.Width  = new GridLength(1, GridUnitType.Star);
        PreviewCol1.Width  = new GridLength(0);
        Grid.SetRow(SystemPreviewPanel, 0);    Grid.SetRowSpan(SystemPreviewPanel, 2);
        Grid.SetColumn(SystemPreviewPanel, 0); Grid.SetColumnSpan(SystemPreviewPanel, 2);
        Grid.SetRow(SysTitleText, 0);    Grid.SetRowSpan(SysTitleText, 1);
        Grid.SetColumn(SysTitleText, 0); Grid.SetColumnSpan(SysTitleText, 1);
        SysTitleText.HorizontalAlignment = HorizontalAlignment.Center;
        SysTitleText.VerticalAlignment   = VerticalAlignment.Center;
        SysTitleText.Visibility          = Visibility.Collapsed;

        if (!hasTitle) return;

        var vm = _subscribedTileVm;
        SysTitleText.Text       = vm?.Title ?? "";
        SysTitleText.Foreground = vm?.PreviewForeground ?? System.Windows.Media.Brushes.White;
        double sysFontPx = vm != null ? ComputeEffectivePreviewFontSizePx(vm) : 12;
        SysTitleText.FontSize   = Math.Max(8, sysFontPx * 0.75);
        SysTitleText.Visibility = Visibility.Visible;

        Grid.SetRowSpan(SystemPreviewPanel, 1);
        Grid.SetColumnSpan(SystemPreviewPanel, 1);

        switch (position ?? "top")
        {
            case "top":
                PreviewRow0.Height = GridLength.Auto;
                PreviewRow1.Height = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(SysTitleText, 0);    Grid.SetRowSpan(SysTitleText, 1);
                Grid.SetColumn(SysTitleText, 0); Grid.SetColumnSpan(SysTitleText, 2);
                Grid.SetRow(SystemPreviewPanel, 1);
                Grid.SetColumn(SystemPreviewPanel, 0); Grid.SetColumnSpan(SystemPreviewPanel, 2);
                break;

            case "bottom":
                PreviewRow0.Height = new GridLength(1, GridUnitType.Star);
                PreviewRow1.Height = GridLength.Auto;
                Grid.SetRow(SysTitleText, 1);    Grid.SetRowSpan(SysTitleText, 1);
                Grid.SetColumn(SysTitleText, 0); Grid.SetColumnSpan(SysTitleText, 2);
                Grid.SetRow(SystemPreviewPanel, 0);
                Grid.SetColumn(SystemPreviewPanel, 0); Grid.SetColumnSpan(SystemPreviewPanel, 2);
                break;

            case "left":
                PreviewRow1.Height = new GridLength(0);
                PreviewCol0.Width  = GridLength.Auto;
                PreviewCol1.Width  = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(SysTitleText, 0);    Grid.SetRowSpan(SysTitleText, 2);
                Grid.SetColumn(SysTitleText, 0); Grid.SetColumnSpan(SysTitleText, 1);
                Grid.SetRow(SystemPreviewPanel, 0);    Grid.SetRowSpan(SystemPreviewPanel, 2);
                Grid.SetColumn(SystemPreviewPanel, 1); Grid.SetColumnSpan(SystemPreviewPanel, 1);
                break;

            case "right":
                PreviewRow1.Height = new GridLength(0);
                PreviewCol0.Width  = new GridLength(1, GridUnitType.Star);
                PreviewCol1.Width  = GridLength.Auto;
                Grid.SetRow(SysTitleText, 0);    Grid.SetRowSpan(SysTitleText, 2);
                Grid.SetColumn(SysTitleText, 1); Grid.SetColumnSpan(SysTitleText, 1);
                Grid.SetRow(SystemPreviewPanel, 0);    Grid.SetRowSpan(SystemPreviewPanel, 2);
                Grid.SetColumn(SystemPreviewPanel, 0); Grid.SetColumnSpan(SystemPreviewPanel, 1);
                break;

            default: // "center" - タイトルを上部に重ねて表示
                Grid.SetRowSpan(SystemPreviewPanel, 2); Grid.SetColumnSpan(SystemPreviewPanel, 2);
                Grid.SetRow(SysTitleText, 0);    Grid.SetRowSpan(SysTitleText, 2);
                Grid.SetColumn(SysTitleText, 0); Grid.SetColumnSpan(SysTitleText, 2);
                SysTitleText.VerticalAlignment = VerticalAlignment.Top;
                break;
        }
    }

    private void UpdatePreviewImage()
    {
        var vm = _subscribedTileVm;
        if (vm == null || PreviewImage == null) return;

        string? path = vm.ImagePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            PreviewImage.Visibility = Visibility.Collapsed;
            if (vm.Type != "system")
            {
                // 画像なし: テキストが全体を占有
                PreviewRow0.Height = new GridLength(1, GridUnitType.Star);
                PreviewRow1.Height = new GridLength(0);
                PreviewCol0.Width  = new GridLength(1, GridUnitType.Star);
                PreviewCol1.Width  = new GridLength(0);
                Grid.SetRow(PreviewText, 0);    Grid.SetRowSpan(PreviewText, 2);
                Grid.SetColumn(PreviewText, 0); Grid.SetColumnSpan(PreviewText, 2);
                Grid.SetRow(PreviewImage, 0);   Grid.SetRowSpan(PreviewImage, 1);
                Grid.SetColumn(PreviewImage, 0); Grid.SetColumnSpan(PreviewImage, 1);
            }
            return;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource   = new Uri(path, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            PreviewImage.Source     = bmp;
            PreviewImage.Opacity    = vm.ImageTransparent ? 0.4 : 1.0;
            PreviewImage.Visibility = Visibility.Visible;
        }
        catch
        {
            PreviewImage.Visibility = Visibility.Collapsed;
        }

        if (vm.Type != "system")
            ArrangePreviewImageAndText(vm.ImagePosition ?? "top");
        // system タイルの場合は ArrangeSystemPreviewPanels で処理

        UpdatePreviewFontSize(vm);
    }

    private void ArrangePreviewImageAndText(string position)
    {
        Grid.SetRowSpan(PreviewImage, 1);
        Grid.SetColumnSpan(PreviewImage, 1);
        Grid.SetRowSpan(PreviewText, 1);
        Grid.SetColumnSpan(PreviewText, 1);

        switch (position)
        {
            case "bottom":
                PreviewRow0.Height = new GridLength(1, GridUnitType.Star);
                PreviewRow1.Height = new GridLength(2, GridUnitType.Star);
                PreviewCol0.Width  = new GridLength(1, GridUnitType.Star);
                PreviewCol1.Width  = new GridLength(0);
                Grid.SetRow(PreviewImage, 1); Grid.SetColumn(PreviewImage, 0);
                Grid.SetRow(PreviewText,  0); Grid.SetColumn(PreviewText,  0);
                break;
            case "left":
                PreviewRow0.Height = new GridLength(1, GridUnitType.Star);
                PreviewRow1.Height = new GridLength(0);
                PreviewCol0.Width  = new GridLength(1, GridUnitType.Star);
                PreviewCol1.Width  = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(PreviewImage, 0); Grid.SetColumn(PreviewImage, 0);
                Grid.SetRow(PreviewText,  0); Grid.SetColumn(PreviewText,  1);
                break;
            case "right":
                PreviewRow0.Height = new GridLength(1, GridUnitType.Star);
                PreviewRow1.Height = new GridLength(0);
                PreviewCol0.Width  = new GridLength(1, GridUnitType.Star);
                PreviewCol1.Width  = new GridLength(1, GridUnitType.Star);
                Grid.SetRow(PreviewImage, 0); Grid.SetColumn(PreviewImage, 1);
                Grid.SetRow(PreviewText,  0); Grid.SetColumn(PreviewText,  0);
                break;
            case "center":
                PreviewRow0.Height = new GridLength(1, GridUnitType.Star);
                PreviewRow1.Height = new GridLength(0);
                PreviewCol0.Width  = new GridLength(1, GridUnitType.Star);
                PreviewCol1.Width  = new GridLength(0);
                Grid.SetRowSpan(PreviewImage, 2); Grid.SetColumnSpan(PreviewImage, 2);
                Grid.SetRowSpan(PreviewText,  2); Grid.SetColumnSpan(PreviewText,  2);
                Grid.SetRow(PreviewImage, 0); Grid.SetColumn(PreviewImage, 0);
                Grid.SetRow(PreviewText,  0); Grid.SetColumn(PreviewText,  0);
                break;
            default: // "top"
                PreviewRow0.Height = new GridLength(2, GridUnitType.Star);
                PreviewRow1.Height = new GridLength(1, GridUnitType.Star);
                PreviewCol0.Width  = new GridLength(1, GridUnitType.Star);
                PreviewCol1.Width  = new GridLength(0);
                Grid.SetRow(PreviewImage, 0); Grid.SetColumn(PreviewImage, 0);
                Grid.SetRow(PreviewText,  1); Grid.SetColumn(PreviewText,  0);
                break;
        }
    }

    public void ApplyUiElementColor()
    {
        var vm = App.LauncherViewModel;
        if (vm == null) return;
        var baseColor = ColorPalette.GetColor(vm.CurrentPage.BackgroundColor);
        var uiBrush   = new SolidColorBrush(ColorHelper.ComputeUiElementColor(baseColor));

        Background = new SolidColorBrush(Color.FromArgb(255, baseColor.R, baseColor.G, baseColor.B));

        BasicHeader.BorderBrush      = uiBrush;
        LaunchHeader.BorderBrush     = uiBrush;
        SystemHeader.BorderBrush     = uiBrush;
        AppearanceHeader.BorderBrush = uiBrush;
        FontHeader.BorderBrush       = uiBrush;
        CancelButton.Background      = uiBrush;
        OkButton.Background          = uiBrush;
        BrowsePathButton.Background    = uiBrush;
        BrowseWorkDirButton.Background = uiBrush;
        BrowseImageButton.Background   = uiBrush;
        TitleFontDetailButton.Background   = uiBrush;
        ContentFontDetailButton.Background = uiBrush;
    }

    private static void ToggleSection(UIElement content, TextBlock arrow, string label)
    {
        bool isVisible = content.Visibility == Visibility.Visible;
        content.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
        arrow.Text = (isVisible ? "▼ " : "▲ ") + label;
    }

    private void OnBrowsePath(object sender, MouseButtonEventArgs e)
    {
        var vm = App.LauncherViewModel?.EditingTileVm;
        if (vm == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "実行ファイルを選択",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) vm.Path = dlg.FileName;
    }

    private void OnBrowseWorkDir(object sender, MouseButtonEventArgs e)
    {
        var vm = App.LauncherViewModel?.EditingTileVm;
        if (vm == null) return;
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "作業フォルダを選択" };
        if (dlg.ShowDialog() == true) vm.WorkDir = dlg.FolderName;
    }

    private void OnBrowseImage(object sender, MouseButtonEventArgs e)
    {
        var vm = App.LauncherViewModel?.EditingTileVm;
        if (vm == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "画像 / アイコンを選択",
            Filter = "画像ファイル (*.png;*.jpg;*.gif;*.ico)|*.png;*.jpg;*.gif;*.ico|すべてのファイル (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) vm.ImagePath = dlg.FileName;
    }

    // ─── フォント詳細ダイアログ ───────────────────────────────────────────
    private void OnTitleFontDetailClick(object sender, MouseButtonEventArgs e)
    {
        var vm = _subscribedTileVm;
        if (vm == null) return;
        var previewText = string.IsNullOrEmpty(vm.Title) ? "Aa" : vm.Title;
        var uiColor     = GetUiColor();
        FontDetailPanel.Open(
            vm.GetTitleFontCopy(), previewText, "タイトルフォント設定詳細",
            result =>
            {
                vm.ApplyTitleFont(result);
                if (vm.Type == "system") UpdateSystemPreviewArrangement();
                UpdatePreviewFontSize(vm);  // _subscribedTileVm でなく vm を直接渡す
            },
            uiColor);
    }

    private void OnContentFontDetailClick(object sender, MouseButtonEventArgs e)
    {
        var vm = _subscribedTileVm;
        if (vm == null) return;
        var uiColor = GetUiColor();
        FontDetailPanel.Open(
            vm.GetContentFontCopy(), "99%\nCPU", "コンテンツフォント設定詳細",
            result =>
            {
                vm.ApplyContentFont(result);
                _ = RefreshSystemPreviewAsync();
            },
            uiColor);
    }

    // ─── プレビュー円グラフのフォントサイズ調整 ───────────────────────────────
    // プレビュー Canvas は 64×64（実タイル 80×80 の 0.8 倍）。
    // 実タイルの UpdateCircleLayout と同様に "100%" 基準で全パターン統一サイズ。
    private void FitPreviewCircleFont()
    {
        if (string.IsNullOrEmpty(SysCircleCenterText.Text)) return;

        // 内側リング内径 40px → 視覚余裕を持たせた制約
        const double maxW = 30.0;
        const double maxH = 16.0;

        var typeface = new Typeface(
            SysCircleCenterText.FontFamily,
            FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        double ppd = 1.0;
        try { ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }

        for (double pt = 36; pt >= 6; pt--)
        {
            double px = pt * 4.0 / 3.0;
            var ft = new FormattedText(
                "100%",
                System.Globalization.CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, px, Brushes.White, ppd);

            if (ft.Width <= maxW && ft.Height <= maxH)
            {
                SysCircleCenterText.FontSize = px;
                SysCircleLabel.FontSize      = Math.Max(7.0, px * 0.75);
                return;
            }
        }
        SysCircleCenterText.FontSize = 6 * 4.0 / 3.0;
        SysCircleLabel.FontSize      = 7.0;
    }

    // ─── プレビュー システムテキストのフォントサイズ自動調整 ──────────────────
    // プレビュー Border は 200×96px。タイトルが占有する高さを実測し、残りを
    // コンテンツフォント設定に従って自動調整する。UpdateContentFontSize() と同じロジック。
    private void FitPreviewSystemTextFont()
    {
        if (_subscribedTileVm is not { } vm) return;

        const double previewW = 200.0;
        const double previewH = 96.0;

        string pos      = vm.ImagePosition ?? "top";
        bool   hasTitle = !string.IsNullOrEmpty(vm.Title) && SysTitleText.Visibility == Visibility.Visible;

        double availW = pos is "left" or "right" ? previewW / 2.0 : previewW;
        double availH = previewH;

        if (hasTitle && pos is not "left" and not "right")
        {
            var titleMeasure = new TextBlock
            {
                Text         = SysTitleText.Text,
                FontSize     = SysTitleText.FontSize,
                FontFamily   = SysTitleText.FontFamily,
                TextWrapping = TextWrapping.Wrap,
                Padding      = SysTitleText.Padding,
            };
            titleMeasure.Measure(new Size(previewW, double.PositiveInfinity));
            availH = Math.Max(0, previewH - titleMeasure.DesiredSize.Height);
        }

        var cf = vm.GetContentFontCopy();

        if (!cf.AutoFontSize)
        {
            double fixedPx = cf.FontSizePt * 4.0 / 3.0;
            SysMainText.FontSize = fixedPx;
            SysSubText.FontSize  = Math.Max(8, fixedPx - 4);
            return;
        }

        if (string.IsNullOrEmpty(SysMainText.Text))
        {
            double cfPx = cf.FontSizePt * 4.0 / 3.0;
            SysMainText.FontSize = cfPx;
            SysSubText.FontSize  = Math.Max(8, cfPx - 4);
            return;
        }

        double innerW     = Math.Max(0, availW - 16);
        double startPt    = Math.Clamp(cf.FontSizePt, 6, 72);
        bool   subVisible = SysSubText.Visibility == Visibility.Visible;

        for (double size = startPt; size >= 6; size--)
        {
            double mainPx = size * 4.0 / 3.0;
            double subPx  = Math.Max(8, mainPx - 4);

            var mMain = new TextBlock
            {
                Text         = SysMainText.Text,
                TextWrapping = TextWrapping.Wrap,
                FontWeight   = FontWeights.SemiBold,
                FontSize     = mainPx,
            };
            mMain.Measure(new Size(innerW, double.PositiveInfinity));

            double totalNeeded = mMain.DesiredSize.Height + 16;
            if (subVisible && !string.IsNullOrEmpty(SysSubText.Text))
            {
                var mSub = new TextBlock
                {
                    Text         = SysSubText.Text,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize     = subPx,
                };
                mSub.Measure(new Size(innerW, double.PositiveInfinity));
                totalNeeded += mSub.DesiredSize.Height + 2;
            }

            if (totalNeeded <= availH)
            {
                SysMainText.FontSize = mainPx;
                SysSubText.FontSize  = subPx;
                return;
            }
        }
        SysMainText.FontSize = 6 * 4.0 / 3.0;
        SysSubText.FontSize  = 8;
    }

    private Color GetUiColor()
    {
        var vm = App.LauncherViewModel;
        if (vm == null) return Colors.Gray;
        return ColorHelper.ComputeUiElementColor(ColorPalette.GetColor(vm.CurrentPage.BackgroundColor));
    }
}
