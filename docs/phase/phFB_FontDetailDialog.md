# Ph.F-B 実装指示書 ─ フォント詳細ダイアログ

対象バージョン：V1.2.0  
作成日：2026-05-24

---

## 概要

フォント関連の設定（フォント名・サイズ・自動調整・カラー）をまとめて編集できる  
**フォント詳細ダイアログ**を実装する。

ダイアログは新しいウィンドウではなく、タイル編集コントロール内に  
オーバーレイ表示するパネル方式とする（既存の削除確認ダイアログと同じ表示形式を大きくした形）。

- タイル編集画面の「フォント」セクションに「タイトルフォント設定」ボタン（全タイル共通）と  
  「コンテンツフォント設定」ボタン（system タイル専用）を配置する。
- ボタン押下でオーバーレイダイアログが開き、設定完了後に OK を押すと編集画面に戻る。
- OK 後、プレビューに即時反映する。

**前提**：Ph.F-A（FontConfig 基盤）の実装完了が必要。

---

## 実装内容

### 1. `FontDetailViewModel` の新規作成

**対象ファイル**

- `src/AppLauncher/ViewModels/FontDetailViewModel.cs`（新規）

フォント詳細ダイアログが保持する作業コピーの ViewModel。  
ダイアログ開く際に `FontConfig` から初期化し、OK 時に呼び出し元へ返す。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using AppLauncher.Models.Config;

namespace AppLauncher.ViewModels;

public partial class FontDetailViewModel : ObservableObject
{
    [ObservableProperty] private string _fontName = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FontSizeSliderEnabled))]
    [NotifyPropertyChangedFor(nameof(PreviewFontSize))]
    private int _fontSizePt = 16;
    [ObservableProperty] private string _fontColor = "white";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FontSizeSliderEnabled))]
    private bool _autoFontSize = false;

    // Ph.F-D で追加予定
    // [ObservableProperty] private string _autoFontSizeMode = "overflow";

    public bool FontSizeSliderEnabled => !AutoFontSize;
    public double PreviewFontSize => FontSizePt * 4.0 / 3.0;

    // プレビューテキスト（ダイアログを開いた目的によって変わる）
    public string PreviewText { get; init; } = "Aa";

    public FontDetailViewModel(FontConfig src, string previewText)
    {
        PreviewText  = previewText;
        _fontName    = src.FontName;
        _fontSizePt  = src.FontSizePt;
        _fontColor   = src.FontColor;
        _autoFontSize = src.AutoFontSize;
    }

    public FontConfig ToConfig() => new()
    {
        FontName     = FontName,
        FontSizePt   = FontSizePt,
        FontColor    = FontColor,
        AutoFontSize = AutoFontSize,
    };
}
```

---

### 2. `FontDetailPanel.xaml` の新規作成

**対象ファイル**

- `src/AppLauncher/Views/Controls/FontDetailPanel.xaml`（新規）
- `src/AppLauncher/Views/Controls/FontDetailPanel.xaml.cs`（新規）

`UserControl` として実装し、`TileEditControl.xaml` 内にオーバーレイとして配置する。

**XAML 構成イメージ**

```xml
<UserControl ...>
    <!-- 半透明の暗い背景（モーダル感演出） -->
    <Grid Background="#AA000000">
        <!-- ダイアログ本体 -->
        <Border Width="320" VerticalAlignment="Center" HorizontalAlignment="Center"
                CornerRadius="12" Background="#FF2A2A2A" Padding="20">
            <StackPanel>
                <!-- タイトル -->
                <TextBlock Text="フォント詳細設定" FontSize="14" FontWeight="SemiBold"
                           Foreground="White" Margin="0,0,0,16"/>

                <!-- フォント名（Ph.F-C で描画機能を追加） -->
                <TextBlock Text="フォント名" Foreground="#CCC" FontSize="11" Margin="0,0,0,4"/>
                <ComboBox x:Name="FontNameCombo"
                          ItemsSource="{Binding FontFamilies}"
                          SelectedItem="{Binding FontName}"
                          Margin="0,0,0,12"/>

                <!-- フォントサイズ -->
                <TextBlock Text="フォントサイズ" Foreground="#CCC" FontSize="11" Margin="0,0,0,4"/>
                <Grid Margin="0,0,0,12">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="48"/>
                    </Grid.ColumnDefinitions>
                    <Slider Grid.Column="0"
                            Minimum="6" Maximum="72"
                            Value="{Binding FontSizePt}"
                            IsEnabled="{Binding FontSizeSliderEnabled}"
                            TickFrequency="1" IsSnapToTickEnabled="True"
                            VerticalAlignment="Center"/>
                    <TextBlock Grid.Column="1"
                               Text="{Binding FontSizePt, StringFormat={}{0} pt}"
                               Foreground="White" TextAlignment="Right" VerticalAlignment="Center"/>
                </Grid>

                <!-- 自動調整チェックボックス -->
                <CheckBox Content="フォントサイズを自動調整する"
                          IsChecked="{Binding AutoFontSize}"
                          Foreground="White" Margin="0,0,0,12"/>

                <!-- フォントカラー -->
                <TextBlock Text="フォントカラー" Foreground="#CCC" FontSize="11" Margin="0,0,0,4"/>
                <ComboBox x:Name="FontColorCombo"
                          ItemsSource="{Binding FontColorOptions}"
                          SelectedItem="{Binding FontColor}"
                          Margin="0,0,0,16"/>

                <!-- プレビュー -->
                <Border Background="#FF1A1A1A" CornerRadius="6" Padding="8" Margin="0,0,0,16">
                    <TextBlock x:Name="PreviewText"
                               Text="{Binding PreviewText}"
                               Foreground="{Binding FontColor, Converter={...ColorToBrushConverter}}"
                               FontSize="{Binding PreviewFontSize}"
                               TextAlignment="Center" TextWrapping="Wrap"
                               HorizontalAlignment="Center"/>
                </Border>

                <!-- OK / キャンセル -->
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="8"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <Button Grid.Column="0" Content="キャンセル"
                            x:Name="CancelButton"/>
                    <Button Grid.Column="2" Content="OK"
                            x:Name="OkButton"/>
                </Grid>
            </StackPanel>
        </Border>
    </Grid>
