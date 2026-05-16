using System.Windows.Controls;
using System.Windows.Media;
using AppLauncher.Models;

namespace AppLauncher.Views.Controls;

public partial class HandleControl : UserControl
{
    public HandleControl() => InitializeComponent();

    public void SetAppearance(string colorName, int transparencyPercent)
    {
        var color = ColorPalette.GetColor(colorName);
        double alpha = ColorPalette.OpacityToDouble(transparencyPercent);
        HandleBorder.Background = new SolidColorBrush(
            Color.FromArgb((byte)(255 * alpha), color.R, color.G, color.B));
    }
}
