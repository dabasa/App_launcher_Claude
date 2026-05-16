using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AppLauncher.Models;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class TileGridControl : UserControl
{
    public TileGridControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        TileGrid.Children.Clear();
        TileGrid.ColumnDefinitions.Clear();
        TileGrid.RowDefinitions.Clear();

        if (DataContext is not PageViewModel page) return;

        var config = App.ConfigService.Current;
        var layout = config.Layout;
        var global = config.Global;

        int cols = global.TileCountCols;
        int rows = global.TileCountRows;
        int tileSize = layout.TileSize;
        int tileMargin = layout.TileMargin;
        int cornerRadius = layout.TileCornerRadius;

        // タイル列とスペーサー列を交互に定義
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
        {
            for (int dc = 0; dc < tile.ColSpan; dc++)
                for (int dr = 0; dr < tile.RowSpan; dr++)
                {
                    int c = tile.Col + dc;
                    int r = tile.Row + dr;
                    if (c < cols && r < rows)
                        occupied[c, r] = true;
                }
        }

        // 空スロット：点線枠
        var bgColor = ColorPalette.GetColor(page.BackgroundColor);
        var slotBrush = new SolidColorBrush(
            Color.FromArgb((byte)(255 * 0.4), bgColor.R, bgColor.G, bgColor.B));

        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows; r++)
            {
                if (!occupied[c, r])
                {
                    var rect = new Rectangle
                    {
                        Stroke = slotBrush,
                        StrokeThickness = 1.5,
                        StrokeDashArray = new DoubleCollection([6.0, 4.0]),
                        Fill = Brushes.Transparent,
                    };
                    Grid.SetColumn(rect, c * 2);
                    Grid.SetRow(rect, r * 2);
                    TileGrid.Children.Add(rect);
                }
            }
        }

        // タイル配置
        foreach (var tile in page.Tiles)
        {
            var control = new TileControl();
            control.Apply(tile, cornerRadius, bgColor);
            Grid.SetColumn(control, tile.Col * 2);
            Grid.SetRow(control, tile.Row * 2);
            Grid.SetColumnSpan(control, tile.ColSpan * 2 - 1);
            Grid.SetRowSpan(control, tile.RowSpan * 2 - 1);
            TileGrid.Children.Add(control);
        }
    }
}
