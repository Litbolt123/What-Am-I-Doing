using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Timers;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>
/// Opt-in periodic screen capture. Encrypts each JPEG with DPAPI (current user) before writing
/// to disk, runs OCR on it (best effort), persists a row to <c>screen_events</c>, and prunes
/// expired captures. Disabled by default; many guardrails (idle, screen lock, exclusion list,
/// pause-for-30-min) gate every capture.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class ScreenCaptureService : IDisposable
{
    private readonly AppDatabase _db;
    private System.Timers.Timer? _timer;
    private System.Timers.Timer? _retentionTimer;
    private bool _running;
    private DateTime _lastRetentionUtc = DateTime.MinValue;

    public ScreenCaptureService(AppDatabase db)
    {
        _db = db;
    }

    public void Start()
    {
        if (_running)
            return;
        _running = true;
        Schedule();
        ScheduleRetention();
    }

    private void Schedule()
    {
        _timer?.Dispose();
        var ms = Math.Clamp(_db.GetScreensIntervalMs(), 5_000, 60 * 60_000);
        _timer = new System.Timers.Timer(ms) { AutoReset = false };
        _timer.Elapsed += (_, _) => Tick();
        _timer.Start();
    }

    private void ScheduleRetention()
    {
        _retentionTimer?.Dispose();
        _retentionTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds)
        {
            AutoReset = true,
        };
        _retentionTimer.Elapsed += (_, _) => RunRetentionPurge();
        _retentionTimer.Start();
        // Also run once at startup so we don't keep stale files indefinitely if the app is rarely open.
        RunRetentionPurge();
    }

    /// <summary>Called from Settings when interval / enable toggle changes.</summary>
    public void Reschedule()
    {
        if (!_running)
            return;
        Schedule();
    }

    private void Tick()
    {
        try
        {
            CaptureOnce();
        }
        catch
        {
            /* never crash the app from a screenshot path */
        }
        finally
        {
            if (_running)
                Schedule();
        }
    }

    private void CaptureOnce()
    {
        if (!_db.GetScreensEnabled())
            return;

        var pausedUntil = _db.GetScreensPausedUntilUtc();
        if (pausedUntil is not null && pausedUntil > DateTime.UtcNow)
            return;

        var idle = IdleHelper.GetIdleTime();
        if (idle.TotalMilliseconds >= _db.GetIdleThresholdMs())
            return;

        if (IsWorkstationLocked())
            return;

        var fg = ForegroundWindowHelper.TryGetForeground();
        var processName = fg?.ProcessName ?? "(unknown)";
        var title = fg?.WindowTitle ?? "";
        if (IsExcluded(processName))
            return;

        byte[] jpegBytes;
        try
        {
            jpegBytes = CapturePrimaryScreenJpeg();
        }
        catch
        {
            return;
        }

        string encryptedPath;
        try
        {
            var encrypted = ProtectedData.Protect(jpegBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jpg.dpapi";
            encryptedPath = Path.Combine(AppPaths.ScreensDirectory, fileName);
            File.WriteAllBytes(encryptedPath, encrypted);
        }
        catch
        {
            return;
        }

        string? text = null;
        try
        {
            text = ScreenOcrService.Recognize(jpegBytes);
        }
        catch
        {
            text = null;
        }

        try
        {
            _db.InsertScreenEvent(DateTime.UtcNow, encryptedPath, text, processName, title);
        }
        catch
        {
            // If DB write fails, delete the orphan image so disk doesn't grow.
            try { File.Delete(encryptedPath); }
            catch { /* best effort */ }
        }
    }

    private bool IsExcluded(string processName)
    {
        var csv = _db.GetScreensExcludedProcesses();
        if (string.IsNullOrWhiteSpace(csv))
            return false;
        foreach (var raw in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (processName.Contains(raw, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static byte[] CapturePrimaryScreenJpeg()
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                     ?? new Rectangle(0, 0, 1920, 1080);
        using var raw = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(raw))
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, raw.Size, CopyPixelOperation.SourceCopy);

        // Downscale to 1280 px wide to keep encrypted blobs small but still readable for OCR.
        var targetWidth = Math.Min(1280, raw.Width);
        var scale = targetWidth / (double)raw.Width;
        var targetHeight = Math.Max(1, (int)Math.Round(raw.Height * scale));

        using var resized = new Bitmap(targetWidth, targetHeight, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(raw, 0, 0, targetWidth, targetHeight);
        }

        using var ms = new MemoryStream();
        var jpegEncoder = ImageCodecInfo.GetImageEncoders()
            .First(e => e.FormatID == ImageFormat.Jpeg.Guid);
        var encoderParams = new EncoderParameters(1)
        {
            Param = { [0] = new EncoderParameter(Encoder.Quality, 70L) },
        };
        resized.Save(ms, jpegEncoder, encoderParams);
        return ms.ToArray();
    }

    /// <summary>Writes a cleartext JPEG copy of the encrypted file to <paramref name="targetPath"/>.</summary>
    public static bool TryDecryptToFile(string encryptedPath, string targetPath)
    {
        try
        {
            var encrypted = File.ReadAllBytes(encryptedPath);
            var clear = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(targetPath, clear);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RunRetentionPurge()
    {
        try
        {
            // Don't hammer DB if multiple ticks fire close together.
            if ((DateTime.UtcNow - _lastRetentionUtc).TotalMinutes < 5)
                return;
            _lastRetentionUtc = DateTime.UtcNow;

            var paths = _db.ExpireOldScreenEvents(_db.GetScreensRetentionDays());
            foreach (var p in paths)
            {
                try { File.Delete(p); }
                catch { /* file may already be gone; that's OK */ }
            }
        }
        catch
        {
            /* never crash from retention */
        }
    }

    public void Dispose()
    {
        _running = false;
        _timer?.Dispose();
        _timer = null;
        _retentionTimer?.Dispose();
        _retentionTimer = null;
    }

    // --- screen-locked detection ----------------------------------------------------

    [DllImport("user32.dll")]
    private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll")]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    private static bool IsWorkstationLocked()
    {
        // OpenInputDesktop fails when the secure desktop (lock screen / UAC prompt) is up.
        var h = OpenInputDesktop(0, false, 0x0001 /*DESKTOP_READOBJECTS*/);
        if (h == IntPtr.Zero)
            return true;
        CloseDesktop(h);
        return false;
    }
}
