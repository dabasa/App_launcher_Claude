# Ph.F-A 実装指示書 ─ FontConfig 基盤整備

対象バージョン：V1.2.0  
作成日：2026-05-24

---

## 概要

現在 `TileConfig` に散在しているフォント関連フィールド（`fontName` / `fontSizePt` / `fontColor` / `autoFontSize`）を  
`FontConfig` オブジェクトに集約し、タイトルフォントとコンテンツフォントを独立管理できる基盤を整える。

- **TitleFont**：全タイル種別共通。タイル上のタイトルテキストに適用。
- **ContentFont**：system タイル専用。システム情報テキスト（MainText / SubText / 円グラフ等）に適用。

既存の `config.json` は旧フォーマット（フラットフィールド）で保存されているため、  
読み込み時に自動マイグレーションして新フォーマットに変換する。

---

## 実装内容

### 1. `FontConfig` クラスの新規作成

**対象ファイル**

- `src/AppLauncher/Models/Config/FontConfig.cs`（新規）

```csharp
namespace AppLauncher.Models.Config;

public class FontConfig
{
    public string FontName         { get; set; } = "";
    public int    FontSizePt       { get; set; } = 16;
    public string FontColor        { get; set; } = "white";
    public bool   AutoFontSize     { get; set; } = false;
    // Ph.F-D で追加予定のフィールドのプレースホルダー（F-D 実装時に追加）
    // public string AutoFontSizeMode  { get; set; } = "overflow";
    // public double AutoFontOptimalFactor { get; set; } = 0.20;
}
```

---

### 2. `TileConfig` の更新

**対象ファイル**

- `src/AppLauncher/Models/Config/TileConfig.cs`

**変更内容**

1. `TitleFont` と `ContentFont` プロパティを追加する（null = 未設定、旧フォーマットからマイグレーション前）。
2. 旧フラットフィールドは JSON 読み込み互換のため残す。新規保存時は `titleFont` / `contentFont` に書き込まれるため、次回以降は旧フィールドは読み込まれない。

```csharp
using System.Text.Json.Serialization;

namespace AppLauncher.Models.Config;

public class TileConfig
{
    public int    Col     { get; set; }
    public int    Row     { get; set; }
    public int    ColSpan { get; set; } = 1;
    public int    RowSpan { get; set; } = 1;
    public string Type    { get; set; } = "app";
    public string Title   { get; set; } = "";
    public string Path    { get; set; } = "";
    public string Args    { get; set; } = "";
    public string WorkDir { get; set; } = "";
    public string Color   { get; set; } = "blue";
    public int    Opacity { get; set; } = 0;
    public string ImagePath       { get; set; } = "";
    public string ImagePosition   { get; set; } = "top";
    public bool   ImageTransparent { get; set; } = false;

    // ─── 新フォーマット（Ph.F-A 以降） ───────────────────────────────────
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FontConfig? TitleFont   { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FontConfig? ContentFont { get; set; }

    // ─── 旧フォーマット（マイグレーション用・読み込みのみ） ─────────────
    // TitleFont != null の場合はこれらは無視される
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string FontName    { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int    FontSizePt  { get; set; } = 0;   // 0 = 未設定（旧フォーマット不在）
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string FontColor   { get; set; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool   AutoFontSize { get; set; } = false;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SystemInfoConfig? SystemInfo { get; set; }

    // ─── マイグレーション ─────────────────────────────────────────────
    /// <summary>旧フォーマットの場合に TitleFont を生成する。既に設定済みなら何もしない。</summary>
    internal void MigrateFont()
    {
        if (TitleFont != null) return;
        TitleFont = new FontConfig
        {
            FontName     = FontName,
            FontSizePt   = FontSizePt > 0 ? FontSizePt : 16,
            FontColor    = string.IsNullOrEmpty(FontColor) ? "white" : FontColor,
            AutoFontSize = AutoFontSize,
        };
    }
}
```

> **注意**：`FontSizePt` フィールドのデフォルト値を `0` にすることで、  
> `WhenWritingDefault` による書き込みスキップが機能する。  
> 旧 config には `"fontSizePt": 16` が保存されているため、読み込み時は正しく `16` が入る。

---

### 3. `ConfigService` の更新（マイグレーション呼び出し）

**対象ファイル**

- `src/AppLauncher/Services/ConfigService.cs`

**変更内容**

`Load()` でデシリアライズ直後に `MigrateFont()` を呼び出す。

```csharp
// Load() 内、config 生成後
foreach (var page in config.Pages)
    foreach (var tile in page.Tiles)
        tile.MigrateFont();
```

---

### 4. `TileViewModel` の更新

**対象ファイル**

- `src/AppLauncher/ViewModels/TileViewModel.cs`

**変更内容**

`TitleFont` と `ContentFont` を公開する。  
既存のフラットプロパティ（`FontName`, `FontSizePt`, `FontColor`, `AutoFontSize`）は  
`TitleFont` から派生する計算プロパティに変更し、既存の `TileControl` / `SystemTileControl` の  
コードを変えずに動作させる。

