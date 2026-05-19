using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace AppLauncher.Helpers;

public static class IconExtractHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON      = 0x100;
    private const uint SHGFI_LARGEICON = 0x000;

    /// <summary>
    /// ファイルに関連付けられたアイコンを抽出して PNG として saveDir に保存し、そのパスを返す。
    /// 同じ sourcePath からは同じファイルに保存（重複排除）。失敗時は null。
    /// </summary>
    public static string? ExtractAndSave(string sourcePath, string saveDir)
    {
        if (!File.Exists(sourcePath)) return null;

        var sfi = new SHFILEINFO();
        var result = SHGetFileInfo(sourcePath, 0, ref sfi,
            (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
            SHGFI_ICON | SHGFI_LARGEICON);

        if (result == IntPtr.Zero || sfi.hIcon == IntPtr.Zero) return null;

        try
        {
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                sfi.hIcon, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            Directory.CreateDirectory(saveDir);

            // 同じファイルパスからは同じ PNG に保存（ハッシュでファイル名を決定）
            string hash = ((uint)sourcePath.ToLowerInvariant().GetHashCode()).ToString("x8");
            string savePath = Path.Combine(saveDir, $"{hash}.png");

            if (!File.Exists(savePath))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                using var fs = File.Create(savePath);
                encoder.Save(fs);
            }
            return savePath;
        }
        catch { return null; }
        finally { DestroyIcon(sfi.hIcon); }
    }
}
