using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>Whether the dashboard opens on launch or only the tray icon is shown.</summary>
public static class StartupTrayService
{
    public const string SettingStartInSystemTray = "start_in_system_tray";

    public static bool IsStartInTrayEnabled(AppDatabase db) =>
        db.GetSetting(SettingStartInSystemTray) == "1";

    public static bool ShouldStartMinimized(AppDatabase db, IEnumerable<string> args) =>
        ArgsRequestTray(args) || IsStartInTrayEnabled(db);

    public static bool ArgsRequestTray(IEnumerable<string> args) =>
        args.Any(a =>
            string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
}
