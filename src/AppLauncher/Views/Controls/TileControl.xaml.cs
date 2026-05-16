using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AppLauncher.Models;
using AppLauncher.Services;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class TileControl : UserControl
{
    private TileViewModel? _tile;
    private Color _pageBackgroundColor;
    private Point _mouseDownPos;

    public TileControl() => InitializeComponent();

    public void Apply(TileViewModel tile, int cornerRadius, Color pageBackgroundColor)
    {
        _tile = tile;
        _pageBackgroundColor = pageBackgroundColor;

        var bgColor = ColorPalette.GetColor(tile.Color);
        double alpha = ColorPalette.OpacityToDouble(tile.Opacity);
        TileBorder.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), bgColor.R, bgColor.G, bgColor.B));
        TileBorder.CornerRadius = new CornerRadius(cornerRadius);

        TitleText.Text = tile.Title;
        TitleText.FontSize = tile.FontSizePt * 4.0 / 3.0;
        TitleText.Foreground = new SolidColorBrush(ColorPalette.GetColor(tile.FontColor));
        if (!string.IsNullOrEmpty(tile.FontName))
            TitleText.FontFamily = new FontFamily(tile.FontName);
    }

    // ─── クリック ─────────────────────────────────────────────────────────
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _mouseDownPos = e.GetPosition(this);
        // e.Handled = false のまま → SnapService がドラッグ開始を検知できるようにする
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_tile == null) return;
        if (App.LauncherViewModel?.Mode != AppMode.Normal) return;

        double dist = (e.GetPosition(this) - _mouseDownPos).Length;
        if (dist < 5.0)
        {
            e.Handled = true; // ドラッグではなくクリックと判定
            TileLaunchService.Launch(_tile);
        }
    }

    // ─── ホバー ───────────────────────────────────────────────────────────
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (_tile == null) return;
        if (App.LauncherViewModel?.Mode != AppMode.Normal) return;

        if (_tile.Args.Contains("{drop}"))
        {
            // D&D 専用タイル：オーバーレイを表示（アニメーションなし）
            DndOverlay.Background = new SolidColorBrush(_pageBackgroundColor);
            DndOverlay.Visibility = Visibility.Visible;
            return;
        }

        // 通常タイル：底面基準 1.13 倍へアニメーション
        Panel.SetZIndex(this, 100);
        AnimateScale(1.13);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        DndOverlay.Visibility = Visibility.Collapsed;
        Panel.SetZIndex(this, 0);
        AnimateScale(1.0);
    }

    private void AnimateScale(double to)
    {
        var duration = TimeSpan.FromMilliseconds(120);
        HoverScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(to, duration));
        HoverScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(to, duration));
    }
}
