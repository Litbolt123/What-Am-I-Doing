using System.IO;
using System.Text.Json;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>
/// Applies one-time settings written by the Inno Setup wizard (Advanced install options),
/// then deletes the marker file so it does not run again.
/// </summary>
internal static class InstallerBootstrap
{
    private const string FileName = "install-bootstrap.json";

    public static void ApplyIfPresent(AppDatabase db)
    {
        var path = Path.Combine(AppPaths.DataDirectory, FileName);
        if (!File.Exists(path))
            return;

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("idleThresholdMs", out var idleEl) &&
                idleEl.TryGetInt32(out var idleMs))
                db.SetIdleThresholdMs(idleMs);

            if (root.TryGetProperty("thinkingExtraMs", out var thinkEl) &&
                thinkEl.TryGetInt32(out var thinkMs))
                db.SetThinkingExtraMs(thinkMs);

            if (root.TryGetProperty("screensEnabled", out var scrEl) &&
                scrEl.ValueKind is JsonValueKind.True or JsonValueKind.False)
                db.SetScreensEnabled(scrEl.GetBoolean());
        }
        catch (Exception ex)
        {
            CrashLogger.Log("InstallerBootstrap.ApplyIfPresent", ex);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }
}
