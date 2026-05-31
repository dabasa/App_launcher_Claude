# Ph.F-D 実装指示書 ─ AutoFont 自動調整（見切れ防止）

対象バージョン：V1.2.0  
作成日：2026-05-24  
実装完了：2026-05-30

> **設計変更（2026-05-30）**  
> 当初は「overflow（見切れ防止）」と「optimal（最適サイズ算出）」の2モードを実装する予定だったが、  
> ユーザーの判断により **optimal モードは廃止**し、チェックボックス1つの構成に統一した。  
> AutoFontSize=true は「はみ出す場合のみ縮小する」（overflow 相当）の動作のみとなる。

---

## 概要

`AutoFontSize = true` が設定されたタイルにおいて、  
指定 `FontSizePt` を上限としてテキストがタイル領域に収まる最大フォントサイズを計算し、適用する。  
実タイル・編集画面プレビュー・フォント詳細設定パネルのプレビュー全てに反映する。

---

## 実装内容

### 1. `FontConfig` への変更（不要なフィールドを追加しない）

当初の設計では `AutoFontSizeMode`・`AutoFontOptimalFactor` を追加する予定だったが、  
optimal モード廃止に伴い追加しない。  
`FontConfig` は既存の構成（`FontName` / `FontSizePt` / `FontColor` / `AutoFontSize`）のままとする。

---

### 2. `TileControl.xaml.cs` の更新（見切れ防止ロジック）

**実装ファイル**：`src/AppLauncher/Views/Controls/TileControl.xaml.cs`

`FormattedText` は WPF TextBlock の実描画より行高が小さく出るため、`TextBlock.Measure()` で測定する。  
`w`・`h` はパディング（8px×2=16px）を除いたコンテンツ領域。

```csharp
private double CalcOverflowFontSize(string text, double w, double h)
{
    if (string.IsNullOrEmpty(text) || w <= 0 || h <= 0) return 6;

    var measure = new TextBlock
    {
        Text         = text,
        TextWrapping = TextWrapping.Wrap,
        FontFamily   = TitleText.FontFamily,
        FontStyle    = TitleText.FontStyle,
        FontWeight   = TitleText.FontWeight,
        FontStretch  = TitleText.FontStretch,
        Padding      = new Thickness(8),
    };
    // w/h はパディング除外済み。+16 で TextBlock 全体サイズに戻す
    double startPt = Math.Clamp(_tile.FontSizePt, 6, 72);
    for (double size = startPt; size >= 6; size--)
    {
        measure.FontSize = size * 4.0 / 3.0;
        measure.Measure(new Size(w + 16, double.PositiveInfinity));
        if (measure.DesiredSize.Height <= h + 16) return size;
    }
    return 6;
}
```

画像レイアウトに応じたテキスト領域調整：

| ImagePosition | w の調整 | h の調整 |
|---|---|---|
| top / bottom | なし | `(ActualHeight - pad) * 0.35` |
| left / right | `ActualWidth / 2.0 - pad` | なし |
| center | なし | なし（全体をテキスト領域として使う） |

---

### 3. `SystemTileControl.xaml.cs` の更新

**実装ファイル**：`src/AppLauncher/Views/Controls/SystemTileControl.xaml.cs`

> **設計変更（2026-05-31）**  
> 当初はタイトルとコンテンツの高さ割合を固定比率（タイトル 35%・コンテンツ 65%）で分割していたが、  
> この方式では両者が相互参照しないため、タイトルが小さくてもコンテンツが 65% に制限される問題があった。  
> **改善後**：`UpdateTitleFontSize()` でタイトルの実測高さを `_estimatedTitleH` フィールドに保存し、  
> コンテンツ系メソッド（`UpdateContentFontSize` / `UpdateCircleLayout` / `UpdateTopProcFontSize`）が  
> `_estimatedTitleH` を参照して正確な残り高さを算出する設計に変更した。

#### フィールド追加

```csharp
// タイトルの推定高さ（UpdateTitleFontSize で計算 → コンテンツ系メソッドで参照）
private double _estimatedTitleH;
```

#### タイトルフォント自動調整（`UpdateTitleFontSize`）

`TitleFont.AutoFontSize = true` の場合に `TitleText.FontSize` を自動調整する。  
`ImagePosition` に応じてタイトル領域のサイズ予算を算出：

| ImagePosition | 幅予算 | 高さ予算 |
|---|---|---|
| top / bottom / center | `ActualWidth`（全幅） | `ActualHeight * 0.35` |
| left / right | `ActualWidth / 2.0` | `ActualHeight`（全高） |

**実測値の保存（`AutoFontSize` の値によらず常に実行）**  
上下配置のとき、決定したフォントサイズで `TextBlock.Measure()` を行い `_estimatedTitleH` へ保存する。  
これにより `AutoFontSize = false` のタイルでも正確な残り高さをコンテンツ系メソッドへ渡せる。

