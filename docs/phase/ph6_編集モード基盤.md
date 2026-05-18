# Ph.6 実装指示書 ─ 編集モード基盤

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.6 編集モード基盤 |
| 実装目的 | 通常/編集モード切替・ボトムバー2行化・ページ管理（追加・削除・順序変更）の基盤実装 |
| 未確定事項 | なし |
| 既存影響 | `LauncherViewModel` にコマンド追加。`SnapService` に高さ管理を追加。`BottomBarControl` を全面改修。`LauncherWindow.xaml` の BottomBar 行を Auto 高さへ変更 |
| 追加ライブラリ | なし |
| 実装範囲 | 編集モード切替・ピン自動化・ボトムバー2行レイアウト・ページ管理コントロール。タイル編集・ページ編集設定画面・全体設定画面は対象外（Ph.7 / Ph.8） |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `ViewModels/LauncherViewModel.cs` | 変更：`Pages` を ObservableCollection へ変換、モード切替・ページ管理コマンド追加 |
| `Services/SnapService.cs` | 変更：モード変更時のウィンドウ高さ調整 |
| `Views/Controls/BottomBarControl.xaml` | 変更：2行レイアウト・ページ管理行追加 |
| `Views/Controls/BottomBarControl.xaml.cs` | 変更：編集モード対応・ページ管理ボタン配線 |
| `Views/LauncherWindow.xaml` | 変更：BottomBar 行を `Height="32"` → `Height="Auto"` |

---

## 実装仕様

### 1. LauncherViewModel の変更

#### 変更点

- `Pages` の型を `List<PageViewModel>` → `ObservableCollection<PageViewModel>` に変更
- 編集モード突入前のピン状態を保存する `_pinnedBeforeEdit` フィールドを追加
- 5つのコマンドを追加（`ToggleModeCommand`, `AddPageCommand`, `RemovePageCommand`, `MovePageLeftCommand`, `MovePageRightCommand`）

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppLauncher.Models;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class LauncherViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private int _currentPageIndex;

    [ObservableProperty]
    private AppMode _mode = AppMode.Normal;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isStored;

    // 編集モード突入前のピン状態を保存（編集モード終了時に復元）
    private bool _pinnedBeforeEdit;

    public ObservableCollection<PageViewModel> Pages { get; }

    public double PageNameFontSize { get; }

    public PageViewModel CurrentPage => Pages[CurrentPageIndex];

    public LauncherViewModel(AppConfig config)
    {
        Pages = new ObservableCollection<PageViewModel>(
            config.Pages.Select(p => new PageViewModel(p)));
        PageNameFontSize = config.Global.PageNameFontSizePt * 4.0 / 3.0;
    }

    [RelayCommand]
    private void NavigateToPage(int index)
    {
        if (index >= 0 && index < Pages.Count)
            CurrentPageIndex = index;
    }

    /// <summary>
    /// 通常 ↔ 編集モードを切り替える。
    /// 編集モード突入時はピンモードを強制 ON。終了時は突入前の状態へ復元。
    /// </summary>
    [RelayCommand]
    private void ToggleMode()
    {
        if (Mode == AppMode.Normal)
        {
            _pinnedBeforeEdit = IsPinned;
            Mode    = AppMode.Edit;
            IsPinned = true;
        }
        else
        {
            Mode    = AppMode.Normal;
            IsPinned = _pinnedBeforeEdit;
        }
    }

    /// <summary>
    /// 現在ページの直後に新ページを挿入して移動する。配色は現在ページを継承。上限10。
    /// </summary>
    [RelayCommand]
    private void AddPage()
    {
        if (Pages.Count >= 10) return;
        var src = CurrentPage;
        var cfg = new PageConfig
        {
            Name            = $"ページ{Pages.Count + 1}",
            BackgroundColor = src.BackgroundColor,
            BackgroundOpacity = src.BackgroundOpacity,
            HandleColor     = src.HandleColor,
            HandleOpacity   = src.HandleOpacity,
            PinFrameColor   = src.PinFrameColor,
            PinFrameOpacity = src.PinFrameOpacity,
        };
        int insertAt = CurrentPageIndex + 1;
        Pages.Insert(insertAt, new PageViewModel(cfg));
        CurrentPageIndex = insertAt;
    }

    /// <summary>
    /// 現在ページを削除する。タイル未配置・ページ数2以上の場合のみ実行。
    /// 削除後は左隣へ移動（先頭の場合は右隣）。
    /// </summary>
    [RelayCommand]
    private void RemovePage()
    {
        if (Pages.Count <= 1) return;
        if (CurrentPage.Tiles.Count > 0) return;
        int removed = CurrentPageIndex;
        Pages.RemoveAt(removed);
        CurrentPageIndex = removed > 0 ? removed - 1 : 0;
    }

    /// <summary>現在ページを左隣と入れ替える（先頭では何もしない）。</summary>
    [RelayCommand]
    private void MovePageLeft()
    {
        if (CurrentPageIndex <= 0) return;
        int i = CurrentPageIndex;
        (Pages[i], Pages[i - 1]) = (Pages[i - 1], Pages[i]);
        CurrentPageIndex = i - 1;
    }

    /// <summary>現在ページを右隣と入れ替える（末尾では何もしない）。</summary>
    [RelayCommand]
    private void MovePageRight()
    {
        if (CurrentPageIndex >= Pages.Count - 1) return;
        int i = CurrentPageIndex;
        (Pages[i], Pages[i + 1]) = (Pages[i + 1], Pages[i]);
        CurrentPageIndex = i + 1;
    }
}
```

---

### 2. SnapService の変更

#### 変更点

- `_baseFrameH` フィールドを追加（通常モード時のフレーム高さを記憶）
- `Attach()` で `LauncherViewModel.PropertyChanged` を購読し、`Mode` 変更時に高さを調整
- `ApplySnap()` で `_baseFrameH` を保存し `UpdateWindowHeight()` を呼ぶ
- `UpdateWindowHeight()` を追加

追加・変更する部分のみ示す（他は現状のまま）。

```csharp
// ─── フレーム高さ ─────────────────────────────────────────────────────────
private double _baseFrameH;  // 追加

