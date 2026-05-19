using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppLauncher.Helpers;
using AppLauncher.Models;

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

    public TileEditControl()
    {
        InitializeComponent();

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

    private void OnLoaded(object sender, RoutedEventArgs e) => ApplyUiElementColor();

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
