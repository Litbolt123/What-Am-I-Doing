using System.Globalization;

namespace WhatAmIDoing.Data;

/// <summary>Maintainer-tuned defaults (fresh install + missing-key seed). Values match preferred Settings profile.</summary>
public static class AppSettingsDefaults
{
    public const int IdleThresholdMs = 60_000;
    public const int ThinkingExtraMs = 90_000;
    public const int SampleIntervalMs = 1_000;
    public const double YouTubeContextIdleScale = 20;

    public const string QuietStartHour = "22";
    public const string QuietEndHour = "7";

    public static readonly (string Key, string Value)[] FreshInstall =
    [
        ("idle_threshold_ms", IdleThresholdMs.ToString(CultureInfo.InvariantCulture)),
        ("thinking_extra_ms", ThinkingExtraMs.ToString(CultureInfo.InvariantCulture)),
        ("sample_interval_ms", SampleIntervalMs.ToString(CultureInfo.InvariantCulture)),
        ("audio_detection_enabled", "1"),
        ("passive_media_audio_engagement", "1"),
        ("passive_media_peak_fallback", "1"),
        ("controller_input_engagement", "1"),
        ("youtube_context_idle_scale", YouTubeContextIdleScale.ToString("0.##", CultureInfo.InvariantCulture)),
        ("screens_enabled", "0"),
        ("screens_interval_ms", "60000"),
        ("screens_retention_days", "7"),
        ("screens_excluded_processes", "KeePass,1Password,LastPass,Bitwarden,Dashlane,Enpass,Authy"),
        ("screens_paused_until_utc", ""),
        ("lifecycle_logging_enabled", "1"),
        ("watchdog_restart_enabled", "1"),
        ("desktop_shortcut", "1"),
        ("quiet_hours_enabled", "0"),
        ("quiet_start_hour", QuietStartHour),
        ("quiet_end_hour", QuietEndHour),
        ("auto_check_updates", "1"),
        ("update_notify_tray", "1"),
        ("start_in_system_tray", "0"),
        ("ui_large_text", "0"),
        ("ui_high_contrast", "1"),
        ("ui_keyboard_helpers", "1"),
        ("backup_reminder_enabled", "1"),
        ("html_export_youtube_full_list", "1"),
    ];
}