// ─── Attach への追記 ─────────────────────────────────────────────────────
public void Attach(LauncherWindow window)
{
    _window = window;
    _window.MouseLeftButtonDown += OnMouseDown;
    _window.MouseMove           += OnMouseMove;
    _window.AddHandler(UIElement.MouseLeftButtonUpEvent,
        new MouseButtonEventHandler(OnMouseUp), handledEventsToo: true);
    _window.MouseEnter += OnWindowMouseEnter;
    _window.MouseLeave += OnWindowMouseLeave;

    // モード変更でウィンドウ高さを調整
    if (App.LauncherViewModel != null)
        App.LauncherViewModel.PropertyChanged += OnViewModelPropertyChanged;
}

private void OnViewModelPropertyChanged(object? sender,
    System.ComponentModel.PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(LauncherViewModel.Mode))
        UpdateWindowHeight();
}

// ─── ApplySnap の変更（_window.Height = frameH; 部分のみ差し替え）─────────
public void ApplySnap()
{
    var config = App.ConfigService.Current;
    var layout = config.Layout;
    var global = config.Global;

    var (frameW, frameH) = WindowSizeCalculator.Calculate(global, layout);
    double windowW = layout.HandleShortSide + layout.HandleFrameMargin + frameW;

    _window.Width = windowW;
    _baseFrameH   = frameH;   // ← 追加
    UpdateWindowHeight();     // ← frameH 直接代入の代わりに

    double screenW = SystemParameters.PrimaryScreenWidth;
    double screenH = SystemParameters.PrimaryScreenHeight;

    _normalLeft = screenW - layout.ScreenEdgeDistance - frameW
                  - layout.HandleFrameMargin - layout.HandleShortSide;
    _storedLeft = _normalLeft + frameW + layout.HandleFrameMargin;

    _window.Left = _normalLeft;
    _window.Top  = (screenH - _window.Height) / 2;
}

/// <summary>
/// モードに応じてウィンドウ高さを更新する。
/// 編集モード時は BottomBar 2行分（33 px）だけ拡張する。
/// </summary>
private void UpdateWindowHeight()
{
    bool isEdit = App.LauncherViewModel?.Mode == AppMode.Edit;
    _window.Height = _baseFrameH + (isEdit ? 33 : 0);
}
```

using に `AppLauncher.Models` を追加（`AppMode` 参照のため）。

---

### 3. LauncherWindow.xaml の変更

BottomBar 行（Row 5）の高さを固定値 `32` から `Auto` へ変更する。
これにより `BottomBarControl` の内容物がそのまま行高さになる。

```xml
<!-- 変更前 -->
<RowDefinition Height="32"/>   <!-- BottomBarControl -->

