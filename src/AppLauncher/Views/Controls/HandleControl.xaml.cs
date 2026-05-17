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