```csharp
if (!isHorizontal)
{
    measure.FontSize = titlePx;
    measure.Measure(new Size(measureW, double.PositiveInfinity));
    _estimatedTitleH = measure.DesiredSize.Height;
}
```

#### コンテンツフォント自動調整（`UpdateContentFontSize`）

`ContentFont.AutoFontSize = true` の場合にテキスト形式のコンテンツフォントを自動調整する。  
コンテンツ領域予算：

| 条件 | 幅予算 | 高さ予算 |
|---|---|---|
| left / right 配置 | `ActualWidth / 2.0` | `ActualHeight` |
| タイトルあり（top / bottom） | `ActualWidth` | `ActualHeight - _estimatedTitleH`（実測値） |
| タイトルなし | `ActualWidth` | `ActualHeight` |

- 固定比率 `0.65` を廃止し `_estimatedTitleH` を使用する（精度向上）
- 円グラフ形式（キャンバス固定レイアウト）は対象外
- テキスト更新のたびに `Render()` 末尾で再計算（テキスト内容が変わるため）

#### 円グラフレイアウト（`UpdateCircleLayout`）

`_estimatedTitleH` を使ってコンテンツ高さを算出する。  
`_estimatedTitleH = 0`（未計算）の場合は `TitleText.ActualHeight` でフォールバック。

```csharp
double titleH = hasTitle && pos is not "left" and not "right"
    ? (_estimatedTitleH > 0 ? _estimatedTitleH : TitleText.ActualHeight)
    : 0.0;
double contentH = tileH - titleH;
```

#### TOPプロセステーブルのフォントサイズ（`UpdateTopProcFontSize`）

`ContentGrid.ActualHeight` を直接参照することで、`_estimatedTitleH` の推定誤差を完全に排除する。  
フォントサイズは `TextBlock.Measure()` による実測（`1.2` 倍近似を廃止）。

```csharp
double availH = ContentGrid.ActualHeight;

// Margin T(4)+B(8)=12 + BorderThickness(1×2=2) + 行区切り線(3) + 底面余白(4)
availH -= 21;

// ヘッダー行は SemiBold → probe も SemiBold で計測（保守的な高さ）
var probe = new TextBlock
{
    Text       = "Ag",
    Padding    = new Thickness(3, 1, 3, 1),
    FontFamily = _topProcCells[0, 0].FontFamily,
    FontWeight = FontWeights.SemiBold,
};
for (double size = maxPt; size >= 6; size--)
{
    probe.FontSize = size * 4.0 / 3.0;
    probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
    if (probe.DesiredSize.Height * 4 <= availH) { /* 採用 */ break; }
}
```

**`_topProcessPanel` のレイアウト設定（`EnsureTopProcessPanel`）**

| プロパティ | 値 | 備考 |
|---|---|---|
| `BorderThickness` | `new Thickness(1)` | 外枠 1px |
| `Margin` | `new Thickness(6, 4, 6, 8)` | L:6 T:4 R:6 B:8（両端 6px・下 8px で余裕確保） |
| `HorizontalAlignment` | `Stretch` | ContentGrid 幅に合わせて拡張 |
| `VerticalAlignment` | `Center` | コンテンツエリア内で縦中央 |

`availH -= 21` の内訳：Margin T/B(4+8=12) + Border T/B(2) + 行区切り線(3) + 底面余白(4)

---

### 4. `FontDetailPanel` プレビューの更新

**実装ファイル**：`src/AppLauncher/ViewModels/FontDetailViewModel.cs`

`PreviewFontSize` プロパティを更新し、`AutoFontSize = true` 時は標準タイル（96×96px）を基準に  
収まるフォントサイズを計算して返すようにする。

```csharp
public double PreviewFontSize
{
    get
    {
        if (!AutoFontSize || string.IsNullOrEmpty(PreviewText))
            return FontSizePt * 4.0 / 3.0;

        // 標準タイル 96×96 で TextBlock.Measure
        const double tileSize = 96.0;
        const double pad = 16.0;
        double w = tileSize - pad;
        double h = tileSize - pad;
        var measure = new TextBlock { Text=PreviewText, TextWrapping=TextWrapping.Wrap,
            FontFamily=PreviewFontFamily, Padding=new Thickness(8) };
        double startPt = Math.Clamp(FontSizePt, 6, 72);
        for (double size = startPt; size >= 6; size--)
        {
            measure.FontSize = size * 4.0 / 3.0;
            measure.Measure(new Size(w + 16, double.PositiveInfinity));
            if (measure.DesiredSize.Height <= h + 16) return size * 4.0 / 3.0;
        }
        return 6 * 4.0 / 3.0;
    }
}
```

チェックボックス ON/OFF で即座にプレビューへ反映するため  
`[NotifyPropertyChangedFor(nameof(PreviewFontSize))]` を `AutoFontSize` に追加。

