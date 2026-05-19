using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

public enum ChartLegendDisplay
{
    Time,
    Percent,
    Both,
}

public static class ChartLegendDisplayService
{
    public const string SettingKey = "dashboard_legend_display";

    public static ChartLegendDisplay Get(AppDatabase db)
    {
        var raw = db.GetSetting(SettingKey);
        return raw?.ToLowerInvariant() switch
        {
            "percent" or "%" => ChartLegendDisplay.Percent,
            "both" => ChartLegendDisplay.Both,
            _ => ChartLegendDisplay.Time,
        };
    }

    public static void Set(AppDatabase db, ChartLegendDisplay display) =>
        db.SetSetting(SettingKey, display switch
        {
            ChartLegendDisplay.Percent => "percent",
            ChartLegendDisplay.Both => "both",
            _ => "time",
        });
}