</UserControl>
```

**コードビハインドのポイント**

```csharp
public partial class FontDetailPanel : UserControl
{
    public event Action<FontConfig>? Confirmed;
    public event Action?             Cancelled;

    public void Open(FontConfig src, string previewText)
    {
        DataContext = new FontDetailViewModel(src, previewText);
        Visibility  = Visibility.Visible;
    }

    private void OnOkClick(...)
    {
        if (DataContext is FontDetailViewModel vm)
            Confirmed?.Invoke(vm.ToConfig());
        Visibility = Visibility.Collapsed;
    }

    private void OnCancelClick(...)
    {
        Cancelled?.Invoke();
        Visibility = Visibility.Collapsed;
    }
}
```

---

### 3. `TileEditControl.xaml` の更新

**対象ファイル**

- `src/AppLauncher/Views/Controls/TileEditControl.xaml`

**変更内容**

1. 最後尾に `FontDetailPanel` を追加（オーバーレイとして全面に配置）。
2. フォントセクションに「フォント設定詳細」ボタンを追加。

```xml
<!-- 既存の編集コンテンツと同じ Grid 内、ZIndex 最大値で追加 -->
<local:FontDetailPanel x:Name="FontDetailPanel"
                       Panel.ZIndex="99"
                       Visibility="Collapsed"/>
```

フォントセクションのボタン（全タイル共通）：

```xml
<Button Content="タイトルフォント設定詳細..."
        Click="OnTitleFontDetailClick"/>
<TextBlock Text="{Binding FontSummary}" Opacity="0.6" FontSize="11"/>
```

system タイル専用（`Visibility` を `IsSystem` でバインド）：

```xml
<Button Content="コンテンツフォント設定詳細..."
        Visibility="{Binding IsSystem, Converter={...BoolToVisibilityConverter}}"
        Click="OnContentFontDetailClick"/>
```

---

### 4. `TileEditControl.xaml.cs` の更新

**対象ファイル**

- `src/AppLauncher/Views/Controls/TileEditControl.xaml.cs`

**追加ハンドラー**

```csharp
private void OnTitleFontDetailClick(object sender, RoutedEventArgs e)
{
    if (_subscribedTileVm is not { } vm) return;
    var src         = vm.ToTitleFontConfig();   // TileEditViewModel に追加するメソッド
    var previewText = string.IsNullOrEmpty(vm.Title) ? "Aa" : vm.Title;
    FontDetailPanel.Confirmed += OnTitleFontConfirmed;
    FontDetailPanel.Cancelled += OnFontDetailCancelled;
    FontDetailPanel.Open(src, previewText);
}

