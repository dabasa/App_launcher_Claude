# Ph.F-E 実装指示書 ─ タイル編集画面セクション再編

対象バージョン：V1.2.0  
作成日：2026-05-24

---

## 概要

タイル編集画面（`TileEditControl`）のセクション構成と項目名を見直し、  
フォント設定（Ph.F-B）の統合後のレイアウトを最終整理する。

### 現行セクション構成

| # | セクション名 | 主な項目 |
|---|---|---|
| 1 | 基本情報 | タイトル、種類 |
| 2 | 起動 | パス・URL、引数、作業フォルダ |
| 3 | コンテンツ | WebView URL |
| 4 | システム情報 | カテゴリ・デバイス・表示形式 etc. |
| 5 | 見た目 | カラー・透過率・フォント名・サイズ・カラー・画像 |

### 変更後セクション構成

| # | セクション名 | 主な項目 | 対象タイル種別 |
|---|---|---|---|
| 1 | 基本情報 | タイトル、種類 | 全種別 |
| 2 | 起動・コンテンツ | パス・URL（種別による）、引数、作業フォルダ | app / url / folder / webview |
| 3 | システム情報 | カテゴリ・デバイス・表示形式 etc. | system |
| 4 | 外観 | カラー、透過率、画像設定 | 全種別 |
| 5 | フォント | タイトルフォント設定詳細ボタン ＋ サマリー | 全種別 |
| 5+ | フォント（続き） | コンテンツフォント設定詳細ボタン ＋ サマリー | system のみ（セクション内条件表示） |

**前提**：Ph.F-A 〜 Ph.F-D の実装完了が必要。

---

## 実装内容

### 1. セクション「起動・コンテンツ」への統合

**対象ファイル**

- `src/AppLauncher/Views/Controls/TileEditControl.xaml`

**変更内容**

現行の「起動」と「コンテンツ」の2セクションを「起動・コンテンツ」1セクションに統合する。

```xml
<!-- 変更前 -->
<Expander Header="起動">...</Expander>
<Expander Header="コンテンツ">...</Expander>

<!-- 変更後 -->
<Expander Header="起動・コンテンツ">
    <!-- 起動セクションの内容 + コンテンツセクションの内容を統合 -->
    <!-- path / args / workDir（app のみ）-->
    <!-- webview URL（webview のみ）-->
</Expander>
```

---

### 2. セクション「見た目」→「外観」へのリネームとフォント分離

**変更内容**

- 「見た目」セクションを「外観」に改称する。
- 「外観」セクションからフォント関連項目（フォント名・フォントサイズ・自動調整・フォントカラー）を削除し、「フォント」セクションへ移動する。

**「外観」セクションの項目（変更後）**

- タイル背景カラー（カラーパレット選択）
- 透過率（スライダー）
- 画像 / アイコン設定
  - 画像パス入力 ＋ 参照ボタン
  - 表示位置（top / bottom / left / right / center）
  - 透過させる（チェックボックス）

**「フォント」セクション（新設）**

```xml
<Expander Header="フォント">
    <StackPanel>
        <!-- タイトルフォント（全タイル共通） -->
        <TextBlock Text="タイトルフォント" FontWeight="SemiBold" Margin="0,0,0,6"/>
        <Button Content="設定詳細..." Click="OnTitleFontDetailClick" HorizontalAlignment="Left"/>
        <TextBlock Text="{Binding TitleFontSummary}" Opacity="0.6" FontSize="11" Margin="0,4,0,0"/>

        <Separator Margin="0,12,0,12"
                   Visibility="{Binding IsSystem, Converter={...BoolToVisibilityConverter}}"/>

        <!-- コンテンツフォント（system タイルのみ） -->
        <TextBlock Text="コンテンツフォント（システム情報表示）" FontWeight="SemiBold" Margin="0,0,0,6"
                   Visibility="{Binding IsSystem, Converter={...BoolToVisibilityConverter}}"/>
        <Button Content="設定詳細..."
                Click="OnContentFontDetailClick" HorizontalAlignment="Left"
                Visibility="{Binding IsSystem, Converter={...BoolToVisibilityConverter}}"/>
        <TextBlock Text="{Binding ContentFontSummary}" Opacity="0.6" FontSize="11" Margin="0,4,0,0"
                   Visibility="{Binding IsSystem, Converter={...BoolToVisibilityConverter}}"/>
    </StackPanel>
</Expander>
```

