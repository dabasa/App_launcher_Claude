# Ph.F-D 実装指示書 ─ AutoFont 最適サイズ算出モード

対象バージョン：V1.2.0  
作成日：2026-05-24

---

## 概要

自動フォントサイズ調整（`AutoFontSize`）に2つのモードを追加する。

| モード | 動作 |
|---|---|
| `overflow`（見切れ防止） | 既存の動作。72pt から順に小さくし、タイル領域に収まる最大サイズを選択する |
| `optimal`（最適サイズ） | タイルの短辺からフォントサイズを逆算し、読みやすいサイズを算出する。算出後に見切れチェックを行い、収まらなければ縮小する |

両モードとも最大値は `72pt`、最小値は `6pt` に制限する。

**前提**：Ph.F-A（FontConfig 基盤）および Ph.F-B（FontDetailDialog）の実装完了が必要。

---

## 実装内容

### 1. `FontConfig` への追加フィールド

**対象ファイル**

- `src/AppLauncher/Models/Config/FontConfig.cs`

```csharp
public class FontConfig
{
    public string FontName     { get; set; } = "";
    public int    FontSizePt   { get; set; } = 16;
    public string FontColor    { get; set; } = "white";
    public bool   AutoFontSize { get; set; } = false;

    // Ph.F-D 追加
    /// <summary>"overflow" または "optimal"。AutoFontSize が true の場合のみ有効。</summary>
    public string AutoFontSizeMode { get; set; } = "overflow";

    /// <summary>
    /// optimal モード時のサイズ計算係数。タイル短辺（px）に掛けて基準フォントサイズを求める。
    /// 範囲：0.05 ～ 0.50（5% ～ 50%）。デフォルト 0.20（20%）。
    /// 例：タイル短辺 96px × 0.20 = 19.2px → 約 14pt（96DPI換算）。
    /// </summary>
    public double AutoFontOptimalFactor { get; set; } = 0.20;
}
```

---

### 2. `TileControl.xaml.cs` の更新

**対象ファイル**

- `src/AppLauncher/Views/Controls/TileControl.xaml.cs`

**変更内容**

`CalcAutoFontSize()` を `FontConfig` を受け取る形に拡張する。

```csharp
private void UpdateFontSize()
{
    if (_tile == null) return;
    if (!_tile.AutoFontSize)
    {
        TitleText.FontSize = _tile.FontSizePt * 4.0 / 3.0;
        return;
    }
    if (TileBorder.ActualWidth <= 0 || TileBorder.ActualHeight <= 0) return;

    const double pad = 16.0;
    double w = TileBorder.ActualWidth  - pad;
    double h;
    bool hasImage = !string.IsNullOrEmpty(_tile.ImagePath);
    if (hasImage && _tile.ImagePosition is "top" or "bottom")
        h = (TileBorder.ActualHeight - pad) * 0.35;
    else
        h = TileBorder.ActualHeight - pad;

    var   cfg  = _tile.TitleFont;
    double size = cfg.AutoFontSizeMode == "optimal"
        ? CalcOptimalFontSize(cfg.AutoFontOptimalFactor, w, h, _tile.Title)
        : CalcOverflowFontSize(_tile.Title, w, h);

    TitleText.FontSize = size * 4.0 / 3.0;  // pt → device-independent px
}

/// <summary>
/// overflow モード：96pt から順に下げ、テキストが w×h に収まる最大サイズを返す（単位: pt）。
/// </summary>
private double CalcOverflowFontSize(string text, double w, double h)
{
    if (string.IsNullOrEmpty(text) || w <= 0 || h <= 0) return 6;
    var typeface = new Typeface(TitleText.FontFamily, TitleText.FontStyle,
        TitleText.FontWeight, TitleText.FontStretch);
    double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

    for (double size = 96; size >= 6; size--)
    {
        double emPx = size * 4.0 / 3.0;
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, emPx, Brushes.Black, dpi);
        ft.MaxTextWidth = w;
        if (ft.Height <= h && ft.Width <= w) return size;
    }
    return 6;
}

/// <summary>
/// optimal モード：タイル短辺 × factor で基準サイズを算出し、
/// overflow チェックで収まらなければ縮小する（単位: pt）。
/// </summary>
private double CalcOptimalFontSize(double factor, double w, double h, string text)
{
    double shortSidePx = Math.Min(w, h);
    // factor はデバイス独立ピクセル系の係数として扱い、pt に変換
    double basePx = shortSidePx * factor;
    double basePt = Math.Clamp(basePx * 3.0 / 4.0, 6, 72);  // px → pt
    double startPt = Math.Floor(basePt);

    if (string.IsNullOrEmpty(text)) return startPt;

    var typeface = new Typeface(TitleText.FontFamily, TitleText.FontStyle,
        TitleText.FontWeight, TitleText.FontStretch);
    double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

    // 基準サイズから下げながら overflow チェック
    for (double size = startPt; size >= 6; size--)
    {
        double emPx = size * 4.0 / 3.0;
        var ft = new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, emPx, Brushes.Black, dpi);
        ft.MaxTextWidth = w;
        if (ft.Height <= h && ft.Width <= w) return size;
    }
    return 6;
}
```

