using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Type4Me.Models;
using Type4Me.NativeMethods;

namespace Type4Me.Services;

/// <summary>
/// Captures the foreground application's identity and window screenshot for LLM context.
/// </summary>
public static class CurrentApplicationContextService
{
    private const int MaxScreenshotSide = 1280;

    public static LLMRequestContext Capture()
    {
        try
        {
            var hwnd = Win32.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return new LLMRequestContext();

            var appName = GetApplicationName(hwnd);
            var screenshot = CaptureWindowScreenshot(hwnd);

            return new LLMRequestContext
            {
                CurrentApplicationName = appName,
                CurrentApplicationScreenshotBase64 = screenshot,
            };
        }
        catch (Exception ex)
        {
            DebugFileLogger.Log($"[Context] Capture failed: {ex.Message}");
            return new LLMRequestContext();
        }
    }

    private static string GetApplicationName(IntPtr hwnd)
    {
        var title = GetWindowTitle(hwnd);
        Win32.GetWindowThreadProcessId(hwnd, out var pid);

        string processName = string.Empty;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            processName = process.ProcessName;
        }
        catch { /* ignore inaccessible process metadata */ }

        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(processName))
            return $"{title} ({processName})";

        return !string.IsNullOrWhiteSpace(title) ? title : processName;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = Win32.GetWindowTextLengthW(hwnd);
        if (length <= 0) return string.Empty;

        var sb = new StringBuilder(length + 1);
        Win32.GetWindowTextW(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string? CaptureWindowScreenshot(IntPtr hwnd)
    {
        if (!Win32.GetWindowRect(hwnd, out var rect))
            return null;

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
            return null;

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height));
        }

        using var resized = ResizeIfNeeded(bitmap);
        using var stream = new MemoryStream();
        resized.Save(stream, ImageFormat.Png);
        return Convert.ToBase64String(stream.ToArray());
    }

    private static Bitmap ResizeIfNeeded(Bitmap source)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= MaxScreenshotSide)
            return new Bitmap(source);

        var scale = (double)MaxScreenshotSide / longest;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(resized);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, 0, 0, width, height);

        return resized;
    }
}
