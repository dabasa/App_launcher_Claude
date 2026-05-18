using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppLauncher.Models;
using AppLauncher.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace AppLauncher.Views.Controls;

public partial class WebViewTileControl : UserControl
{
    private TileViewModel? _tile;
    private bool _navigated;

    public event Action<TileViewModel>? EditRequested;
    public event Action<TileViewModel>? DeleteRequested;
    public event Action<TileViewModel, MouseButtonEventArgs>? ResizeStarted;

    public WebViewTileControl()
    {
        InitializeComponent();
        EditButton.MouseLeftButtonUp     += (_, e) => { e.Handled = true; if (_tile != null) EditRequested?.Invoke(_tile); };
        DeleteButton.MouseLeftButtonUp   += (_, e) => { e.Handled = true; if (_tile != null) DeleteRequested?.Invoke(_tile); };
        ResizeHandle.MouseLeftButtonDown += (_, e) => { if (_tile != null) { e.Handled = true; ResizeStarted?.Invoke(_tile, e); } };

        WebView.CoreWebView2InitializationCompleted += OnCoreWebView2Ready;
        _ = WebView.EnsureCoreWebView2Async();
    }

    public void Apply(TileViewModel tile)
    {
        _tile = tile;
        if (WebView.CoreWebView2 != null && !_navigated)
            Navigate(tile.Path);
    }

    private void OnCoreWebView2Ready(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess && _tile != null && !_navigated)
            Navigate(_tile.Path);
    }

    private void Navigate(string path)
    {
        if (string.IsNullOrEmpty(path) || WebView.CoreWebView2 == null) return;
        _navigated = true;
        try
        {
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https")
                WebView.CoreWebView2.Navigate(path);
            else
                WebView.CoreWebView2.Navigate(new Uri(path, UriKind.Absolute).ToString());
        }
        catch { }
    }

    // ─── 編集モードオーバーレイ ────────────────────────────────────────────
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (App.LauncherViewModel?.Mode == AppMode.Edit)
            EditOverlay.Visibility = Visibility.Visible;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        EditOverlay.Visibility = Visibility.Collapsed;
    }
}