<!-- 変更後 -->
<RowDefinition Height="Auto"/> <!-- BottomBarControl -->
```

---

### 4. BottomBarControl.xaml の変更

単一 Grid から StackPanel ＋ 2行 Grid 構成に全面変更する。

```xml
<UserControl x:Class="AppLauncher.Views.Controls.BottomBarControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <!-- 行1（常時表示）＋ 行2（編集モード時のみ）の縦積み -->
    <StackPanel Orientation="Vertical">

        <!-- ═══ 行1 ═══════════════════════════════════════════════════ -->
        <Grid Height="32">
            <!-- ページインジケーター（中央） -->
            <StackPanel x:Name="IndicatorPanel"
                        Orientation="Horizontal"
                        HorizontalAlignment="Center"
                        VerticalAlignment="Center"/>

            <!-- 右側ボタン群 -->
            <StackPanel x:Name="RightButtons"
                        Orientation="Horizontal"
                        HorizontalAlignment="Right"
                        VerticalAlignment="Center"
                        Margin="0,0,20,0">

                <!-- ページ編集ボタン（Ph.8 実装予定・現在はプレースホルダー） -->
                <Border x:Name="PageEditButton"
                        Width="32" Height="32"
                        Margin="0,0,8,0"
                        Cursor="Hand"
                        Visibility="Collapsed"/>

                <!-- 全体設定ボタン（Ph.8 実装予定・現在はプレースホルダー） -->
                <Border x:Name="GlobalSettingsButton"
                        Width="32" Height="32"
                        Margin="0,0,8,0"
                        Cursor="Hand"
                        Visibility="Collapsed"/>

                <!-- モード切替ボタン -->
                <Border x:Name="ModeButton"
                        Width="32" Height="32"
                        Cursor="Hand"/>
            </StackPanel>
        </Grid>

        <!-- ═══ 行2：ページ管理コントロール（編集モード時のみ表示） ════ -->
        <Grid x:Name="PageMgmtRow"
              Height="25"
              Margin="0,8,0,0"
              Visibility="Collapsed">
            <StackPanel Orientation="Horizontal"
                        HorizontalAlignment="Center"
                        VerticalAlignment="Center">

                <!-- ← ページ左移動 -->
                <Border x:Name="MoveLeftButton"
                        Width="25" Height="25"
                        Margin="0,0,8,0"
                        Cursor="Hand"/>

                <!-- ⊕ ページ追加 -->
                <Border x:Name="AddPageButton"
                        Width="25" Height="25"
                        Margin="0,0,8,0"
                        Cursor="Hand"/>

                <!-- ⊖ ページ削除 -->
                <Border x:Name="RemovePageButton"
                        Width="25" Height="25"
                        Margin="0,0,8,0"
                        Cursor="Hand"/>

                <!-- → ページ右移動 -->
                <Border x:Name="MoveRightButton"
                        Width="25" Height="25"
                        Cursor="Hand"/>
            </StackPanel>
        </Grid>

    </StackPanel>
</UserControl>
```

---

### 5. BottomBarControl.xaml.cs の変更

`Rebuild()` でモードに応じた表示切替を行い、ページ管理ボタンのクリックを配線する。

```csharp
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AppLauncher.Models;
using AppLauncher.ViewModels;

namespace AppLauncher.Views.Controls;

public partial class BottomBarControl : UserControl
{
    private LauncherViewModel? _vm;

