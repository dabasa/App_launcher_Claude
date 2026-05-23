# Ph.1 実装指示書 ─ プロジェクト基盤

## 実装前確認

| 項目 | 内容 |
|---|---|
| 対象フェーズ | Ph.1 プロジェクト基盤 |
| 実装目的 | config.json の読み書き・カラーパレット・ウィンドウサイズ算出の基盤を構築する |
| 未確定事項 | なし |
| 既存影響 | なし（新規プロジェクト） |
| 追加ライブラリ | CommunityToolkit.Mvvm / Hardcodet.NotifyIcon.Wpf |
| 実装範囲 | Models / Services / Helpers のみ。UI（Views / ViewModels）は次フェーズ |

---

## 対象ファイル一覧

| ファイル | 役割 |
|---|---|
| `AppLauncher.sln` | ソリューション |
| `AppLauncher/AppLauncher.csproj` | プロジェクト定義 |
| `App.xaml / App.xaml.cs` | アプリ起動エントリーポイント |
| `MainWindow.xaml / MainWindow.xaml.cs` | 空シェル（Ph.2 で置き換え） |
| `Models/Config/AppConfig.cs` | config.json トップレベル |
| `Models/Config/GlobalConfig.cs` | global セクション |
| `Models/Config/LayoutConfig.cs` | layout セクション |
| `Models/Config/PageConfig.cs` | pages[] 各要素 |
| `Models/Config/TileConfig.cs` | tiles[] 各要素 |
| `Models/Config/SystemInfoConfig.cs` | systemInfo オブジェクト |
| `Models/ColorPalette.cs` | カラー名 → Color 変換（11色） |
| `Services/ConfigService.cs` | config.json 読み書き・初期値生成 |
| `Helpers/WindowSizeCalculator.cs` | ウィンドウサイズ算出式 |

---

## 完了条件

- [ ] `dotnet build` がエラーなく成功する
- [ ] アプリ起動時に `config/config.json` が存在しない場合、初期値で自動生成される
- [ ] 既存の config.json が正しく読み込まれ、変更後に保存できる
- [ ] カラー名（"blue" 等）から `Color` オブジェクトに変換できる
- [ ] `WindowSizeCalculator` が縦 4 × 横 6 の設定で幅 656 px・高さ 494 px を返す
- [ ] `.gitignore` を作成する

---

## 参照定義書

- `definition/data/設定ファイルフォーマット.md` — JSON キー名・型・仕様値
- `definition/function/アプリケーション機能定義.md § 3.6` — カラーパレット 11 色
- `definition/function/アプリケーション機能定義.md § 3.8` — Debug 初期タイル 13 件
- `definition/ui/配置位置数値定義.md § 8` — ウィンドウサイズ算出式・変数定義
- `definition/implementation/技術スタック.md` — フォルダ構成・パッケージ
