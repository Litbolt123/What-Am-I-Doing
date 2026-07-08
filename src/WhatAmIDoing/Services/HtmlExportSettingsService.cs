using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>Options that affect exported HTML reports (daily / weekly).</summary>
public static class HtmlExportSettingsService
{
    public const string SettingIncludeAllYouTube = "html_export_youtube_full_list";

    public static bool GetIncludeAllYouTubeVideos(AppDatabase db) =>
        db.GetSetting(SettingIncludeAllYouTube) != "0";

    public static void SetIncludeAllYouTubeVideos(AppDatabase db, bool enabled) =>
        db.SetSetting(SettingIncludeAllYouTube, enabled ? "1" : "0");
}