```csharp
public FontConfig  TitleFont   { get; }
public FontConfig? ContentFont { get; }

// TileControl / SystemTileControl との後方互換プロパティ
public string FontName     => TitleFont.FontName;
public int    FontSizePt   => TitleFont.FontSizePt;
public string FontColor    => TitleFont.FontColor;
public bool   AutoFontSize => TitleFont.AutoFontSize;
```

コンストラクタ:

```csharp
TitleFont   = config.TitleFont   ?? new FontConfig();
ContentFont = config.ContentFont;
```

`ToConfig()` も更新する:

```csharp
public TileConfig ToConfig() => new()
{
    ...
    TitleFont   = TitleFont,
    ContentFont = ContentFont,
    SystemInfo  = SystemInfo,
};
```

---

### 5. `TileEditViewModel` の更新

**対象ファイル**

- `src/AppLauncher/ViewModels/TileEditViewModel.cs`

**変更内容**

内部状態として `FontConfig` の作業コピーを保持し、既存のフラットプロパティをそこから読み書きする。  
これにより既存のバインディング（スライダー・色選択等）はそのまま動作する。

```csharp
// ─── フォント設定（作業コピー） ────────────────────────────────────────
private FontConfig _titleFontWork;
private FontConfig _contentFontWork;

// TitleFont の各プロパティを既存バインディング名で公開（FontSizePt 変更時は PreviewFontSize も通知）
public string FontName
{
    get => _titleFontWork.FontName;
    set { _titleFontWork.FontName = value; OnPropertyChanged(); }
}
public int FontSizePt
{
    get => _titleFontWork.FontSizePt;
    set { _titleFontWork.FontSizePt = value; OnPropertyChanged(); OnPropertyChanged(nameof(PreviewFontSize)); }
}
public string FontColor
{
    get => _titleFontWork.FontColor;
    set { _titleFontWork.FontColor = value; OnPropertyChanged(); OnPropertyChanged(nameof(PreviewForeground)); }
}
public bool AutoFontSize
{
    get => _titleFontWork.AutoFontSize;
    set { _titleFontWork.AutoFontSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FontSizeSliderEnabled)); }
}

// ContentFont 用（system タイル専用）
public string ContentFontName
{
    get => _contentFontWork.FontName;
    set { _contentFontWork.FontName = value; OnPropertyChanged(); }
}
// ...以下同様
```

コンストラクタ:

```csharp
_titleFontWork   = source.TitleFont   != null ? Clone(source.TitleFont)   : new FontConfig { FontSizePt = 16, FontColor = "white" };
_contentFontWork = source.ContentFont != null ? Clone(source.ContentFont) : new FontConfig { FontSizePt = 12, FontColor = "white" };

// Clone ヘルパー
private static FontConfig Clone(FontConfig src) => new()
{
    FontName = src.FontName, FontSizePt = src.FontSizePt,
    FontColor = src.FontColor, AutoFontSize = src.AutoFontSize,
};
```

`ToConfig()`:

```csharp
TitleFont   = Clone(_titleFontWork),
ContentFont = Type == "system" ? Clone(_contentFontWork) : null,
```

---

### 6. `SystemTileControl` への ContentFont 適用

**対象ファイル**

- `src/AppLauncher/Views/Controls/SystemTileControl.xaml.cs`

**変更内容**

`Apply()` で `ContentFont` が存在する場合はそのフォントをシステム情報テキストに適用する。

```csharp
// ContentFont が設定されている場合（system タイル）
var cf = tile.ContentFont;
if (cf != null)
{
    double cfPx = cf.FontSizePt * 4.0 / 3.0;
    var cfBrush  = new SolidColorBrush(ColorPalette.GetColor(cf.FontColor));
    if (!string.IsNullOrEmpty(cf.FontName))
    {
        var cfFamily = new FontFamily(cf.FontName);
        MainText.FontFamily        = cfFamily;
        SubText.FontFamily         = cfFamily;
        CircleCenterText.FontFamily = cfFamily;
        CircleLabel.FontFamily      = cfFamily;
    }
    MainText.FontSize         = cfPx;
    SubText.FontSize          = Math.Max(10, cfPx - 4);
    CircleCenterText.FontSize = cfPx;
    CircleLabel.FontSize      = Math.Max(8, cfPx - 4);
    MainText.Foreground         = cfBrush;
    SubText.Foreground          = cfBrush;
    CircleCenterText.Foreground = cfBrush;
    CircleLabel.Foreground      = cfBrush;
}
```

> 既存の `Apply()` 内の `MainText.FontSize` / `SubText.FontSize` 設定をこのブロックで上書きする形にする。

---

## テスト観点

| # | 確認内容 | 期待結果 |
|---|---|---|
| 1 | 旧フォーマット config.json でアプリを起動する | エラーなく起動し、フォント設定が引き継がれる |
| 2 | タイルを保存し config.json を開く | `titleFont` オブジェクトが書き込まれ、旧フラットフィールドが消える |
| 3 | system タイルを保存する | `contentFont` オブジェクトが書き込まれる |
| 4 | 非 system タイルを保存する | `contentFont` は存在しない（WhenWritingNull で省略）|
| 5 | system タイルで ContentFont を変えてプレビューを確認する | MainText / SubText / 円グラフの色・サイズが変わる |
| 6 | 再起動後にフォント設定が復元される | TitleFont / ContentFont の値が正しく読み込まれる |
