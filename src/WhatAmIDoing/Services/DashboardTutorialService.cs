using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

public sealed class DashboardTutorialStep
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public FrameworkElement? Target { get; init; }
}

public static class DashboardTutorialService
{
    public const string SettingCompleted = "dashboard_tutorial_completed";
    public const string SettingNotes = "dashboard_tutorial_notes";
    public const string SettingWhatsNewSeenVersion = "dashboard_whats_new_seen_version";

    public static bool IsCompleted(AppDatabase db) => db.GetSetting(SettingCompleted) == "1";

    public static void MarkCompleted(AppDatabase db) => db.SetSetting(SettingCompleted, "1");

    public static void ResetCompleted(AppDatabase db) => db.SetSetting(SettingCompleted, "0");

    public static string GetAppVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? info[..plus] : info;
        }

        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    public static bool HasUnseenWhatsNew(AppDatabase db)
    {
        var cur = GetAppVersion();
        var seen = db.GetSetting(SettingWhatsNewSeenVersion) ?? "";
        return !string.Equals(seen, cur, StringComparison.OrdinalIgnoreCase);
    }

    public static void MarkWhatsNewSeen(AppDatabase db) =>
        db.SetSetting(SettingWhatsNewSeenVersion, GetAppVersion());

    public static IReadOnlyList<string> GetWhatsNewBullets()
    {
        var version = GetAppVersion();
        return new[]
        {
            $"Dashboard refresh in v{version}: headline stats, cleaner cards, and a Catch up section.",
            "Walk through each part of the report with Next — or replay anytime in Settings.",
            "How Active, Thinking, and Idle work is under the ? button (not in the Summary anymore).",
        };
    }

    public static IReadOnlyList<DashboardTutorialStep> BuildSteps(MainWindow window) =>
        new List<DashboardTutorialStep>
        {
            new()
            {
                Title = "Your day at a glance",
                Body = "These four numbers are the headline: time at the computer, active work, reading/paused (Thinking), and fully away (Idle).",
                Target = window.TutorialMetricsHost,
            },
            new()
            {
                Title = "Hourly activity",
                Body = "Colored bars show when you were engaged and which categories dominated each hour. The legend matches the colors below.",
                Target = window.TutorialChartHost,
            },
            new()
            {
                Title = "Summary",
                Body = "Top sites and pages for this range, plus how it compares to the prior period. The box above shows install/close/update times so parents know tracking was on.",
                Target = window.SummaryReportBorder,
            },
            new()
            {
                Title = "Highlights",
                Body = "Top YouTube videos, sites, and project folders from your active time. Switch tabs to explore each list.",
                Target = window.HighlightsReportBorder,
            },
            new()
            {
                Title = "By category",
                Body = "Totals per category (Gaming, School, etc.). Double-click a row to see what filled that bucket.",
                Target = window.CategoryReportBorder,
            },
            new()
            {
                Title = "By app",
                Body = "Which programs collected engaged time. Helpful when one browser covers many sites.",
                Target = window.ProcessReportBorder,
            },
        };
}
