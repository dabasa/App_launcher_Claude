# Ph.14 実装指示書 ─ system タイル TOPプロセスカテゴリ追加

対象バージョン：V1.1.0  
作成日：2026-05-24

---

## 概要

システムタイルの情報カテゴリに「TOPプロセス」（`top_process`）を追加する。  
CPU 使用率上位 3 件のプロセス名・CPU 使用率・GPU 使用率・RAM 使用量を表形式で表示する。

---

## 実装内容

### 1. カテゴリ定数の追加

**対象ファイル**

- `src/AppLauncher/Models/SystemInfo/SystemInfoCategory.cs`（または相当する列挙体・定数クラス）

**変更内容**

```csharp
public enum SystemInfoCategory
{
    Os,
    Storage,
    Usage,
    TopProcess,   // 追加
}
```

### 2. データ取得ロジックの追加

**対象ファイル**

- `src/AppLauncher/Services/SystemTileService.cs`（または相当するサービスクラス）

**変更内容**

CPU 使用率降順でプロセスを列挙し、上位 3 件の情報を取得するメソッドを追加する。

```csharp
public record TopProcessEntry(string Name, double CpuPercent, double GpuPercent, double RamMb);

public async Task<IReadOnlyList<TopProcessEntry>> FetchTopProcessesAsync()
{
    // PerformanceCounter / WMI / Process.GetProcesses() 等を使用して取得
    // CPU 使用率は直前の計測値と比較するため、前回値のキャッシュが必要
    // GPUは WMI Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine から取得（取得失敗時は 0）
    // RAM は Process.WorkingSet64 / 1024 / 1024 (MB)
    ...
}
```

- CPU 使用率の計測には `PerformanceCounter("Process", "% Processor Time")` を使用する。
- 初回呼び出し時はカウンタープライミングが必要なため、「─」で表示しても可。
- GPU 使用率取得に失敗した場合（ドライバー非対応等）は `0%` を表示する。

### 3. 表示テキストの生成

**対象ファイル**

- `src/AppLauncher/Services/SystemTileService.cs`（または表示整形クラス）

**表示フォーマット**

```
プロセス名    │ CPU  │ GPU  │ RAM
プロセスA     │  3%  │  0%  │  4MB
プロセスB     │  2%  │  0%  │  2MB
プロセスC     │  1%  │  0%  │  1MB
```

- ヘッダー行・データ行をテキストとして生成し、タイルのテキスト領域に描画する。
- プロセス名の列幅は固定（タイルサイズに応じた文字数）。超過分は末尾 `…` で省略する。
- 各列は右揃えで数値を揃える。
- 等幅フォントの使用が前提。タイルフォントを等幅フォントに設定することを推奨する（設定変更はユーザー任意）。

### 4. タイル編集 UI の対応

**対象ファイル**

- `src/AppLauncher/Views/Controls/TileEditPanel.xaml`
- `src/AppLauncher/ViewModels/TileEditViewModel.cs`

**変更内容**

カテゴリ選択プルダウンに `top_process` を追加する。  
`top_process` 選択時は以下の設定項目を非表示（または無効化）にする：

| 設定項目 | top_process 選択時 |
|---|---|
| デバイス種別 | 非表示 |
| 対象 | 非表示 |
| 表示形式 | 「テキスト表示」のみ（固定） |
| アクセント | 非表示 |
| 背景アクセント | 非表示 |
| 基準値 | 非表示 |

### 5. config.json 対応

- `systemInfo.category` の値として `"top_process"` を保存・読み込みできること。
- `top_process` カテゴリでは `deviceType` / `target` / `displayFormat` / `accentColor` / `backgroundAccent` / `threshold` は不使用（保存しない・または空値）。

---

## テスト観点

| # | 確認内容 | 期待結果 |
|---|---|---|
| 1 | カテゴリで「TOPプロセス」を選択する | 専用設定項目のみ表示される |
| 2 | system タイルとして配置・起動する | CPU上位3プロセスが表形式で表示される |
| 3 | 更新間隔経過後 | プロセス情報が更新される |
| 4 | プロセス名が長い場合 | 末尾が「…」で省略される |
| 5 | GPU 情報取得不可の環境 | GPU 列に「0%」が表示される |
| 6 | config.json に `top_process` を保存し再起動する | 設定が復元される |
