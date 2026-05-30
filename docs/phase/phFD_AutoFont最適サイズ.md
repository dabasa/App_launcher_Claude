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

#### タイトルフォント自動調整（`UpdateTitleFontSize`）

`TitleFont.AutoFontSize = true` の場合に `TitleText.FontSize` を自動調整する。  
`ImagePosition` に応じてタイトル領域のサイズ予算を算出：

| ImagePosition | 幅予算 | 高さ予算 |
|---|---|---|
| top / bottom / center | `ActualWidth`（全幅） | `ActualHeight * 0.35` |
| left / right | `ActualWidth / 2.0` | `ActualHeight`（全高） |

#### コンテンツフォント自動調整（`UpdateContentFontSize`）

`ContentFont.AutoFontSize = true` の場合にテキスト形式のコンテンツフォントを自動調整する。  
コンテンツ領域予算：

| 条件 | 幅予算 | 高さ予算 |
|---|---|---|
| left / right 配置 | `ActualWidth / 2.0` | `ActualHeight` |
| タイトルあり（top / bottom） | `ActualWidth` | `ActualHeight * 0.65` |
| タイトルなし | `ActualWidth` | `ActualHeight` |

- 円グラフ形式（キャンバス固定レイアウト）は対象外
- テキスト更新のたびに `Render()` 末尾で再計算（テキスト内容が変わるため）

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
| system タイル | `SysTitleText.FontSize`（`ArrangeSystemPreviewPanels` 内で適用） |

呼び出しタイミング：
- 編集ダイアログを開いたとき（`SubscribeToTileVm`）
- フォント詳細ダイアログの OK 押下後（`OnTitleFontDetailClick` コールバック）

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
