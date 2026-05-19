using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AppLauncher.Models;

namespace AppLauncher.Views.Controls;

public partial class HandleControl : UserControl
{
    public HandleControl()
    {
        InitializeComponent();
        var layout = App.ConfigService.Current.Layout;
        Width  = layout.HandleShortSide;
        Height = layout.HandleLongSide;
    }

    public void SetOrientation(bool horizontal)
    {
        var layout = App.ConfigService.Current.Layout;
        if (horizontal)
        {
            Width  = layout.HandleLongSide;
            Height = layout.HandleShortSide;
            HandleBorder.CornerRadius = new CornerRadius(layout.HandleShortSide / 2.0);
        }
        else
        {
            Width  = layout.HandleShortSide;
            Height = layout.HandleLongSide;
            HandleBorder.CornerRadius = new CornerRadius(layout.HandleCornerRadius);
        }
    }

    public void SetAppearance(string colorName, int transparencyPercent)
    {
        var color = ColorPalette.GetColor(colorName);
        double alpha = ColorPalette.OpacityToDouble(transparencyPercent);
        HandleBorder.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), color.R, color.G, color.B));

        int bt = App.ConfigService.Current.Layout.FrameBorderThickness;
        HandleBorder.BorderThickness = new Thickness(bt);
    }

    public void SetPinBorder(Brush brush, int thickness)
    {
        HandleBorder.BorderBrush     = brush;
        HandleBorder.BorderThickness = new Thickness(thickness);
    }

    public void ResetBorder()
    {
        int bt = App.ConfigService.Current.Layout.FrameBorderThickness;
        HandleBorder.BorderBrush     = new SolidColorBrush(Colors.Black);
        HandleBorder.BorderThickness = new Thickness(bt);
    }
}
