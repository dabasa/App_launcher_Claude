# Ph.F-C 実装指示書 ─ フォント名プルダウン描画

対象バージョン：V1.2.0  
作成日：2026-05-24

---

## 概要

Ph.F-B で作成したフォント詳細ダイアログの「フォント名」プルダウンを改善する。  
各プルダウン選択肢のラベルを、そのフォント自身で描画する（名前を見れば字体がわかる形式）。

- 選択肢一覧：Windows システムにインストールされている全フォントファミリー
- 先頭に「システムデフォルト」（空文字列に対応）を追加
- 各選択肢のテキストをそのフォントで描画
- 現在選択中のフォント名も `ComboBox` 本体の表示部分に同フォントで表示

**前提**：Ph.F-B（フォント詳細ダイアログ）の実装完了が必要。

---

## 実装内容

### 1. `FontDetailViewModel` へフォントリストを追加

**対象ファイル**

- `src/AppLauncher/ViewModels/FontDetailViewModel.cs`

**変更内容**

`FontFamilies` プロパティを追加する。先頭に空文字列（システムデフォルト）を入れ、以降はシステムフォントを名前順で列挙する。

```csharp
using System.Windows.Media;

// クラス内に追加
public IReadOnlyList<string> FontFamilies { get; } = BuildFontFamilyList();

private static IReadOnlyList<string> BuildFontFamilyList()
{
    var list = new List<string> { "" };  // 空文字列 = システムデフォルト
    list.AddRange(
        Fonts.SystemFontFamilies
             .Select(f => f.Source)
             .OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
    return list;
}
```

---

### 2. `FontDetailPanel.xaml` のプルダウン描画カスタマイズ

**対象ファイル**

- `src/AppLauncher/Views/Controls/FontDetailPanel.xaml`

**変更内容**

`ComboBox` に `ItemTemplate` と `ItemContainerStyle` を追加し、各選択肢をそのフォントで描画する。

```xml
<ComboBox x:Name="FontNameCombo"
          ItemsSource="{Binding FontFamilies}"
          SelectedItem="{Binding FontName}"
          Margin="0,0,0,12">

    <!-- ドロップダウンリスト内の各項目 -->
    <ComboBox.ItemTemplate>
        <DataTemplate>
            <TextBlock x:Name="ItemLabel"
                       Text="{Binding Converter={StaticResource FontNameDisplayConverter}}"
                       Foreground="White"/>
        </DataTemplate>
    </ComboBox.ItemTemplate>

    <!-- 各アイテムのコンテナにフォントを設定するスタイル -->
    <ComboBox.ItemContainerStyle>
        <Style TargetType="ComboBoxItem">
            <Setter Property="FontFamily"
                    Value="{Binding Converter={StaticResource FontNameToFamilyConverter}}"/>
        </Style>
    </ComboBox.ItemContainerStyle>

    <!-- ComboBox 選択中表示部分（折りたたみ時）も同フォントで表示するテンプレート -->
    <ComboBox.SelectionBoxItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Converter={StaticResource FontNameDisplayConverter}}"
                       Foreground="White">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="FontFamily"
                                Value="{Binding Converter={StaticResource FontNameToFamilyConverter}}"/>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
        </DataTemplate>
    </ComboBox.SelectionBoxItemTemplate>
</ComboBox>
```

---

### 3. コンバーターの実装

**対象ファイル**

- `src/AppLauncher/Helpers/FontNameDisplayConverter.cs`（新規）
- `src/AppLauncher/Helpers/FontNameToFamilyConverter.cs`（新規）

**`FontNameDisplayConverter`**：空文字列を「システムデフォルト」として表示する。

```csharp
using System.Globalization;
using System.Windows.Data;

namespace AppLauncher.Helpers;

[ValueConversion(typeof(string), typeof(string))]
public class FontNameDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && string.IsNullOrEmpty(s) ? "システムデフォルト" : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && s == "システムデフォルト" ? "" : value;
}
```

**`FontNameToFamilyConverter`**：フォント名文字列を `FontFamily` オブジェクトに変換する。  
空文字列（システムデフォルト）の場合は `SystemFonts.MessageFontFamily` を返す。

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AppLauncher.Helpers;

[ValueConversion(typeof(string), typeof(FontFamily))]
public class FontNameToFamilyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string name && !string.IsNullOrEmpty(name))
        {
            try { return new FontFamily(name); }
            catch { /* 無効なフォント名 */ }
        }
        return SystemFonts.MessageFontFamily;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

---

### 4. パフォーマンス対策

フォント一覧は数百件になるため、以下の対策を行う。

1. **仮想化の有効化**：`VirtualizingStackPanel` を `ComboBox` の `ItemsPanel` に設定する。

```xml
<ComboBox.ItemsPanel>
    <ItemsPanelTemplate>
        <VirtualizingStackPanel/>
    </ItemsPanelTemplate>
</ComboBox.ItemsPanel>
```

2. **フォントリストのキャッシュ**：`FontDetailViewModel.BuildFontFamilyList()` の結果を  
   `static readonly` フィールドにキャッシュし、毎回 `Fonts.SystemFontFamilies` を列挙しない。

```csharp
private static readonly Lazy<IReadOnlyList<string>> _fontFamiliesCache =
    new(BuildFontFamilyList);

public IReadOnlyList<string> FontFamilies => _fontFamiliesCache.Value;
```

---

## テスト観点

| # | 確認内容 | 期待結果 |
|---|---|---|
| 1 | フォント名プルダウンを開く | 全システムフォントが一覧表示される（先頭は「システムデフォルト」）|
| 2 | 各選択肢のラベルを確認する | ラベルのフォントが選択肢ごとに異なる字体で表示される |
| 3 | 「游ゴシック」など日本語フォントを選択する | 選択後のコンボボックス表示部分も游ゴシックで表示される |
| 4 | 「システムデフォルト」を選択する | 空文字列に対応し、フォント名プロパティが `""` になる |
| 5 | プルダウンを素早くスクロールする | 描画が著しく遅れない（仮想化が機能している） |
| 6 | 存在しないフォント名が config に残っていた場合 | 例外が出ず、デフォルトフォントで表示される |
