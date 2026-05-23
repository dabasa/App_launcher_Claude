# Ph.12 実装指示書 ─ タイルダブルクリック除外 & system タイルプレビュー改善

対象バージョン：V1.1.0  
作成日：2026-05-24

---

## 概要

本フェーズでは以下の 2 機能を実装します。

| 機能ID | 内容 |
|---|---|
| H | タイル上でのダブルクリックをピンモード切替の対象外にする |
| F | タイル編集画面のプレビューに実際のシステム情報を表示する |

---

## H. タイル上のダブルクリックでピンモードが切り替わらないようにする

### 現状

`LauncherWindow.xaml.cs` の `OnMouseDoubleClick`（または類似ハンドラー）がランチャーフレーム全体のダブルクリックを受け取り、ピンモードを切り替えている。タイル上でのダブルクリックも同一イベントに到達するため、意図せずピンモードが切り替わる。

### 実装方針

**対象ファイル**

- `src/AppLauncher/Views/LauncherWindow.xaml.cs`

**変更内容**

ダブルクリックハンドラー（`OnMouseDoubleClick` または `LauncherFrame_MouseDoubleClick`）内で、イベントの元ソースがタイルに属するコントロールであった場合に処理をスキップする。

```csharp
// 例：ヒットテストで「タイルのコントロールであれば除外」するロジック
private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
{
    // タイル上のダブルクリックは除外
    if (IsOnTile(e.OriginalSource as DependencyObject))
        return;

    _vm?.TogglePinCommand.Execute(null);
}

private bool IsOnTile(DependencyObject? source)
{
    while (source != null)
    {
        if (source is TileControl)   // タイルコントロールのクラス名に合わせる
            return true;
        source = VisualTreeHelper.GetParent(source);
    }
    return false;
}
```

既存の `IsInteractiveTarget()` メソッドが存在する場合は、そこにタイルの判定を追加してもよい。

### 確認事項

- タイル以外の領域（ランチャーフレーム・ページ名称エリア等）でのダブルクリックはこれまで通りピンモードが切り替わること。
- タイルの編集ボタン・削除ボタン上でのダブルクリックも除外されること。

---

## F. system タイルのプレビュー表示改善

### 現状

タイル編集モードのプレビューエリアは、タイルの色・透過率を反映するが、`system` タイルのシステム情報テキストは表示されない（プレビューが空白またはテキストなし状態）。

### 実装方針

**対象ファイル**

- `src/AppLauncher/Views/Controls/TileEditPanel.xaml.cs`（または相当するタイル編集パネルのコードビハインド）
- `src/AppLauncher/ViewModels/TileEditViewModel.cs`（または相当する VM）

**変更内容**

1. **プレビュー初期表示時にシステム情報を取得する**  
   タイル種別が `system` の場合、パネルが開いた直後に一度だけシステム情報を取得してプレビューに反映する。

2. **設定変更時に再取得する**  
   情報カテゴリ・デバイス種別・対象が変更されるたびに、システム情報を再取得してプレビューを更新する。

3. **エラー時のフォールバック表示**  
   データ取得に失敗した場合は「─」などのプレースホルダーテキストを表示する。

**実装例（VM 側）**

```csharp
// 既存の SystemInfo 取得ロジック（SystemTileService 等）を流用する
private async Task RefreshSystemPreviewAsync()
{
    try
    {
        var info = await _systemTileService.FetchAsync(Category, DeviceType, Target);
        PreviewText = info.ToDisplayString();
    }
    catch
    {
        PreviewText = "─";
    }
}
```

- 取得は非同期（`async/await`）で行い、UI スレッドをブロックしない。
- 既存の `SystemTileService`（または同等のサービスクラス）を再利用する。
- 新しいスレッドや長命なタイマーは作らない（1 回の取得で完結する）。

### 確認事項

- プレビューパネルを開くと system タイルの情報が表示されること。
- カテゴリや対象を変更するとプレビューが更新されること。
- system 以外の種別ではプレビュー挙動が変わらないこと。
- 取得失敗時に「─」が表示されること。

---

## テスト観点

| # | 確認内容 | 期待結果 |
|---|---|---|
| 1 | タイル上でダブルクリック | ピンモードが切り替わらない |
| 2 | タイル以外の領域でダブルクリック | ピンモードが切り替わる |
| 3 | system タイルの編集パネルを開く | プレビューにシステム情報が表示される |
| 4 | カテゴリを変更する | プレビューが更新される |
| 5 | app タイルの編集パネルを開く | プレビューに変化なし（既存動作） |
| 6 | system 情報取得失敗 | プレビューに「─」が表示される |