private void OnContentFontDetailClick(object sender, RoutedEventArgs e)
{
    if (_subscribedTileVm is not { } vm) return;
    var src = vm.ToContentFontConfig();
    FontDetailPanel.Confirmed += OnContentFontConfirmed;
    FontDetailPanel.Cancelled += OnFontDetailCancelled;
    FontDetailPanel.Open(src, "99%\nCPU");
}

private void OnTitleFontConfirmed(FontConfig result)
{
    FontDetailPanel.Confirmed -= OnTitleFontConfirmed;
    FontDetailPanel.Cancelled -= OnFontDetailCancelled;
    _subscribedTileVm?.ApplyTitleFont(result);   // TileEditViewModel に追加するメソッド
    RefreshPreview();
}

private void OnContentFontConfirmed(FontConfig result)
{
    FontDetailPanel.Confirmed -= OnContentFontConfirmed;
    FontDetailPanel.Cancelled -= OnFontDetailCancelled;
    _subscribedTileVm?.ApplyContentFont(result);
    RefreshSystemPreviewAsync();
}

private void OnFontDetailCancelled()
{
    FontDetailPanel.Confirmed -= OnTitleFontConfirmed;
    FontDetailPanel.Confirmed -= OnContentFontConfirmed;
    FontDetailPanel.Cancelled -= OnFontDetailCancelled;
}
```

**`TileEditViewModel` に追加するメソッド**

```csharp
public FontConfig ToTitleFontConfig() => Clone(_titleFontWork);
public FontConfig ToContentFontConfig() => Clone(_contentFontWork);

public void ApplyTitleFont(FontConfig cfg)
{
    _titleFontWork = Clone(cfg);
    // 既存バインディングへ通知
    OnPropertyChanged(nameof(FontName));
    OnPropertyChanged(nameof(FontSizePt));
    OnPropertyChanged(nameof(FontColor));
    OnPropertyChanged(nameof(AutoFontSize));
    OnPropertyChanged(nameof(PreviewFontSize));
    OnPropertyChanged(nameof(PreviewForeground));
    OnPropertyChanged(nameof(FontSizeSliderEnabled));
}

public void ApplyContentFont(FontConfig cfg)
{
    _contentFontWork = Clone(cfg);
    // ContentFont 用通知（Ph.F-B 時点では system プレビューで確認）
    OnPropertyChanged(nameof(ContentFontName));
    // ...
}

// サマリー文字列（ボタン下の状態表示用）
public string FontSummary =>
    $"{(string.IsNullOrEmpty(_titleFontWork.FontName) ? "デフォルト" : _titleFontWork.FontName)}" +
    $"  {_titleFontWork.FontSizePt} pt" +
    $"{(_titleFontWork.AutoFontSize ? "  [自動]" : "")}";
```

---

## テスト観点

| # | 確認内容 | 期待結果 |
|---|---|---|
| 1 | 「タイトルフォント設定詳細」ボタンを押す | ダイアログオーバーレイが開く |
| 2 | ダイアログでフォントサイズを変更し OK を押す | 編集画面に戻り、プレビューのフォントサイズが変わる |
| 3 | ダイアログでキャンセルを押す | 変更が破棄され元の設定に戻る |
| 4 | 自動調整チェックを ON にする | フォントサイズスライダーがグレーアウトする |
| 5 | 自動調整 ON でプレビューを確認する | プレビューもスライダー値を無視したサイズで表示 |
| 6 | system タイル編集時に「コンテンツフォント設定詳細」が表示される | system タイル以外では非表示 |
| 7 | コンテンツフォントを変更し OK する | system タイルのシステム情報テキストのフォントが変わる |
| 8 | ダイアログの「タイトルフォント設定詳細」ボタン下のサマリーが更新される | フォント名・サイズが表示される |
| 9 | OK 後に config.json を保存して再起動する | ダイアログで設定したフォントが正しく復元される |
