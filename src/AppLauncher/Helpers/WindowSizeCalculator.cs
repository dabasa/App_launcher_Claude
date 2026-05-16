using System.Windows;
using AppLauncher.Models.Config;

namespace AppLauncher.Helpers;

public static class WindowSizeCalculator
{
    // ボトムバーの高さ（通常モード: 32px、編集モード: 25+8+32 = 65px）
    public const int BottomBarHeightNormal = 32;
    public const int BottomBarHeightEdit = 65;

    public static (double Width, double Height) Calculate(
        GlobalConfig global, LayoutConfig layout, bool editMode = false)
    {
        // PageName 行の実際の高さ：
        //   fontSizePt → px 変換（× 4/3）後、
        //   システムフォント（Segoe UI 等）のラインスペーシング比を乗算して切り上げ
        double fontSizePx = global.PageNameFontSizePt * 4.0 / 3.0;
        int pageNameH = (int)Math.Ceiling(fontSizePx * SystemFonts.MessageFontFamily.LineSpacing);

        int bottomBarH = editMode ? BottomBarHeightEdit : BottomBarHeightNormal;

        int tileAreaW = layout.TileSize * global.TileCountCols
                      + layout.TileMargin * (global.TileCountCols - 1);
        int tileAreaH = layout.TileSize * global.TileCountRows
                      + layout.TileMargin * (global.TileCountRows - 1);

        // フレーム内部コンテンツのサイズ
        double contentW = 2 * layout.TileSideMargin + tileAreaW;
        double contentH = layout.PageNameTopMargin
                        + pageNameH
                        + layout.PageNameBottomMargin
                        + tileAreaH
                        + layout.BottomBarTopMargin
                        + bottomBarH
                        + layout.BottomBarBottomMargin;

        // フレーム外形サイズ：上下左右の BorderThickness を加算する
        // （BorderThickness 分だけ内部コンテンツ領域が縮小されるため補正）
        int bt = layout.FrameBorderThickness;
        double width  = contentW + 2 * bt;
        double height = contentH + 2 * bt;

        return (width, height);
    }
}
