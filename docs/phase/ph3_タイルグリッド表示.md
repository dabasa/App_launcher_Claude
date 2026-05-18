# Ph.3 実装指示書 ─ タイルグリッド表示

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.3 タイルグリッド表示 |
| 実装目的 | config.json のページ・タイルデータを読み込み、ランチャーフレーム内にタイルグリッド・ページ名・ボトムバーを静的表示する |
| 未確定事項 | なし |
| 既存影響 | `LauncherWindow.xaml` のフレーム内部（現在空）にコンテンツを追加する。`App.xaml.cs` で ViewModel を生成してウィンドウに渡す |
| 追加ライブラリ | なし（`CommunityToolkit.Mvvm` は Ph.1 で追加済み） |
| 実装範囲 | ViewModels / Views/Controls 内のタイルグリッド表示のみ。タイル起動・ホバーアニメーションは Ph.4 |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `ViewModels/LauncherViewModel.cs` | 現在ページインデックス・モード状態・ページ一覧を管理するルート ViewModel |
| `ViewModels/PageViewModel.cs` | 1 ページ分の設定（背景色・タイル一覧等）を保持する ViewModel |
| `ViewModels/TileViewModel.cs` | 1 タイル分の設定（位置・サイズ・色・テキスト等）を保持する ViewModel |
| `Views/Controls/PageNameControl.xaml` | ページ名テキスト（中央揃え・省略表示・フォントサイズ可変） |
| `Views/Controls/PageNameControl.xaml.cs` | コードビハインド（必要最小限） |
| `Views/Controls/TileControl.xaml` | 1 タイルの表示（角丸・背景色・透過率・テキスト・複数サイズ対応） |
| `Views/Controls/TileControl.xaml.cs` | コードビハインド（必要最小限） |
| `Views/Controls/TileGridControl.xaml` | タイル配置グリッド・空スロット表示 |
| `Views/Controls/TileGridControl.xaml.cs` | グリッド行列定義の動的生成・タイル配置処理 |
| `Views/Controls/BottomBarControl.xaml` | ページインジケーター＋モード切替ボタン（通常モードのみ） |
| `Views/Controls/BottomBarControl.xaml.cs` | インジケーター生成・ページ切り替え処理 |
| `Views/LauncherWindow.xaml` | Frame Border の内部に PageName / TileGrid / BottomBar を組み込む |
| `Views/LauncherWindow.xaml.cs` | DataContext に LauncherViewModel をセット |
| `App.xaml.cs` | LauncherViewModel を生成してウィンドウに渡す |

---

## 実装仕様

### 1. ViewModel 構成

#### LauncherViewModel（ObservableObject）

```
LauncherViewModel
├── ObservableProperty: int CurrentPageIndex
├── ObservableProperty: AppMode Mode  // Normal / Edit（Ph.6 まで Normal 固定）
├── List<PageViewModel> Pages
└── PageViewModel CurrentPage  // Pages[CurrentPageIndex]
```

- `CommunityToolkit.Mvvm` の `[ObservableProperty]` / `[RelayCommand]` を使用する
- ページ切り替えは `CurrentPageIndex` を変更し `CurrentPage` を更新することで行う

#### PageViewModel（ObservableObject）

```
PageViewModel
├── string Name
├── string BackgroundColor
├── int BackgroundOpacity
├── string HandleColor
├── int HandleOpacity
├── string PinFrameColor
├── int PinFrameOpacity
└── List<TileViewModel> Tiles
```

`PageConfig` から変換して生成する。

#### TileViewModel（ObservableObject）

```
TileViewModel
├── int Col, Row, ColSpan, RowSpan
├── string Type, Title, Path, Args, WorkDir
├── string Color
├── int Opacity
├── string FontName
├── int FontSizePt
├── string FontColor
├── string ImagePath, ImagePosition
├── bool ImageTransparent
└── SystemInfoConfig? SystemInfo
```

`TileConfig` から変換して生成する。

---

### 2. LauncherWindow フレーム内部レイアウト

