# Ph.2 実装指示書 ─ フレームレスウィンドウ

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.2 フレームレスウィンドウ |
| 実装目的 | ランチャー本体の外観（フレーム・ハンドル）を表示し、画面右端吸着・縦スライドドラッグを実現する |
| 未確定事項 | なし |
| 既存影響 | `App.xaml.cs` の起動フローを変更する。`MainWindow` は以後未使用 |
| 追加ライブラリ | なし（Ph.1 の依存関係で対応可能） |
| 実装範囲 | `Views/` および `Services/SnapService.cs`。Ph.3 で追加するタイル・ボトムバーの実装は含まない |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `Views/LauncherWindow.xaml` | フレームレス透過ウィンドウ本体。ハンドル・フレームのレイアウトを定義 |
| `Views/LauncherWindow.xaml.cs` | ページ配色（背景色・透過率）をフレームとハンドルに適用 |
| `Views/Controls/HandleControl.xaml` | ハンドル UserControl（15 × 100 px・角丸 10 px） |
| `Views/Controls/HandleControl.xaml.cs` | ハンドルの色・透過率を動的に設定する `SetAppearance` メソッド |
| `Services/SnapService.cs` | 吸着位置の算出・ウィンドウ配置・縦スライドドラッグ処理（DPI 対応） |
| `App.xaml.cs` | `LauncherWindow` と `SnapService` を使う起動フローに変更 |
| `Models/ColorPalette.cs` | `OpacityToDouble` の変換ロジック修正（透過率→WPF 不透明度） |

---

## 実装仕様

### ウィンドウ構成（右端吸着時）

```
Window（透明・フレームレス・Topmost）
└── Grid（3列）
    ├── 列0: HandleControl  15 px（ハンドル）
    ├── 列1: 透明ギャップ    8 px（handleFrameMargin）
    └── 列2: Border         *（ランチャーフレーム）
```

- `WindowStyle="None"` / `AllowsTransparency="True"` / `Background="Transparent"`
- `ShowInTaskbar="False"` / `Topmost="True"` / `ResizeMode="NoResize"`

### ランチャーフレーム（Border）

| 項目 | 値 |
|---|---|
| 角丸半径 | `layout.FrameCornerRadius`（デフォルト 20 px） |
| 外枠 | 黒（`#FF000000`）・2 px 実線 |
| 背景色 | ページ背景色（ColorPalette）＋透過率をアルファ値に変換して `SolidColorBrush` を生成 |

### ハンドル（HandleControl）

| 項目 | 値 |
|---|---|
| サイズ | 15 × 100 px（右端吸着時は縦長） |
| 角丸半径 | 10 px |
| 配置 | フレーム左側・`VerticalAlignment="Center"` |
| 色・透過率 | ページ設定の `handleColor` / `handleOpacity` から生成 |

### 透過率変換（ColorPalette.OpacityToDouble）

```
透過率 30 % → WPF Opacity 0.70（= 1 - 0.30）
```

- 変換式：`1.0 - transparencyPercent / 100.0`
- 旧実装（`transparencyPercent / 100.0`）は誤りであるため修正する

### 吸着位置算出（SnapService.ApplySnap）

右端吸着時のウィンドウ配置：

```
ウィンドウ幅   = handleShortSide(15) + handleFrameMargin(8) + frameWidth
ウィンドウ左端 = 画面幅 - screenEdgeDistance(20) - frameWidth - handleFrameMargin - handleShortSide
ウィンドウ上端 = (画面高さ - ウィンドウ高さ) / 2   ← 垂直中央
```

- `frameWidth` / `frameHeight` は `WindowSizeCalculator.Calculate()` で算出
- `SystemParameters.PrimaryScreenWidth/Height` を使用（論理ピクセル単位）
- `ApplySnap()` は `window.Show()` 後に呼ぶ（DPI 情報の取得に `PresentationSource` が必要なため）

### ドラッグ（縦スライド）

- 右端吸着時は **縦方向のみ**移動可能（左右は固定）
- `MouseLeftButtonDown` でドラッグ開始位置を記録し `Mouse.Capture` で捕捉
- `MouseMove` で Y 方向の移動量を計算してウィンドウ `Top` を更新
- 物理ピクセル（`PointToScreen` 戻り値）を `DPI スケール` で除算して論理ピクセルに変換
- 移動範囲は `0 ～ 画面高さ - ウィンドウ高さ` にクランプ

```csharp
double dpiScaleY = PresentationSource.FromVisual(window)
                       ?.CompositionTarget.TransformToDevice.M22 ?? 1.0;
double newTop = dragStartTop + (currentPhysicalY - startPhysicalY) / dpiScaleY;
```

---

## 完了条件

- [ ] アプリが画面右端に吸着した状態で起動する（フレーム右端〜画面右端 = 20 px）
- [ ] ハンドルがフレーム左側の上下中央に表示される
- [ ] ページ背景色（gray）・透過率（30 %）がフレームに正しく反映される（70 % 不透明）
- [ ] ハンドルにページのハンドル色・透過率（20 %）が反映される（80 % 不透明）
- [ ] ドラッグで縦方向にのみ移動できる（横方向は移動しない）
- [ ] 他のウィンドウの最前面に常に表示される
- [ ] `dotnet build` がエラーなく成功する

---

## 参照定義書

- `definition/ui/ランチャー本体.md § 2` — ハンドル仕様
- `definition/ui/ランチャー本体.md § 3` — ランチャーフレーム仕様
- `definition/ui/配置位置数値定義.md § 5` — フレーム数値（角丸・枠幅）
- `definition/ui/配置位置数値定義.md § 6` — ハンドル数値（サイズ・角丸・マージン・画面端距離）
- `definition/ui/配置位置数値定義.md § 8` — ウィンドウサイズ算出式
- `definition/function/アプリケーション機能定義.md § 3.1` — 画面吸着仕様
- `definition/data/設定ファイルフォーマット.md § 3.2` — ページ設定（backgroundColor / handleColor 等）
- `mockup/ランチャー想定図.html` — SVG 座標でフレーム・ハンドル・20 px gap の配置を確認