---

### 3. `TileEditViewModel` へサマリープロパティを追加

**対象ファイル**

- `src/AppLauncher/ViewModels/TileEditViewModel.cs`

```csharp
public string TitleFontSummary =>
    $"{(string.IsNullOrEmpty(_titleFontWork.FontName) ? "デフォルト" : _titleFontWork.FontName)}" +
    $"  {_titleFontWork.FontSizePt} pt" +
    $"  {_titleFontWork.FontColor}" +
    $"{(_titleFontWork.AutoFontSize ? "  [自動]" : "")}";

public string ContentFontSummary =>
    $"{(string.IsNullOrEmpty(_contentFontWork.FontName) ? "デフォルト" : _contentFontWork.FontName)}" +
    $"  {_contentFontWork.FontSizePt} pt" +
    $"{(_contentFontWork.AutoFontSize ? "  [自動]" : "")}";
```

`ApplyTitleFont()` / `ApplyContentFont()` の末尾に通知を追加:

```csharp
OnPropertyChanged(nameof(TitleFontSummary));
// ApplyContentFont 側：
OnPropertyChanged(nameof(ContentFontSummary));
```

---

### 4. セクション展開状態のデフォルト

> **設計変更（2026-05-30）**  
> 当初は基本情報・起動コンテンツ・システム情報をデフォルト展開とする予定だったが、  
> 実装後のユーザーフィードバックにより **全セクションを折りたたみ** に変更した。  
> 理由：編集ダイアログを開くたびに必要なセクションだけ開く操作性の方が好ましいため。

**変更後のルール：**

- **全セクションを初期状態で折りたたみ**
- 編集ダイアログを開くたびに（`SubscribeToTileVm` のタイミングで）全セクションをリセット
- フォント詳細パネル等のサブ画面へ移動して戻ってきたときは、セクションの開閉状態を保持する

| セクション | デフォルト |
|---|---|
| 基本情報 | **折りたたみ** |
| 起動・コンテンツ | **折りたたみ** |
| システム情報 | **折りたたみ** |
| 外観 | 折りたたみ |
| フォント | 折りたたみ |

**実装方法**

XAML で全セクションを `Visibility="Collapsed"` / `▼ ` テキストに設定し、  
`TileEditControl.xaml.cs` の `SubscribeToTileVm()` 内で `ResetSections()` を呼び出す。

```csharp
private void ResetSections()
{
    BasicContent.Visibility      = Visibility.Collapsed;
    LaunchContent.Visibility     = Visibility.Collapsed;
    SystemContent.Visibility     = Visibility.Collapsed;
    AppearanceContent.Visibility = Visibility.Collapsed;
    FontContent.Visibility       = Visibility.Collapsed;

    BasicArrow.Text      = "▼ 基本";
    LaunchArrow.Text     = "▼ 起動・コンテンツ";
    SystemArrow.Text     = "▼ システム情報";
    AppearanceArrow.Text = "▼ 外観";
    FontArrow.Text       = "▼ フォント";
}
```

---

### 5. `TileEditControl.xaml.cs` の更新

**変更内容**

- セクション名変更に伴い、XAML 要素名の変更が必要な場合は更新する。
- `OnTitleFontDetailClick` / `OnContentFontDetailClick` は Ph.F-B で実装済みのため変更不要。

---

## テスト観点

| # | 確認内容 | 期待結果 |
|---|---|---|
| 1 | タイル編集画面を開く | セクションが正しい順序で表示される（基本情報・起動コンテンツ・外観・フォント） |
| 2 | 「起動・コンテンツ」セクションに URL と引数が統合されている | 旧「起動」「コンテンツ」が統合されている |
| 3 | 「外観」セクションにフォント項目がない | フォント名・サイズ・カラーは「フォント」セクションに移動済み |
| 4 | 「フォント」セクションに「タイトルフォント設定詳細」ボタンがある | ボタン押下でダイアログが開く |
| 5 | system タイルでコンテンツフォント設定が表示される | 非 system タイルでは非表示 |
| 6 | フォントサマリーが正しく表示される | フォント名・サイズ・[自動] の有無が表示される |
| 7 | セクションの展開状態が意図した通りである | 基本情報・起動コンテンツが展開、外観・フォントが折りたたみ |
| 8 | 既存の全編集機能（保存・キャンセル・プレビュー）が正常動作する | 再編によるデグレードがない |
