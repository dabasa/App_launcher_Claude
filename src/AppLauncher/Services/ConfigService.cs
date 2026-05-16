using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppLauncher.Models.Config;

namespace AppLauncher.Services;

public class ConfigService
{
    private static readonly string ConfigDir;
    private static readonly string ConfigPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    static ConfigService()
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        ConfigDir = Path.Combine(exeDir, "config");
        ConfigPath = Path.Combine(ConfigDir, "config.json");
    }

    public AppConfig Current { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Current = CreateDefault();
            Save();
            return;
        }

        string json = File.ReadAllText(ConfigPath);
        Current = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        string json = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(ConfigPath, json, System.Text.Encoding.UTF8);
    }

    private static AppConfig CreateDefault()
    {
        var config = new AppConfig
        {
            Global = new GlobalConfig(),
            Layout = new LayoutConfig(),
            Pages = CreateDefaultPages(),
        };
        return config;
    }

    private static List<PageConfig> CreateDefaultPages()
    {
        var pages = Enumerable.Range(1, 4)
            .Select(i => new PageConfig { Name = $"ページ{i}" })
            .ToList();

#if DEBUG_TILES
        pages[0].Tiles.AddRange(CreateDebugTiles());
#endif

        return pages;
    }

#if DEBUG_TILES
    // 機能定義書 §3.8 に基づくデバッグ用初期タイル（13件）
    private static List<TileConfig> CreateDebugTiles() =>
    [
        // (0,0) 1×1 app — notepad / 画像位置:上
        new() { Col=0, Row=0, ColSpan=1, RowSpan=1, Type="app",
                Title="メモ帳", Path="notepad.exe", Color="blue", ImagePosition="top" },

        // (1,0) 1×1 url — Google / 画像位置:下
        new() { Col=1, Row=0, ColSpan=1, RowSpan=1, Type="url",
                Title="Google", Path="https://www.google.com", Color="green", ImagePosition="bottom" },

        // (2,0) 1×1 folder — デスクトップ / 画像位置:左
        new() { Col=2, Row=0, ColSpan=1, RowSpan=1, Type="folder",
                Title="デスクトップ", Path=@"%USERPROFILE%\Desktop", Color="teal", ImagePosition="left" },

        // (3,0) 1×1 folder — ドキュメント / 画像位置:右
        new() { Col=3, Row=0, ColSpan=1, RowSpan=1, Type="folder",
                Title="ドキュメント", Path=@"%USERPROFILE%\Documents", Color="teal", ImagePosition="right" },

        // (4,0) 1×1 system — CPU使用率 / 円形グラフ
        new() { Col=4, Row=0, ColSpan=1, RowSpan=1, Type="system",
                Title="CPU", Color="gray",
                SystemInfo=new SystemInfoConfig
                {
                    Category="usage", DeviceType="cpu", Target="CPU(0)",
                    DisplayFormat="circle", MainColor="blue", AccentColor="orange",
                    BackgroundAccent="red", Threshold=80, UpdateIntervalMs=1000,
                }},

        // (5,0) 1×1 system — メモリ使用率 / テキスト
        new() { Col=5, Row=0, ColSpan=1, RowSpan=1, Type="system",
                Title="メモリ", Color="gray",
                SystemInfo=new SystemInfoConfig
                {
                    Category="usage", DeviceType="memory", Target="Memory",
                    DisplayFormat="text", MainColor="blue", UpdateIntervalMs=1000,
                }},

        // (0,1) 2×2 app — 電卓（大タイル確認）
        new() { Col=0, Row=1, ColSpan=2, RowSpan=2, Type="app",
                Title="電卓", Path="calc.exe", Color="orange", FontSizePt=20 },

        // (2,1) 2×1 webview — Google
        new() { Col=2, Row=1, ColSpan=2, RowSpan=1, Type="webview",
                Title="WebView", Path="https://www.google.com", Color="purple" },

        // (4,1) 1×2 system — ストレージ C: / テキスト
        new() { Col=4, Row=1, ColSpan=1, RowSpan=2, Type="system",
                Title="C: ドライブ", Color="gray",
                SystemInfo=new SystemInfoConfig
                {
                    Category="storage", DeviceType="cpu", Target=@"C:\",
                    DisplayFormat="text", MainColor="green", UpdateIntervalMs=60000,
                }},

        // (5,1) 1×1 system — OS情報 / テキスト
        new() { Col=5, Row=1, ColSpan=1, RowSpan=1, Type="system",
                Title="OS情報", Color="gray",
                SystemInfo=new SystemInfoConfig
                {
                    Category="os", DisplayFormat="text", MainColor="white", UpdateIntervalMs=60000,
                }},

        // (2,2) 1×1 app — D&D専用（{drop}構文）
        new() { Col=2, Row=2, ColSpan=1, RowSpan=1, Type="app",
                Title="D&D開く", Path="notepad.exe", Args="{drop}", Color="red" },

        // (3,2) 1×1 app — 通常appタイル（D&Dとの比較用）
        new() { Col=3, Row=2, ColSpan=1, RowSpan=1, Type="app",
                Title="メモ帳2", Path="notepad.exe", Color="blue" },

        // (0,3) 1×1 app — GIFアニメーション確認用（imagePath は起動後に設定）
        new() { Col=0, Row=3, ColSpan=1, RowSpan=1, Type="app",
                Title="GIF確認", Path="notepad.exe", Color="pink", ImagePath="" },
    ];
#endif
}
