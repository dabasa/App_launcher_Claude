using System.Text.Json.Serialization;

namespace AppLauncher.Models.Config;

public class TileConfig
{
    public int    Col     { get; set; }
    public int    Row     { get; set; }
    public int    ColSpan { get; set; } = 1;
    public int    RowSpan { get; set; } = 1;
    public string Type    { get; set; } = "app";
    public string Title   { get; set; } = "";
    public string Path    { get; set; } = "";
    public string Args    { get; set; } = "";
    public string WorkDir { get; set; } = "";
    public string Color   { get; set; } = "blue";
    public int    Opacity { get; set; } = 0;
    public string ImagePath        { get; set; } = "";
    public string ImagePosition    { get; set; } = "top";
    public bool   ImageTransparent { get; set; } = false;

    // ─── 新フォーマット ────────────────────────────────────────────────────
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FontConfig? TitleFont   { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FontConfig? ContentFont { get; set; }

    // ─── 旧フォーマット互換（マイグレーション用 読み込みのみ） ────────────
    // MigrateFont() 呼び出し後は全て null にクリアされ、WhenWritingNull により JSON に書き込まれない
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FontName    { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int?    FontSizePt  { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FontColor   { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool?   AutoFontSize { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SystemInfoConfig? SystemInfo { get; set; }

    // ─── マイグレーション ─────────────────────────────────────────────────
    internal void MigrateFont()
    {
        if (TitleFont != null) return;

        TitleFont = new FontConfig
        {
            FontName     = FontName ?? "",
            FontSizePt   = Math.Clamp(FontSizePt ?? 16, 6, 72),
            FontColor    = FontColor ?? "white",
            AutoFontSize = AutoFontSize ?? false,
        };

        // 旧フィールドを null に → WhenWritingNull で JSON 出力から除外
        FontName     = null;
        FontSizePt   = null;
        FontColor    = null;
        AutoFontSize = null;
    }
}