`Frame`（Border）の内側に以下の Grid を配置する。

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="8"/>      <!-- pageNameTopMargin -->
        <RowDefinition Height="Auto"/>   <!-- PageNameControl（フォントサイズ依存） -->
        <RowDefinition Height="8"/>      <!-- pageNameBottomMargin -->
        <RowDefinition Height="*"/>      <!-- TileGridControl -->
        <RowDefinition Height="8"/>      <!-- bottomBarTopMargin -->
        <RowDefinition Height="32"/>     <!-- BottomBarControl（通常モード固定高さ） -->
        <RowDefinition Height="8"/>      <!-- bottomBarBottomMargin -->
    </Grid.RowDefinitions>

    <!-- 左右マージン 20 px はタイル配置エリアと PageName に共通 -->
    <controls:PageNameControl Grid.Row="1" Margin="20,0"/>
    <controls:TileGridControl  Grid.Row="3" Margin="20,0"/>
    <controls:BottomBarControl Grid.Row="5"/>
</Grid>
```

> `RowDefinition Height="8"` は `layout.PageNameTopMargin` 等の設定値で置き換えてもよい。  
> Ph.3 では固定値で実装し、全体設定反映は後フェーズで対応する。

---

### 3. PageNameControl

| 項目 | 仕様 |
|---|---|
| テキスト | バインディング先：`LauncherViewModel.CurrentPage.Name` |
| 配置 | `HorizontalAlignment="Center"`・`VerticalAlignment="Center"` |
| フォントサイズ | `LauncherViewModel` 経由で `global.PageNameFontSizePt` を pt → WPF px 変換して適用（`pt × 4/3`） |
| 省略表示 | `TextTrimming="CharacterEllipsis"` |
| フォント色 | 白（`#F0F0F0`）固定（Ph.8 で設定化） |

---

### 4. TileGridControl

#### グリッド列・行定義（スペーサー列方式）

タイル間マージン（`tileMargin`）を WPF Grid で再現するため、**タイル列とスペーサー列を交互に定義**する。

| 定義数 | 式 |
|---|---|
| 列定義数 | `2 × cols - 1` |
| 行定義数 | `2 × rows - 1` |
| タイル列（偶数インデックス 0, 2, 4, …） | `Width = tileSize`（96 px） |
| スペーサー列（奇数インデックス 1, 3, 5, …） | `Width = tileMargin`（8 px） |

タイルの配置変換：

```
Grid.Column  = tile.Col  × 2
Grid.Row     = tile.Row  × 2
Grid.ColumnSpan = tile.ColSpan  × 2 - 1
Grid.RowSpan    = tile.RowSpan  × 2 - 1
```

例：ColSpan=2 → ColumnSpan = 2×2-1 = 3（タイル列 + スペーサー列 + タイル列を跨ぐ）

#### 空スロット表示

タイルが配置されていない 1×1 セル（グリッド座標）には点線枠を描画する。

- 占有チェック：各 `(col, row)` について `Tiles` のいずれかの `ColSpan` / `RowSpan` 範囲に含まれているかを確認する
- 占有されていないセルに `Rectangle`（背景なし・点線ストローク）を配置する

```
色:      PageBackgroundColor（ColorPalette から取得）
透過率:  0.4（固定）
StrokeDashArray: 6, 4
StrokeThickness: 1.5
```

#### 処理タイミング

- `TileGridControl` は `PageViewModel`（またはそのプロパティ）を受け取り、  
  `Loaded` イベントまたは `DataContext` 変更時にグリッド定義と子要素を動的生成する。

---

### 5. TileControl

#### サイズ

タイルの表示サイズは `TileViewModel.ColSpan` / `RowSpan` から以下の式で算出する。

```
幅 = tileSize × ColSpan + tileMargin × (ColSpan - 1)
高さ = tileSize × RowSpan + tileMargin × (RowSpan - 1)
```

ただし、TileGridControl のスペーサー列方式を採用する場合、`TileControl` は `Width`・`Height` を明示指定しなくても Grid の ColumnSpan によって自動的に正しいサイズになる。

#### 外観

| 項目 | 仕様 |
|---|---|
| 背景色 | `ColorPalette.GetColor(tile.Color)` にアルファ値（透過率変換）を適用した `SolidColorBrush` |
| 角丸 | `layout.TileCornerRadius`（デフォルト 20 px） |
| タイトル | `tile.Title`・`HorizontalAlignment="Center"`・`VerticalAlignment="Center"`（Ph.4 で画像表示位置対応） |
| フォント | `tile.FontName`（空文字はシステムデフォルト）・`tile.FontSizePt × 4/3` px・`ColorPalette.GetColor(tile.FontColor)` |

