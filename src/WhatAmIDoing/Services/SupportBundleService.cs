using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using WhatAmIDoing;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>
/// Creates a zip with log tail + redacted metadata — no window titles or raw samples.
/// </summary>
public static class SupportBundleService
{
    public static void WriteBundleZip(string zipPath, AppDatabase db)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WhatAmIDoing-support-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "readme.txt"),
                """
                What Am I Doing — support bundle
                This archive intentionally excludes sample window titles and process lists.
                Include this zip when reporting an issue on GitHub.
                """.Trim(), Encoding.UTF8);

            File.WriteAllText(Path.Combine(dir, "meta.txt"), BuildMeta(db), Encoding.UTF8);

            var logTail = ReadLatestLogTail(12000);
            File.WriteAllText(Path.Combine(dir, "log_tail.txt"), logTail, Encoding.UTF8);

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            ZipFile.CreateFromDirectory(dir, zipPath);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static string BuildMeta(AppDatabase db)
    {
        var sb = new StringBuilder();
        var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "?";
        sb.AppendLine("version=").AppendLine(ver);
        sb.AppendLine("utc=").AppendLine(DateTime.UtcNow.ToString("o"));
        sb.AppendLine("data_folder=").AppendLine(AppPaths.DataDirectory);
        sb.AppendLine("settings_redacted:");
        foreach (var key in new[]
                 {
                     "sample_interval_ms", "idle_threshold_ms", "thinking_extra_ms",
                     AccessibilityUi.SettingLargeText, AccessibilityUi.SettingHighContrast,
                     AccessibilityUi.SettingKeyboardHelpers,
                     "lifecycle_logging_enabled", "screens_enabled",
                 })
        {
            var v = db.GetSetting(key);
            if (!string.IsNullOrEmpty(v))
                sb.Append("  ").Append(key).Append('=').AppendLine(v);
        }

        sb.AppendLine("pin_configured=").AppendLine(PinManager.IsSet(db) ? "yes" : "no");
        return sb.ToString();
    }

    private static string ReadLatestLogTail(int maxChars)
    {
        try
        {
            var logsDir = AppPaths.LogsDirectory;
            if (!Directory.Exists(logsDir))
                return "(no logs folder)\n";

            var today = Path.Combine(logsDir, $"app-{DateTime.UtcNow:yyyy-MM-dd}.log");
            var files = Directory.GetFiles(logsDir, "app-*.log")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();
            var path = files.FirstOrDefault();
            if (path is null || !File.Exists(path))
                return "(no log files)\n";

            var text = File.ReadAllText(path);
            if (text.Length <= maxChars)
                return text;
            return text[^maxChars..];
        }
        catch (Exception ex)
        {
            return $"(could not read logs: {ex.Message})\n";
        }
    }
}
