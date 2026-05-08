using System.Text;

namespace WhatAmIDoing.Services;

/// <summary>Builds a short comparison vs the immediately preceding period of equal length.</summary>
public static class ReportComparisonText
{
    public static string Build(AggregatedReport current, AggregatedReport previous)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("Compared to previous period (same length, ending the day before this range starts):");

        var curCats = current.SecondsByCategory.Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Take(8)
            .ToList();
        var prevCats = previous.SecondsByCategory;

        foreach (var (cat, sec) in curCats)
        {
            prevCats.TryGetValue(cat, out var psec);
            var delta = sec - psec;
            var sign = delta >= 0 ? "+" : "-";
            var ad = Math.Abs(delta);
            sb.AppendLine($"  • {cat}: {Format(sec)} (was {Format(psec)}, {sign}{Format(ad)})");
        }

        return sb.ToString();
    }

    private static string Format(int seconds)
    {
        if (seconds < 0)
            seconds = 0;
        var t = TimeSpan.FromSeconds(seconds);
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}h {t.Minutes}m";
        if (t.TotalMinutes >= 1)
            return $"{t.Minutes}m";
        return $"{t.Seconds}s";
    }
}
