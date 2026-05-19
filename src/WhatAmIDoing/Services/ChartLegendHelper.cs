namespace WhatAmIDoing.Services;

/// <summary>Shared category legend for the dashboard chart and HTML export.</summary>
public static class ChartLegendHelper
{
    public const int DefaultMaxEntries = 8;
    public const int DashboardSwatchSize = 18;
    public const int HtmlSwatchSize = 14;

    public readonly record struct Entry(string Category, int Seconds, int PercentOfEngaged);

    public static bool IsExcludedFromLegend(string category) =>
        string.Equals(category, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase)
        || string.Equals(category, "Ignored", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<Entry> GetTopEntries(AggregatedReport report, int max = DefaultMaxEntries)
    {
        var engagedTotal = GetEngagedSecondsTotal(report);
        return report.SecondsByCategory
            .Where(kv => kv.Value > 0 && !IsExcludedFromLegend(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .Take(max)
            .Select(kv => new Entry(
                kv.Key,
                kv.Value,
                engagedTotal > 0 ? (int)Math.Round(100.0 * kv.Value / engagedTotal) : 0))
            .ToList();
    }

    public static int GetEngagedSecondsTotal(AggregatedReport report) =>
        report.SecondsByCategory
            .Where(kv => kv.Value > 0 && !IsExcludedFromLegend(kv.Key))
            .Sum(kv => kv.Value);

    public static string FormatLabel(Entry entry, ChartLegendDisplay display = ChartLegendDisplay.Time)
    {
        var time = FormatDuration(entry.Seconds);
        var pctText = entry.PercentOfEngaged > 0 ? $"{entry.PercentOfEngaged}%" : "—";
        return display switch
        {
            ChartLegendDisplay.Percent => $"{entry.Category}  {pctText}",
            ChartLegendDisplay.Both when entry.PercentOfEngaged > 0 =>
                $"{entry.Category}  {time} · {entry.PercentOfEngaged}%",
            ChartLegendDisplay.Both => $"{entry.Category}  {time}",
            _ => $"{entry.Category}  {time}",
        };
    }

    public static int GetPercentOfTotalTime(AggregatedReport report, int categorySeconds)
    {
        var total = report.SecondsByCategory.Values.Sum();
        return total > 0 ? (int)Math.Round(100.0 * categorySeconds / total) : 0;
    }

    public static string FormatPercentColumn(int percent) =>
        percent > 0 ? $"{percent}%" : "—";

    public static string FormatDuration(int seconds)
    {
        if (seconds < 0)
            seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }
}
