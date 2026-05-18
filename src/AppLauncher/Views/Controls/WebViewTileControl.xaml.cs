using System.ComponentModel;
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
    private bool _modeSubscribed;
    private Point _mouseDownPos;
    private bool _dragStarted;

    public event Action<TileViewModel>? EditRequested;
    public event Action<TileViewModel>? DeleteRequested;
    public event Action<TileViewModel, MouseButtonEventArgs>? ResizeStarted;

    public WebViewTileControl()
    {
        InitializeComponent();
        EditButton.MouseLeftButtonUp     += (_, e) => { e.Handled = true; if (_tile != null) EditRequested?.Invoke(_tile); };
        DeleteButton.MouseLeftButtonUp   += (_, e) => { e.Handled = true; if (_tile != null) DeleteRequested?.Invoke(_tile); };
        ResizeHandle.MouseLeftButtonDown += (_, e) => { if (_tile != null) { e.Handled = true; ResizeStarted?.Invoke(_tile, e); } };

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;

        WebView.CoreWebView2InitializationCompleted += OnCoreWebView2Ready;
        _ = WebView.EnsureCoreWebView2Async();
    }

    public void Apply(TileViewModel tile)
    {
        _tile = tile;
        PlaceholderTitle.Text = tile.Title;
        PlaceholderUrl.Text   = tile.Path;
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

    // ─── ライフサイクル ────────────────────────────────────────────────────
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeMode();
        ApplyMode();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeMode();
    }

    private void SubscribeMode()
    {
        if (_modeSubscribed || App.LauncherViewModel is not { } vm) return;
        vm.PropertyChanged += OnVmPropertyChanged;
        _modeSubscribed = true;
    }

    private void UnsubscribeMode()
    {
        if (!_modeSubscribed || App.LauncherViewModel is not { } vm) return;
        vm.PropertyChanged -= OnVmPropertyChanged;
        _modeSubscribed = false;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherViewModel.Mode))
            ApplyMode();
    }

    // WebView2 は HwndHost (Win32 HWND) のため Normal 以外では WPF レイヤーを覆ってしまう。
    // Normal 以外のモードでは WebView を非表示にしてプレースホルダーを表示する。
    private void ApplyMode()
    {
        bool isNormal = App.LauncherViewModel?.Mode == AppMode.Normal;
        WebView.Visibility            = isNormal ? Visibility.Visible   : Visibility.Collapsed;
        WebViewPlaceholder.Visibility = isNormal ? Visibility.Collapsed : Visibility.Visible;
        if (isNormal)
            EditOverlay.Visibility = Visibility.Collapsed;
    }

    // ─── 編集モードオーバーレイ ────────────────────────────────────────────
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateEditOverlay();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        EditOverlay.Visibility = Visibility.Collapsed;
    }

    private void UpdateEditOverlay()
    {
        EditOverlay.Visibility = IsMouseOver && App.LauncherViewModel?.Mode == AppMode.Edit
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ─── タイル移動 D&D ──────────────────────────────────────────────────
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _mouseDownPos = e.GetPosition(this);
        _dragStarted  = false;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (App.LauncherViewModel?.Mode != AppMode.Edit) return;
        if (_tile == null || _dragStarted) return;
        if ((e.GetPosition(this) - _mouseDownPos).Length < 5.0) return;

        _dragStarted = true;
        DragDrop.DoDragDrop(this, _tile, DragDropEffects.Move);
    }
}