---

### 3. `SystemTileControl.xaml.cs` の更新

**対象ファイル**

- `src/AppLauncher/Views/Controls/SystemTileControl.xaml.cs`

**変更内容**

`ContentFont` に `AutoFontSize = true` が設定されている場合、  
`MainText` / `SubText` / `CircleCenterText` / `CircleLabel` のフォントサイズを自動計算する。

```csharp
private void UpdateContentFontSize()
{
    if (_tile?.ContentFont is not { AutoFontSize: true } cf) return;
    if (ActualWidth <= 0 || ActualHeight <= 0) return;

    const double pad = 16.0;
    double w = ActualWidth  - pad;
    double h = ActualHeight - pad;
    double shortSide = Math.Min(w, h);

    double sizePt = cf.AutoFontSizeMode == "optimal"
        ? Math.Clamp(shortSide * cf.AutoFontOptimalFactor * 3.0 / 4.0, 6, 72)
        : cf.FontSizePt;  // overflow モードは現時点では手動サイズをベースに使用

    double emPx = sizePt * 4.0 / 3.0;
    MainText.FontSize         = emPx;
    SubText.FontSize          = Math.Max(10, emPx - 4);
    CircleCenterText.FontSize = emPx;
    CircleLabel.FontSize      = Math.Max(8, emPx - 4);
}
```

`Loaded` イベントおよびサイズ変更時に `UpdateContentFontSize()` を呼び出す。

---

### 4. `FontDetailPanel.xaml` の更新（モード選択 UI 追加）

**対象ファイル**

- `src/AppLauncher/Views/Controls/FontDetailPanel.xaml`

**変更内容**

自動調整チェックボックスの下にモード選択ラジオボタンを追加する。  
`AutoFontSize = false` の場合はグレーアウト（`IsEnabled` バインド）。

```xml
<StackPanel IsEnabled="{Binding AutoFontSize}" Margin="16,0,0,12" Opacity="{Binding AutoFontSize, Converter={...BoolToOpacityConverter}}">
    <RadioButton Content="見切れ防止のみ（文字が収まる最大サイズ）"
                 IsChecked="{Binding IsOverflowMode}"
                 Foreground="White" Margin="0,0,0,4"/>
    <RadioButton Content="最適サイズを算出（タイルサイズから逆算）"
                 IsChecked="{Binding IsOptimalMode}"
                 Foreground="White" Margin="0,0,0,4"/>
    <!-- optimal モード専用：係数スライダー -->
    <Grid IsEnabled="{Binding IsOptimalMode}" Margin="16,0,0,0">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="60"/>
        </Grid.ColumnDefinitions>
        <Slider Grid.Column="0"
                Minimum="0.05" Maximum="0.50" TickFrequency="0.05"
                IsSnapToTickEnabled="True"
                Value="{Binding AutoFontOptimalFactor}"
                VerticalAlignment="Center"/>
        <TextBlock Grid.Column="1"
                   Text="{Binding AutoFontOptimalFactor, StringFormat={}{0:P0}}"
                   Foreground="White" TextAlignment="Right" VerticalAlignment="Center"/>
    </Grid>
</StackPanel>
```

---

### 5. `FontDetailViewModel` の更新

**対象ファイル**

- `src/AppLauncher/ViewModels/FontDetailViewModel.cs`

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsOverflowMode))]
[NotifyPropertyChangedFor(nameof(IsOptimalMode))]
private string _autoFontSizeMode = "overflow";

[ObservableProperty] private double _autoFontOptimalFactor = 0.20;

public bool IsOverflowMode
{
    get => AutoFontSizeMode == "overflow";
    set { if (value) AutoFontSizeMode = "overflow"; }
}
public bool IsOptimalMode
{
    get => AutoFontSizeMode == "optimal";
    set { if (value) AutoFontSizeMode = "optimal"; }
}

public FontConfig ToConfig() => new()
{
    FontName             = FontName,
    FontSizePt           = FontSizePt,
    FontColor            = FontColor,
    AutoFontSize         = AutoFontSize,
    AutoFontSizeMode     = AutoFontSizeMode,
    AutoFontOptimalFactor = AutoFontOptimalFactor,
};
```

---

## テスト観点

| # | 確認内容 | 期待結果 |
|---|---|---|
| 1 | 自動調整 OFF でスライダーの値がそのまま適用される | 既存動作と変わらない |
| 2 | 自動調整 ON・overflow モード | タイルに収まる最大フォントサイズで表示される |
| 3 | 自動調整 ON・optimal モード・係数 0.20 | 短辺 96px のタイルで約 14pt 相当のサイズになる |
| 4 | optimal モードで長いテキストが overflow する場合 | 自動的にサイズを縮小して収める |
| 5 | タイルリサイズ後にフォントサイズが再計算される | サイズ変更に追従して再描画される |
| 6 | 係数スライダーを変更してダイアログ OK を押す | 係数が config に保存され、再起動後も維持される |
| 7 | system タイルの ContentFont で自動調整を設定する | MainText / SubText がサイズ自動調整される |
| 8 | モード選択ラジオボタンが自動調整 OFF 時にグレーアウトする | IsEnabled が正しく機能している |