    public BottomBarControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        WirePageMgmtButtons();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.Pages.CollectionChanged -= OnPagesCollectionChanged;
        }

        _vm = DataContext as LauncherViewModel;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.Pages.CollectionChanged += OnPagesCollectionChanged;
            Rebuild();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherViewModel.CurrentPage)
                           or nameof(LauncherViewModel.CurrentPageIndex)
                           or nameof(LauncherViewModel.Mode))
            Rebuild();
    }

    private void OnPagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Rebuild();

    private void WirePageMgmtButtons()
    {
        MoveLeftButton.MouseLeftButtonUp   += (_, _) => _vm?.MovePageLeftCommand.Execute(null);
        AddPageButton.MouseLeftButtonUp    += (_, _) => _vm?.AddPageCommand.Execute(null);
        RemovePageButton.MouseLeftButtonUp += (_, _) => _vm?.RemovePageCommand.Execute(null);
        MoveRightButton.MouseLeftButtonUp  += (_, _) => _vm?.MovePageRightCommand.Execute(null);
        ModeButton.MouseLeftButtonUp       += (_, _) => _vm?.ToggleModeCommand.Execute(null);
    }

    private void Rebuild()
    {
        if (_vm == null) return;

        bool isEdit = _vm.Mode == AppMode.Edit;
        var layout  = App.ConfigService.Current.Layout;

        // ─── モードボタン色 ───────────────────────────────────────────
        var modeColor = isEdit
            ? ColorPalette.GetColor("orange")                           // 編集中はオレンジ
            : ColorPalette.GetColor(_vm.CurrentPage.BackgroundColor);   // 通常時はページ背景色
        ModeButton.Background    = new SolidColorBrush(modeColor);
        ModeButton.CornerRadius  = new CornerRadius(layout.SettingsButtonCornerRadius);

        // ─── 行2 表示切替 ────────────────────────────────────────────
        PageMgmtRow.Visibility = isEdit ? Visibility.Visible : Visibility.Collapsed;

        // ─── ページ管理ボタンの活性制御 ──────────────────────────────
        if (isEdit)
        {
            UpdatePageMgmtButtons();
        }

        // ─── ページインジケーター再構築 ──────────────────────────────
        RebuildIndicators();
    }

    private void UpdatePageMgmtButtons()
    {
        if (_vm == null) return;

        bool canAdd    = _vm.Pages.Count < 10;
        bool canRemove = _vm.Pages.Count > 1 && _vm.CurrentPage.Tiles.Count == 0;
        bool canLeft   = _vm.CurrentPageIndex > 0;
        bool canRight  = _vm.CurrentPageIndex < _vm.Pages.Count - 1;

        SetButtonActive(AddPageButton,    canAdd);
        SetButtonActive(RemovePageButton, canRemove);
        SetButtonActive(MoveLeftButton,   canLeft);
        SetButtonActive(MoveRightButton,  canRight);

        // ボタンラベル（TextBlock で各 Border に子要素を設定）
        EnsureButtonLabel(AddPageButton,    "⊕");
        EnsureButtonLabel(RemovePageButton, "⊖");
        EnsureButtonLabel(MoveLeftButton,   "←");
        EnsureButtonLabel(MoveRightButton,  "→");
    }

    private static void SetButtonActive(Border btn, bool active)
    {
        btn.Opacity = active ? 1.0 : 0.3;
        btn.IsHitTestVisible = active;
    }

    private static void EnsureButtonLabel(Border btn, string text)
    {
        if (btn.Child is TextBlock) return;
        btn.Child = new TextBlock
        {
            Text                = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Foreground          = Brushes.White,
            FontSize            = 14,
        };
        btn.Background = new SolidColorBrush(Colors.Gray) { Opacity = 0.6 };
        btn.CornerRadius = new CornerRadius(App.ConfigService.Current.Layout.PageOpButtonSize / 2.0);
    }

    private void RebuildIndicators()
    {
        if (_vm == null) return;
        IndicatorPanel.Children.Clear();

        for (int i = 0; i < _vm.Pages.Count; i++)
        {
            var page     = _vm.Pages[i];
            bool isCurrent = (i == _vm.CurrentPageIndex);
            var color    = ColorPalette.GetColor(page.BackgroundColor);
            var brush    = new SolidColorBrush(color);
            int pageIndex = i;

            var indicator = new Grid
            {
                Width      = 18,
                Height     = 18,
                Cursor     = Cursors.Hand,
                Background = Brushes.Transparent,
                Margin     = i < _vm.Pages.Count - 1
                    ? new Thickness(0, 0, 8, 0)
                    : new Thickness(0),
            };
            indicator.MouseLeftButtonUp += (_, _) =>
                _vm.NavigateToPageCommand.Execute(pageIndex);

            if (isCurrent)
            {
                indicator.Children.Add(new Ellipse { Width = 18, Height = 18, Fill = brush });
                indicator.Children.Add(new Ellipse
                {
                    Width               = 7,
                    Height              = 7,
                    Fill                = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                });
            }
            else
            {
                indicator.Children.Add(new Ellipse
                {
                    Width           = 18,
                    Height          = 18,
                    Stroke          = brush,
                    StrokeThickness = 2,
                    Fill            = Brushes.Transparent,
                });
            }

            IndicatorPanel.Children.Add(indicator);
        }
    }
}
```

---

## 完了条件

- [ ] モード切替ボタンをクリックすると通常モード ↔ 編集モードが切り替わる
- [ ] 編集モード切替時にピンモードが強制 ON になりフレーム外周に枠が表示される
- [ ] 通常モードへ戻るとピンモードが編集モード突入前の状態に復元される
- [ ] 編集モード時にモード切替ボタンがオレンジ（`#E8843A`）で表示される
- [ ] 編集モード時にボトムバーが2行になり、ランチャーフレームが 33 px 下方向に拡張される
- [ ] 通常モードへ戻るとフレームが元のサイズに戻る
- [ ] ページ管理行（← ⊕ ⊖ →）が編集モード時のみ表示される
- [ ] `⊕` でページが追加され、現在ページの配色が継承される（上限10で非活性）
- [ ] `⊖` でタイル未配置のページが削除される（1ページのみ・タイルありで非活性）
- [ ] `←` / `→` でページ順序が入れ替わる（端で非活性）
- [ ] ページ削除後は左隣（先頭削除時は右隣）へ自動移動する
- [ ] `dotnet build` がエラーなく成功する

---

## 参照定義書

- `definition/function/アプリケーション機能定義.md § 2.3` — 編集モード遷移条件・ピン自動化・ページ削除後の移動
- `definition/ui/ランチャー本体.md § 7` — ボトムバーのモード別レイアウト・行2の構成
- `definition/ui/配置位置数値定義.md § 4` — ボトムバー高さ（通常32px・編集65px）・ページ管理ボタンサイズ（25px）
