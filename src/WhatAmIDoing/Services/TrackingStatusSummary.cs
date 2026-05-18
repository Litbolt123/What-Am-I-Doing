using System.Globalization;
using System.Text;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>
/// Parent-facing lines about whether the tracker was installed, updated, or not running during a report range.
/// </summary>
public static class TrackingStatusSummary
{
    public sealed record Result(IReadOnlyList<string> Lines, bool HasAlerts);

    public static Result Build(DateTime startUtc, DateTime endUtc)
    {
        var culture = CultureInfo.CurrentCulture;
        var lines = new List<string>();
        var hasAlerts = false;

        var firstUtc = App.Db.GetTrackerFirstRunUtc();
        if (firstUtc is { } first)
        {
            var firstLocal = first.ToLocalTime();
            lines.Add($"Tracking on this PC since {firstLocal.ToString("MMMM d, yyyy", culture)}.");
        }
        else
            lines.Add("Tracking on this PC (install date not recorded yet).");

        if (!App.Db.GetLifecycleLoggingEnabled())
        {
            lines.Add(
                "Open/close logging is off in Settings — this report cannot show when the app was closed or updated.");
            return new Result(lines, HasAlerts: true);
        }

        var events = App.Db.GetLifecycleEventsBetween(startUtc, endUtc);
        var periodLines = new List<(DateTime Utc, string Text)>();

        if (firstUtc is { } installed && installed >= startUtc && installed < endUtc)
        {
            hasAlerts = true;
            periodLines.Add((
                installed,
                $"Installed on this PC — {installed.ToLocalTime().ToString("g", culture)}"));
        }

        foreach (var ev in events)
        {
            var kind = ev.Kind.ToLowerInvariant();
            var when = ev.EventUtc.ToLocalTime();
            var whenText = when.ToString("g", culture);

            switch (kind)
            {
                case "upgrade":
                    hasAlerts = true;
                    var detail = string.IsNullOrWhiteSpace(ev.Detail) ? "" : $" ({ev.Detail.Trim()})";
                    periodLines.Add((ev.EventUtc, $"App updated — {whenText}{detail}"));
                    break;

                case "quit_update":
                    hasAlerts = true;
                    periodLines.Add((
                        ev.EventUtc,
                        $"Stopped to install an update — {whenText} (tracking paused until the app runs again)"));
                    break;

                case "quit":
                    hasAlerts = true;
                    var resume = FindNextStart(events, ev.EventUtc);
                    if (resume is { } resumeUtc)
                    {
                        var resumeLocal = resumeUtc.ToLocalTime();
                        periodLines.Add((
                            ev.EventUtc,
                            $"Not tracking — {FormatLocalRange(when, resumeLocal, culture)} (app was closed)"));
                    }
                    else
                        periodLines.Add((
                            ev.EventUtc,
                            $"App closed — {whenText} (tracking may have stopped after this)"));
                    break;

            }
        }

        if (periodLines.Count == 0)
        {
            lines.Add("During this period: no closes, updates, or new installs logged.");
            return new Result(lines, hasAlerts);
        }

        lines.Add("During this period:");
        foreach (var (_, text) in periodLines.OrderBy(p => p.Utc))
            lines.Add("  • " + text);

        return new Result(lines, hasAlerts);
    }

    /// <summary>Chronological log for the More details expander.</summary>
    public static string? BuildFullActivityLog(DateTime startUtc, DateTime endUtc)
    {
        if (!App.Db.GetLifecycleLoggingEnabled())
            return null;

        var events = App.Db.GetLifecycleEventsBetween(startUtc, endUtc);
        if (events.Count == 0)
            return null;

        var culture = CultureInfo.CurrentCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Full activity log:");
        foreach (var ev in events)
        {
            var local = ev.EventUtc.ToLocalTime().ToString("g", culture);
            var label = KindLabel(ev.Kind);
            var detail = string.IsNullOrWhiteSpace(ev.Detail) ? "" : $" — {ev.Detail}";
            sb.AppendLine($"  • {local}: {label}{detail}");
        }

        return sb.ToString().TrimEnd();
    }

    private static DateTime? FindNextStart(IReadOnlyList<AppLifecycleEvent> events, DateTime afterUtc)
    {
        foreach (var ev in events)
        {
            if (ev.EventUtc <= afterUtc)
                continue;
            if (string.Equals(ev.Kind, "start", StringComparison.OrdinalIgnoreCase))
                return ev.EventUtc;
        }

        return null;
    }

    private static string FormatLocalRange(DateTime fromLocal, DateTime toLocal, CultureInfo culture)
    {
        if (fromLocal.Date == toLocal.Date)
            return $"{fromLocal.ToString("t", culture)}–{toLocal.ToString("t", culture)} on {fromLocal.ToString("MMMM d", culture)}";

        return $"{fromLocal.ToString("g", culture)} – {toLocal.ToString("g", culture)}";
    }

    private static string KindLabel(string kind) => kind.ToLowerInvariant() switch
    {
        "start" => "App started",
        "quit" => "App closed",
        "quit_update" => "Stopped (installing app update)",
        "upgrade" => "App updated",
        _ => kind,
    };
}
