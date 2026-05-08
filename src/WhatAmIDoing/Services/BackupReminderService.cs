using System.Windows.Forms;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>Optional monthly tray reminder when backup export hasn’t been recorded recently.</summary>
public static class BackupReminderService
{
    private const string SettingEnabled = "backup_reminder_enabled";
    private const string SettingLastExportUtc = "last_manual_backup_utc";
    private const string SettingLastPromptUtc = "last_backup_prompt_utc";

    public static bool IsEnabled(AppDatabase db) => db.GetSetting(SettingEnabled) == "1";

    public static void RecordManualBackup(AppDatabase db) =>
        db.SetSetting(SettingLastExportUtc, DateTime.UtcNow.ToString("o"));

    public static void MaybeShowTrayReminder(AppDatabase db, NotifyIcon tray)
    {
        if (!IsEnabled(db))
            return;

        var lastExport = ParseUtc(db.GetSetting(SettingLastExportUtc));
        var lastPrompt = ParseUtc(db.GetSetting(SettingLastPromptUtc));

        var now = DateTime.UtcNow;
        // Prompt at most every 32 days; only if no export in the last 35 days (or never exported).
        if (lastPrompt is DateTime lp && (now - lp).TotalDays < 32)
            return;

        if (lastExport is DateTime le && (now - le).TotalDays < 35)
            return;

        try
        {
            tray.ShowBalloonTip(
                12000,
                "Backup reminder",
                "Consider exporting a database backup from Settings (Data backup). Turn this reminder off under Settings → Appearance.",
                ToolTipIcon.Info);
        }
        catch
        {
        }

        db.SetSetting(SettingLastPromptUtc, now.ToString("o"));
    }

    private static DateTime? ParseUtc(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToUniversalTime()
            : null;
    }
}