Ph.3 では画像・アイコン表示は実装しない。テキストはタイル中央に表示する。

---

### 6. BottomBarControl（通常モード）

#### レイアウト

```
BottomBarControl（高さ 32 px）
└── Grid
    ├── PageIndicatorPanel（HorizontalAlignment="Center"）  ← ページインジケーター
    └── ModeButton（HorizontalAlignment="Right"）           ← モード切替ボタン
```

#### ページインジケーター

モックアップ（`mockup/ランチャー想定図.html`）の SVG 座標より：

| 項目 | 値 |
|---|---|
| 外径（直径） | 18 px（半径 9 px） |
| 中心間隔 | 26 px |
| 現在ページ | 塗りつぶし円 ＋ 中心に白ドット（半径 3.5 px） |
| 他ページ | 外枠のみ（`StrokeThickness="2"`・塗りなし） |
| 色 | ページ背景色（`BackgroundColor`） |

- インジケーター数 = `Pages.Count`（最大 10）
- クリックで該当ページに即座に切り替え（アニメーションなし）
- `StackPanel`（Horizontal）または `UniformGrid` で並べ、中心揃えにする

#### モード切替ボタン

| 項目 | 値 |
|---|---|
| サイズ | 32 × 32 px |
| 角丸 | `layout.SettingsButtonCornerRadius`（デフォルト 10 px） |
| 色 | ページ背景色（透過率 0 %・完全不透明） |
| 右マージン | `layout.SettingsButtonRightMargin`（デフォルト 20 px）フレーム右端から |
| Ph.3 の動作 | クリックで何もしない（モード切替は Ph.6 で実装） |

---

### 7. ページ切り替え

- `BottomBarControl` の各インジケーターボタンクリック → `LauncherViewModel.CurrentPageIndex` を更新
- `CurrentPage` が変わると：
  - フレーム背景色・透過率が新ページの設定に更新される
  - ページ名テキストが更新される
  - タイルグリッドが新ページのタイル一覧で再描画される
  - ボトムバーの現在ページ強調表示が更新される
  - ハンドル色・透過率が更新される
- 切り替えはアニメーションなしで即座に反映する

---

## 完了条件

- [ ] config.json のタイルが正しい位置（Col / Row）・サイズ（ColSpan / RowSpan）・背景色・テキストで表示される
- [ ] 1×1 / 2×1 / 1×2 / 2×2 などの複数サイズのタイルが正しい大きさで表示される
- [ ] タイルが配置されていないグリッドセルに点線枠が表示される
- [ ] ページ名が中央揃えで表示され、長い場合は末尾が省略記号（…）になる
- [ ] ページインジケーターが正しい数だけ表示される
- [ ] ページインジケータークリックでページが即座に切り替わる
- [ ] 切り替え後にフレーム背景色・ページ名・タイル一覧・ハンドル色が新ページの設定に更新される
- [ ] `dotnet build` がエラーなく成功する

---

## 参照定義書

- `definition/ui/ランチャー本体.md § 4` — ページ名称エリア仕様
- `definition/ui/ランチャー本体.md § 5` — タイル配置エリア仕様
- `definition/ui/ランチャー本体.md § 6` — タイル仕様
- `definition/ui/ランチャー本体.md § 7` — ボトムバー通常モード仕様
- `definition/ui/配置位置数値定義.md § 1` — タイルサイズ・マージン・角丸
- `definition/ui/配置位置数値定義.md § 2` — タイル配置エリアのマージン
- `definition/ui/配置位置数値定義.md § 3` — ページ名称エリアの高さ算出式
- `definition/ui/配置位置数値定義.md § 4` — ボトムバー数値（インジケーター・ボタン）
- `definition/function/アプリケーション機能定義.md § 3.2` — タイル表示仕様
- `definition/function/アプリケーション機能定義.md § 3.4` — タイル種別
- `definition/data/設定ファイルフォーマット.md § 3.2` — ページ設定 JSON
- `definition/data/設定ファイルフォーマット.md § 3.3` — タイル設定 JSON
- `mockup/ランチャー想定図.html` — インジケーターサイズ・間隔・ボトムバー配置の SVG 座標
