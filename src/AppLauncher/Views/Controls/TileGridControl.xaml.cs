using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AppLauncher.Helpers;
using AppLauncher.Models;
using AppLauncher.Models.Config;
using AppLauncher.Services;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class TileGridControl : UserControl
{
    private TileViewModel? _resizingTile;
    private bool _modeSubscribed;

    // webview タイルはページ切替でも破棄しないためキャッシュする
    private static readonly Dictionary<(PageViewModel, int, int), WebViewTileControl> _webviewCache = [];

    public TileGridControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        TileGrid.DragOver  += OnTileGridDragOver;
        TileGrid.DragLeave += (_, _) => HidePreview();
        TileGrid.Drop      += OnTileGridDrop;
        TileGrid.Drop      += OnTileGridFileDrop;
        TileGrid.MouseMove += OnTileGridMouseMove;
        TileGrid.MouseLeftButtonUp += OnTileGridMouseLeftButtonUp;
    }

    // ─── DataContext（ページ切り替え）────────────────────────────────

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is PageViewModel oldPage)
            oldPage.Tiles.CollectionChanged -= OnTilesChanged;

        if (DataContext is PageViewModel newPage)
        {
            newPage.Tiles.CollectionChanged += OnTilesChanged;
            if (!_modeSubscribed && App.LauncherViewModel is { } vm)
            {
                vm.PropertyChanged += OnViewModelPropertyChanged;
                _modeSubscribed = true;
            }
        }
        Rebuild();
    }

    private void OnTilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.Mode)) Rebuild();
    }

    // ─── グリッド再構築 ───────────────────────────────────────────────

    private void Rebuild()
    {
        TileGrid.Children.Clear();
        TileGrid.ColumnDefinitions.Clear();
        TileGrid.RowDefinitions.Clear();

        if (DataContext is not PageViewModel page) return;

        var config       = App.ConfigService.Current;
        var layout       = config.Layout;
        var global       = config.Global;
        int cols         = global.TileCountCols;
        int rows         = global.TileCountRows;
        int tileSize     = layout.TileSize;
        int tileMargin   = layout.TileMargin;
        int cornerRadius = layout.TileCornerRadius;
        bool isEditMode  = App.LauncherViewModel?.Mode == AppMode.Edit;

        // 列・行定義（タイルとスペーサーを交互）
        for (int c = 0; c < cols; c++)
        {
            TileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(tileSize) });
            if (c < cols - 1)
                TileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(tileMargin) });
        }
        for (int r = 0; r < rows; r++)
        {
            TileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(tileSize) });
            if (r < rows - 1)
                TileGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(tileMargin) });
        }

        // 占有セルのマーク
        var occupied = new bool[cols, rows];
        foreach (var tile in page.Tiles)
            for (int dc = 0; dc < tile.ColSpan; dc++)
                for (int dr = 0; dr < tile.RowSpan; dr++)
                {
                    int c = tile.Col + dc, r = tile.Row + dr;
                    if (c < cols && r < rows) occupied[c, r] = true;
                }

        // 空スロット
        var bgColor   = ColorPalette.GetColor(page.BackgroundColor);
        var slotBrush = new SolidColorBrush(
            Color.FromArgb((byte)(255 * 0.4), bgColor.R, bgColor.G, bgColor.B));

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (occupied[c, r]) continue;
                var rect = new Rectangle
                {
                    Stroke          = slotBrush,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection([6.0, 4.0]),
                    Fill            = Brushes.Transparent,
                };
                Grid.SetColumn(rect, c * 2);
                Grid.SetRow(rect, r * 2);

                if (isEditMode)
                {
                    int col = c, row = r;
                    rect.Cursor = Cursors.Hand;
                    rect.MouseEnter += (_, _) =>
                        rect.Fill = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                    rect.MouseLeave += (_, _) => rect.Fill = Brushes.Transparent;
                    rect.MouseLeftButtonUp += (_, _) => CreateTileAt(col, row);
                }

                TileGrid.Children.Add(rect);
            }
        }

        // タイル配置（タイプに応じてコントロールを分岐）
        foreach (var tile in page.Tiles)
        {
            FrameworkElement element = tile.Type switch
            {
                "system"  => CreateSystemElement(tile, cornerRadius, bgColor),
                "webview" => GetOrCreateWebViewControl(page, tile),
                _         => CreateTileControl(tile, cornerRadius, bgColor),
            };

            Grid.SetColumn(element, tile.Col * 2);
            Grid.SetRow(element, tile.Row * 2);
            Grid.SetColumnSpan(element, tile.ColSpan * 2 - 1);
            Grid.SetRowSpan(element, tile.RowSpan * 2 - 1);
            TileGrid.Children.Add(element);
        }
    }

    // ─── タイル Control 生成 ───────────────────────────────────────────

    private TileControl CreateTileControl(TileViewModel tile, int cornerRadius, Color bgColor)
    {
        var ctrl = new TileControl();
        ctrl.Apply(tile, cornerRadius, bgColor);
        ctrl.EditRequested   += OnTileEditRequested;
        ctrl.DeleteRequested += OnTileDeleteRequested;
        ctrl.ResizeStarted   += OnTileResizeStarted;
        return ctrl;
    }

    // system タイル：角丸 Border を外枠として生成し、その中に SystemTileControl を配置
    private Border CreateSystemElement(TileViewModel tile, int cornerRadius, Color bgColor)
    {
        var tileColor = ColorPalette.GetColor(tile.Color);
        double alpha  = ColorPalette.OpacityToDouble(tile.Opacity);
        var border = new Border
        {
            CornerRadius = new CornerRadius(cornerRadius),
            Background   = new SolidColorBrush(
                Color.FromArgb((byte)(255 * alpha), tileColor.R, tileColor.G, tileColor.B)),
            ClipToBounds = true,
        };

        var ctrl = new SystemTileControl();
        ctrl.Apply(tile, cornerRadius, bgColor, border);
        ctrl.EditRequested   += OnTileEditRequested;
        ctrl.DeleteRequested += OnTileDeleteRequested;
        ctrl.ResizeStarted   += OnTileResizeStarted;
        border.Child = ctrl;
        return border;
    }

    private WebViewTileControl GetOrCreateWebViewControl(PageViewModel page, TileViewModel tile)
    {
        var key = (page, tile.Col, tile.Row);
        if (!_webviewCache.TryGetValue(key, out var ctrl))
        {
            ctrl = new WebViewTileControl();
            _webviewCache[key] = ctrl;
        }
        // イベントを毎回再配線（Rebuild のたびに呼ばれるため重複を避けて一旦解除してから再登録）
        ctrl.EditRequested   -= OnTileEditRequested;
        ctrl.DeleteRequested -= OnTileDeleteRequested;
        ctrl.ResizeStarted   -= OnTileResizeStarted;
        ctrl.EditRequested   += OnTileEditRequested;
        ctrl.DeleteRequested += OnTileDeleteRequested;
        ctrl.ResizeStarted   += OnTileResizeStarted;
        ctrl.Apply(tile);
        return ctrl;
    }

    // ─── タイル操作 ───────────────────────────────────────────────────

    private void CreateTileAt(int col, int row)
    {
        if (DataContext is not PageViewModel page) return;
        page.Tiles.Add(TileViewModel.CreateNew(col, row));
    }

    private void OnTileEditRequested(TileViewModel tile)
        => App.LauncherViewModel?.OpenTileEditCommand.Execute(tile);

    private void OnTileDeleteRequested(TileViewModel tile)
        => App.LauncherViewModel?.RequestDeleteTileCommand.Execute(tile);

    // ─── リサイズ ──────────────────────────────────────────────────────

    private void OnTileResizeStarted(TileViewModel tile, MouseButtonEventArgs e)
    {
        _resizingTile = tile;
        TileGrid.CaptureMouse();
        e.Handled = true;
    }

    private void OnTileGridMouseMove(object sender, MouseEventArgs e)
    {
        if (_resizingTile == null) return;
        var (cs, rs) = ComputeResizeSpan(e.GetPosition(TileGrid));
        bool valid = CanPlace(_resizingTile.Col, _resizingTile.Row, cs, rs, _resizingTile);
        ShowPreview(_resizingTile.Col, _resizingTile.Row, cs, rs, valid);
    }

    private void OnTileGridMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizingTile == null) return;
        var (cs, rs) = ComputeResizeSpan(e.GetPosition(TileGrid));
        if (CanPlace(_resizingTile.Col, _resizingTile.Row, cs, rs, _resizingTile))
        {
            _resizingTile.ColSpan = cs;
            _resizingTile.RowSpan = rs;
            Rebuild();
        }
        HidePreview();
        _resizingTile = null;
        TileGrid.ReleaseMouseCapture();
    }

    private (int colSpan, int rowSpan) ComputeResizeSpan(Point mousePos)
    {
        var layout = App.ConfigService.Current.Layout;
        var global = App.ConfigService.Current.Global;
        int step   = layout.TileSize + layout.TileMargin;
        double relX = mousePos.X - _resizingTile!.Col * step;
        double relY = mousePos.Y - _resizingTile!.Row * step;
        int cs = Math.Max(1, (int)Math.Ceiling(relX / step));
        int rs = Math.Max(1, (int)Math.Ceiling(relY / step));
        cs = Math.Min(cs, global.TileCountCols - _resizingTile.Col);
        rs = Math.Min(rs, global.TileCountRows - _resizingTile.Row);
        return (cs, rs);
    }

    // ─── D&D（タイル移動）────────────────────────────────────────────

    private void OnTileGridDragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not PageViewModel page) return;
        if (e.Data.GetData(typeof(TileViewModel)) is not TileViewModel drag) return;

        var pos = e.GetPosition(TileGrid);
        var (col, row) = PositionToCell(pos);

        bool canSwap = page.Tiles.Any(t =>
            t != drag &&
            t.Col == col && t.Row == row &&
            t.ColSpan == drag.ColSpan && t.RowSpan == drag.RowSpan);

        bool valid = canSwap || CanPlace(col, row, drag.ColSpan, drag.RowSpan, drag);
        ShowPreview(col, row, drag.ColSpan, drag.RowSpan, valid);
        e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnTileGridDrop(object sender, DragEventArgs e)
    {
        HidePreview();
        if (DataContext is not PageViewModel page) return;
        if (e.Data.GetData(typeof(TileViewModel)) is not TileViewModel drag) return;

        var pos = e.GetPosition(TileGrid);
        var (col, row) = PositionToCell(pos);

        // 同サイズタイルとの入れ替え
        var target = page.Tiles.FirstOrDefault(t =>
            t != drag &&
            t.Col == col && t.Row == row &&
            t.ColSpan == drag.ColSpan && t.RowSpan == drag.RowSpan);
        if (target != null)
        {
            (target.Col, target.Row) = (drag.Col, drag.Row);
            (drag.Col,   drag.Row)   = (col, row);
            Rebuild();
            return;
        }

        // 通常移動
        if (!CanPlace(col, row, drag.ColSpan, drag.RowSpan, drag)) return;
        drag.Col = col;
        drag.Row = row;
        Rebuild();
    }

    // ─── 編集モードでの外部ファイルドロップ → タイル自動作成 ────────────

    private void OnTileGridFileDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not PageViewModel page) return;
        if (App.LauncherViewModel?.Mode != AppMode.Edit) return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (paths.Length == 0) return;

        var pos        = e.GetPosition(TileGrid);
        var (col, row) = PositionToCell(pos);
        if (!CanPlace(col, row, 1, 1)) return;

        page.Tiles.Add(CreateTileFromFile(paths[0], col, row));
    }

    private static TileViewModel CreateTileFromFile(string path, int col, int row)
    {
        string ext      = System.IO.Path.GetExtension(path).ToLowerInvariant();
        string title    = System.IO.Path.GetFileNameWithoutExtension(path);
        string args     = "";
        string workDir  = "";
        string iconPath = "";

        // .lnk ショートカット解決（引数・作業フォルダ・アイコンも取得）
        if (ext == ".lnk")
        {
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell    = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(path);

                    string target  = (string)shortcut.TargetPath;
                    string lnkArgs = (string)shortcut.Arguments;
                    string lnkWork = (string)shortcut.WorkingDirectory;
                    string lnkIcon = (string)shortcut.IconLocation; // "filepath,index" 形式

                    if (!string.IsNullOrEmpty(target))
                    {
                        path = target;
                        ext  = System.IO.Path.GetExtension(path).ToLowerInvariant();
                    }
                    if (!string.IsNullOrEmpty(lnkArgs)) args    = lnkArgs;
                    if (!string.IsNullOrEmpty(lnkWork)) workDir = lnkWork;

                    // ショートカット指定のアイコンが単独の .ico / .png ファイルであれば直接使用
                    if (!string.IsNullOrEmpty(lnkIcon))
                    {
                        string iconFile = lnkIcon.Split(',')[0].Trim();
                        string iconExt  = System.IO.Path.GetExtension(iconFile).ToLowerInvariant();
                        if (System.IO.File.Exists(iconFile) && iconExt is ".ico" or ".png")
                            iconPath = iconFile;
                    }
                }
            }
            catch { }
        }

        bool isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".ico" or ".bmp";

        if (!isImage)
        {
            // 作業フォルダ：未設定の場合はファイルの親ディレクトリ
            if (string.IsNullOrEmpty(workDir))
                workDir = System.IO.Path.GetDirectoryName(path) ?? "";

            // アイコン未取得の場合：ファイルから抽出して PNG として保存
            if (string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(path))
            {
                string iconDir = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "config", "icons");
                iconPath = IconExtractHelper.ExtractAndSave(path, iconDir) ?? "";
            }
        }

        return new TileViewModel(new TileConfig
        {
            Col      = col,    Row      = row,
            ColSpan  = 1,      RowSpan  = 1,
            Type     = "app",
            Title    = title,
            Path     = isImage ? "" : path,
            Args     = args,
            WorkDir  = workDir,
            Color    = "blue", Opacity  = 20,
            FontSizePt   = 16, FontColor = "white",
            ImagePath    = isImage ? path : iconPath,
            ImagePosition = "top",
        });
    }

    // ─── 競合チェック ─────────────────────────────────────────────────

    private bool CanPlace(int col, int row, int colSpan, int rowSpan, TileViewModel? exclude = null)
    {
        if (DataContext is not PageViewModel page) return false;
        var global = App.ConfigService.Current.Global;
        if (col < 0 || row < 0 ||
            col + colSpan > global.TileCountCols ||
            row + rowSpan > global.TileCountRows) return false;

        foreach (var tile in page.Tiles)
        {
            if (tile == exclude) continue;
            bool ox = col < tile.Col + tile.ColSpan && col + colSpan > tile.Col;
            bool oy = row < tile.Row + tile.RowSpan && row + rowSpan > tile.Row;
            if (ox && oy) return false;
        }
        return true;
    }

    // ─── ユーティリティ ───────────────────────────────────────────────

    private (int col, int row) PositionToCell(Point pt)
    {
        var layout = App.ConfigService.Current.Layout;
        var global = App.ConfigService.Current.Global;
        int step = layout.TileSize + layout.TileMargin;
        int col  = Math.Clamp((int)(pt.X / step), 0, global.TileCountCols - 1);
        int row  = Math.Clamp((int)(pt.Y / step), 0, global.TileCountRows - 1);
        return (col, row);
    }

    private void ShowPreview(int col, int row, int colSpan, int rowSpan, bool valid)
    {
        var layout = App.ConfigService.Current.Layout;
        int step = layout.TileSize + layout.TileMargin;
        Canvas.SetLeft(DragPreview, col * step);
        Canvas.SetTop(DragPreview, row * step);
        DragPreview.Width  = layout.TileSize * colSpan + layout.TileMargin * (colSpan - 1);
        DragPreview.Height = layout.TileSize * rowSpan + layout.TileMargin * (rowSpan - 1);
        DragPreview.Stroke = valid ? Brushes.White : Brushes.OrangeRed;
        DragPreview.Visibility = Visibility.Visible;
    }

    private void HidePreview() => DragPreview.Visibility = Visibility.Collapsed;
}