---

### 5. `TileEditControl` プレビューの更新

**実装ファイル**：`src/AppLauncher/Views/Controls/TileEditControl.xaml.cs`

#### 共通ヘルパー `ComputeEffectivePreviewFontSizePx`

実タイルと同一ロジックで有効プレビューフォントサイズ（WPF px）を計算する静的ヘルパー。  
`ColSpan` / `RowSpan` × `Layout.TileSize` で実ピクセルサイズを求め、`TextBlock.Measure()` で判定する。

```csharp
private static double ComputeEffectivePreviewFontSizePx(TileEditViewModel vm)
{
    if (!vm.AutoFontSize) return vm.PreviewFontSize;
    // 実タイルサイズで TextBlock.Measure（TileControl と同一ロジック）
    var layout = App.ConfigService.Current.Layout;
    double tileW = vm.ColSpan * layout.TileSize + (vm.ColSpan - 1) * layout.TileMargin;
    double tileH = vm.RowSpan * layout.TileSize + (vm.RowSpan - 1) * layout.TileMargin;
    // ... 計算 ...
}
```

#### 適用先の分岐

| タイル種別 | 適用先 |
|---|---|
| 非 system タイル | `PreviewText.FontSize` |
| system タイル（タイトル） | `SysTitleText.FontSize`（`ArrangeSystemPreviewPanels` 内で適用） |
| system タイル（コンテンツ） | `SysMainText.FontSize` / `SysSubText.FontSize`（後述 `FitPreviewSystemTextFont`） |

呼び出しタイミング：
- 編集ダイアログを開いたとき（`SubscribeToTileVm`）
- フォント詳細ダイアログの OK 押下後（`OnTitleFontDetailClick` コールバック）

#### system タイルコンテンツのプレビュー自動調整（`FitPreviewSystemTextFont`）

> **追加（2026-05-31）**  
> 当初実装では `SysMainText` / `SysSubText` のフォントサイズは XAML 固定値（13 / 10）のままだった。  
> 実タイルは `UpdateContentFontSize()` で自動調整されるのにプレビューが追従しないため、  
> `FitPreviewSystemTextFont()` を追加してプレビューにも自動調整を反映させた。

プレビュー Border は `Width="200" Height="96"` 固定。  
タイトル有の場合は `SysTitleText.FontSize` + `SysTitleText.Padding` で実測し、残り高さを算出する。

```csharp
private void FitPreviewSystemTextFont()
{
    // タイトル高さを実測
    if (hasTitle && pos is not "left" and not "right")
    {
        var titleMeasure = new TextBlock { Text=SysTitleText.Text, FontSize=SysTitleText.FontSize,
            FontFamily=SysTitleText.FontFamily, TextWrapping=TextWrapping.Wrap,
            Padding=SysTitleText.Padding };
        titleMeasure.Measure(new Size(previewW, double.PositiveInfinity));
        availH = previewH - titleMeasure.DesiredSize.Height;
    }

    // ContentFont の AutoFontSize に従って SysMainText / SysSubText を調整
    var cf = vm.GetContentFontCopy();
    if (!cf.AutoFontSize) { /* 固定値を適用 */ return; }
    for (double size = startPt; size >= 6; size--)
    {
        // MainText + SubText の合計高さが availH 以下に収まるサイズを選択
    }
}
```

**呼び出しタイミング**：

| イベント | 呼び出し元 |
|---|---|
| データ取得成功後（テキストモード） | `RenderSystemPreview()` の else ブロック末尾 |
| タイトル・レイアウト変更時 | `UpdateSystemPreviewArrangement()` の末尾（テキストパネル表示中のみ） |
| データ取得失敗時 | `RefreshSystemPreviewAsync()` の catch ブロック末尾 |

---

## テスト結果

| # | 確認内容 | 結果 |
|---|---|---|
| 1 | 自動調整 OFF：スライダーの値がそのまま適用される | ✅ |
| 2 | 自動調整 ON：タイルに収まる最大フォントサイズで表示される | ✅ |
| 3 | タイルリサイズ後にフォントサイズが再計算される | ✅ |
| 4 | system タイルのタイトルフォントが自動調整される | ✅ |
| 5 | system タイルのコンテンツフォントが自動調整される | ✅ |
| 6 | FontDetailPanel のプレビューが自動調整結果を反映する | ✅ |
| 7 | TileEditControl のプレビューが自動調整結果を反映する（通常タイル） | ✅ |
| 8 | TileEditControl のプレビューが自動調整結果を反映する（system タイル） | ✅ |
| 9 | タイトルが小さい場合、コンテンツが 35% に制限されず余白を有効活用する | ✅ |
| 10 | TOP プロセステーブルの下端に十分な余白がある（最低 8px） | ✅ |
| 11 | TileEditControl の system タイルプレビューがコンテンツフォント自動調整を反映する | ✅ |
