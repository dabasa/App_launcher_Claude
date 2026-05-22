using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppLauncher.Helpers;
using AppLauncher.Models;
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
        new("os",      "OS情報"),
        new("storage", "ストレージ"),
        new("usage",   "使用率"),
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
        FontColorCombo.ItemsSource       = ColorItems;

        FontNameCombo.ItemsSource = new[] { "" }
            .Concat(Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n))
            .ToList();

        ImagePositionCombo.ItemsSource       = ImagePositions;
        ImagePositionCombo.DisplayMemberPath = nameof(ValueItem.Display);

        BasicHeader.MouseLeftButtonUp      += (_, _) => ToggleSection(BasicContent,      BasicArrow,      "基本");
        LaunchHeader.MouseLeftButtonUp     += (_, _) => ToggleSection(LaunchContent,     LaunchArrow,     "起動");
        SystemHeader.MouseLeftButtonUp     += (_, _) => ToggleSection(SystemContent,     SystemArrow,     "システム情報");
        AppearanceHeader.MouseLeftButtonUp += (_, _) => ToggleSection(AppearanceContent, AppearanceArrow, "見た目");

        BrowsePathButton.MouseLeftButtonUp    += OnBrowsePath;
        BrowseWorkDirButton.MouseLeftButtonUp += OnBrowseWorkDir;
        BrowseImageButton.MouseLeftButtonUp   += OnBrowseImage;

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
            UpdatePreviewImage();
            UpdateSiTargetCombo();
        }
    }

    private void OnTileVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TileEditViewModel.ImagePath)
                           or nameof(TileEditViewModel.ImagePosition)
                           or nameof(TileEditViewModel.ImageTransparent))
            UpdatePreviewImage();

        if (e.PropertyName is nameof(TileEditViewModel.SiCategory)
                           or nameof(TileEditViewModel.SiDeviceType))
            UpdateSiTargetCombo();
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

    private void UpdatePreviewImage()
    {
        var vm = _subscribedTileVm;
        if (vm == null || PreviewImage == null) return;

        string? path = vm.ImagePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            PreviewImage.Visibility = Visibility.Collapsed;
            ArrangePreviewImageAndText("top");
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

        ArrangePreviewImageAndText(vm.ImagePosition ?? "top");
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
        CancelButton.Background      = uiBrush;
        OkButton.Background          = uiBrush;
        BrowsePathButton.Background    = uiBrush;
        BrowseWorkDirButton.Background = uiBrush;
        BrowseImageButton.Background   = uiBrush;
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
}
