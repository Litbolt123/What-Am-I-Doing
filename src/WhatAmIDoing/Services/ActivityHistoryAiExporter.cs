using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WhatAmIDoing.Data;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

/// <summary>How much detail to include — use <see cref="SummaryOnly"/> for long ranges / small AI context.</summary>
public enum AiExportDetail
{
    SummaryOnly,
    DailyRollup,
    Timeline,
    Csv,
}

/// <summary>Exports local activity history for offline / local AI tools. Settings → Developer only.</summary>
public static class ActivityHistoryAiExporter
{
    private const int TimelineMaxRows = 400;
    private const int TimelineMinSeconds = 45;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static void WriteFile(
        string path,
        AppDatabase db,
        DateTime startUtc,
        DateTime endUtc,
        AiExportDetail detail = AiExportDetail.SummaryOnly)
    {
        var document = BuildDocument(db, startUtc, endUtc, detail);
        var ext = Path.GetExtension(path);
        var body = ext.Equals(".csv", StringComparison.OrdinalIgnoreCase) || detail == AiExportDetail.Csv
            ? BuildCsv(document)
            : ext.Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? BuildJson(document)
                : BuildMarkdown(document);

        File.WriteAllText(path, body, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static AiDocument BuildDocument(
        AppDatabase db,
        DateTime startUtc,
        DateTime endUtc,
        AiExportDetail detail)
    {
        var startLocal = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc).ToLocalTime();
        var endLocal = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc).ToLocalTime();
        var intervalSec = Math.Max(1, db.GetSampleIntervalMs() / 1000);
        var samples = db.GetSamplesBetween(startUtc, endUtc);
        var rules = db.GetRules();
        var report = ReportAggregator.Build(samples, intervalSec, startLocal, endLocal, rules);

        var engagedSeconds = Math.Max(1, report.SecondsActiveFocused + report.SecondsThinking);
        var timeline = BuildTimeline(samples, intervalSec);
        var dailyRollup = BuildDailyRollup(timeline);
        var timelineForExport = detail == AiExportDetail.Timeline
            ? TrimTimeline(timeline)
            : [];

        var ruleNotes = rules
            .Where(r => !string.IsNullOrWhiteSpace(r.Notes))
            .Select(r => $"{r.Category} ({r.Pattern}): {r.Notes!.Trim()}")
            .ToList();

        return new AiDocument
        {
            Detail = detail,
            ExportedLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?",
            RangeStartLocal = startLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
            RangeEndLocal = endLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
            SampleIntervalSeconds = intervalSec,
            SampleCount = samples.Count,
            ActiveMinutes = ToMinutes(report.SecondsActiveFocused),
            ThinkingMinutes = ToMinutes(report.SecondsThinking),
            IdleMinutes = ToMinutes(report.SecondsIdle),
            IgnoredMinutes = report.SecondsIgnored > 0 ? ToMinutes(report.SecondsIgnored) : null,
            EngagedMinutes = ToMinutes(engagedSeconds),
            Categories = BuildRanked(report.SecondsByCategory, engagedSeconds, excludeIdle: true),
            TopApps = BuildRanked(MergeProcessSeconds(report), engagedSeconds),
            TopYoutube = BuildRanked(report.ActiveSecondsByYouTube, engagedSeconds, max: 25),
            TopSites = BuildRanked(report.ActiveSecondsBySite, engagedSeconds, max: 30),
            TopProjects = BuildRanked(report.ActiveSecondsByProject, engagedSeconds, max: 20),
            TimeOfDay = BuildTimeOfDay(report, engagedSeconds),
            DailySummaries = BuildDailySummaries(report, startLocal, endLocal),
            Timeline = timelineForExport,
            TimelineOmitted = detail == AiExportDetail.Timeline && timeline.Count > timelineForExport.Count
                ? timeline.Count - timelineForExport.Count
                : 0,
            DailyRollup = detail is AiExportDetail.DailyRollup or AiExportDetail.Csv ? dailyRollup : [],
            RuleNotes = ruleNotes.Count > 0 ? ruleNotes : null,
        };
    }

    private static Dictionary<string, int> MergeProcessSeconds(AggregatedReport report)
    {
        var merged = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in report.ActiveSecondsByProcess)
            merged[kv.Key] = kv.Value;
        foreach (var kv in report.ThinkingSecondsByProcess)
        {
            if (!merged.TryGetValue(kv.Key, out var v))
                v = 0;
            merged[kv.Key] = v + kv.Value;
        }

        return merged;
    }

    private static List<AiRankedLine> BuildRanked(
        IReadOnlyDictionary<string, int> seconds,
        int engagedTotalSeconds,
        bool excludeIdle = false,
        int max = 20)
    {
        return seconds
            .Where(kv => !excludeIdle || !string.Equals(kv.Key, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase))
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Take(max)
            .Select(kv => new AiRankedLine(
                kv.Key,
                ToMinutes(kv.Value),
                Math.Round(kv.Value * 100.0 / engagedTotalSeconds, 1)))
            .ToList();
    }

    private static List<AiRankedLine> BuildTimeOfDay(AggregatedReport report, int engagedTotalSeconds)
    {
        var buckets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Morning (6am–12pm)"] = 0,
            ["Afternoon (12pm–5pm)"] = 0,
            ["Evening (5pm–10pm)"] = 0,
            ["Night (10pm–6am)"] = 0,
        };

        for (var h = 0; h < report.HourlySecondsByCategory.Length; h++)
        {
            var hourTotal = report.HourlySecondsByCategory[h]
                .Where(kv => !string.Equals(kv.Key, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase))
                .Sum(kv => kv.Value);
            var label = h switch
            {
                >= 6 and < 12 => "Morning (6am–12pm)",
                >= 12 and < 17 => "Afternoon (12pm–5pm)",
                >= 17 and < 22 => "Evening (5pm–10pm)",
                _ => "Night (10pm–6am)",
            };
            buckets[label] += hourTotal;
        }

        return buckets
            .Where(kv => kv.Value > 0)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new AiRankedLine(kv.Key, ToMinutes(kv.Value), Math.Round(kv.Value * 100.0 / engagedTotalSeconds, 1)))
            .ToList();
    }

    private static List<AiDailySummaryLine> BuildDailySummaries(
        AggregatedReport report,
        DateTime startLocal,
        DateTime endLocal)
    {
        var lines = new List<AiDailySummaryLine>();
        var dayCount = Math.Max(1, report.DayCount);
        for (var d = 0; d < dayCount; d++)
        {
            var date = startLocal.Date.AddDays(d);
            if (date > endLocal.Date)
                break;

            if (d >= report.DailyCategorySeconds.Length)
                break;

            var dayCats = report.DailyCategorySeconds[d];
            var engaged = dayCats
                .Where(kv => !string.Equals(kv.Key, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase))
                .Sum(kv => kv.Value);
            if (engaged <= 0)
                continue;

            var top = dayCats
                .Where(kv => !string.Equals(kv.Key, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .Select(kv => $"{kv.Key} {FormatMinutes(ToMinutes(kv.Value))}")
                .ToList();

            lines.Add(new AiDailySummaryLine(
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ToMinutes(engaged),
                string.Join("; ", top)));
        }

        return lines;
    }

    private static List<AiDailyRollupRow> BuildDailyRollup(List<AiTimelineRow> timeline)
    {
        return timeline
            .GroupBy(t => (Date: t.StartLocal[..10], t.Category, t.What))
            .Select(g => new AiDailyRollupRow(
                g.Key.Date,
                g.Key.Category,
                g.Key.What,
                g.Sum(x => x.Seconds),
                g.Count()))
            .OrderBy(r => r.Date)
            .ThenByDescending(r => r.Seconds)
            .ToList();
    }

    private static List<AiTimelineRow> TrimTimeline(List<AiTimelineRow> timeline)
    {
        var filtered = timeline
            .Where(t => t.Seconds >= TimelineMinSeconds)
            .OrderByDescending(t => t.Seconds)
            .Take(TimelineMaxRows)
            .OrderBy(t => t.StartLocal, StringComparer.Ordinal)
            .ToList();
        return filtered;
    }

    private static string BuildMarkdown(AiDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# What Am I Doing — activity export");
        sb.AppendLine();
        sb.AppendLine($"Exported {doc.ExportedLocal} · app {doc.AppVersion} · **{DetailLabel(doc.Detail)}**");
        sb.AppendLine($"Range (local): {doc.RangeStartLocal} → {doc.RangeEndLocal}");
        sb.AppendLine($"Samples in range: {doc.SampleCount:N0} (every {doc.SampleIntervalSeconds}s)");
        sb.AppendLine();
        AppendTotals(sb, doc);
        AppendRankedSection(sb, "## By category", doc.Categories);
        AppendRankedSection(sb, "## Time of day (engaged)", doc.TimeOfDay);
        AppendDailySummaries(sb, doc);
        AppendRankedSection(sb, "## Top apps", doc.TopApps);
        AppendRankedSection(sb, "## Top YouTube (from tab titles)", doc.TopYoutube);
        AppendRankedSection(sb, "## Top sites / pages", doc.TopSites);
        AppendRankedSection(sb, "## Top IDE projects", doc.TopProjects);

        if (doc.Detail == AiExportDetail.DailyRollup)
            AppendDailyRollupMarkdown(sb, doc);
        else if (doc.Detail == AiExportDetail.Timeline)
            AppendTimelineMarkdown(sb, doc);

        if (doc.RuleNotes is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Rule notes");
            foreach (var note in doc.RuleNotes)
                sb.AppendLine($"- {note}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("_Local export. Foreground window titles only — not full browser history._");
        return sb.ToString();
    }

    private static void AppendTotals(StringBuilder sb, AiDocument doc)
    {
        sb.AppendLine("## Totals");
        sb.Append($"Engaged {FormatMinutes(doc.EngagedMinutes)}");
        sb.Append($" (active {FormatMinutes(doc.ActiveMinutes)}");
        if (doc.ThinkingMinutes > 0)
            sb.Append($", thinking {FormatMinutes(doc.ThinkingMinutes)}");
        sb.Append(")");
        sb.Append($" · Idle {FormatMinutes(doc.IdleMinutes)}");
        if (doc.IgnoredMinutes is int ign && ign > 0)
            sb.Append($" · Ignored {FormatMinutes(ign)}");
        sb.AppendLine();
        sb.AppendLine();
    }

    private static void AppendRankedSection(StringBuilder sb, string heading, List<AiRankedLine> lines)
    {
        if (lines.Count == 0)
            return;
        sb.AppendLine(heading);
        foreach (var line in lines)
            sb.AppendLine($"- {line.Name}: {FormatMinutes(line.Minutes)} ({line.Percent}%)");
        sb.AppendLine();
    }

    private static void AppendDailySummaries(StringBuilder sb, AiDocument doc)
    {
        if (doc.DailySummaries.Count == 0)
            return;
        sb.AppendLine("## By day (engaged total + top categories)");
        foreach (var day in doc.DailySummaries)
            sb.AppendLine($"- {day.Date}: {FormatMinutes(day.EngagedMinutes)} — {day.TopCategories}");
        sb.AppendLine();
    }

    private static void AppendDailyRollupMarkdown(StringBuilder sb, AiDocument doc)
    {
        sb.AppendLine("## Daily rollup (merged by day + category + title)");
        sb.AppendLine("| Date | Category | What | Time |");
        sb.AppendLine("|------|----------|------|------|");
        foreach (var row in doc.DailyRollup.Take(500))
            sb.AppendLine($"| {row.Date} | {EscapeMdCell(row.Category)} | {EscapeMdCell(row.What)} | {FormatDuration(row.Seconds)} |");
        if (doc.DailyRollup.Count > 500)
            sb.AppendLine($"| … | | | _{doc.DailyRollup.Count - 500} more rows — narrow the date range_ |");
        sb.AppendLine();
    }

    private static void AppendTimelineMarkdown(StringBuilder sb, AiDocument doc)
    {
        sb.AppendLine("## Timeline (longest stretches first, capped)");
        if (doc.TimelineOmitted > 0)
            sb.AppendLine($"_Omitted {doc.TimelineOmitted} shorter rows (<{TimelineMinSeconds}s or beyond top {TimelineMaxRows}). Use **Daily rollup** or **Summary** for full history._");
        sb.AppendLine();
        if (doc.Timeline.Count == 0)
        {
            sb.AppendLine("_No engaged activity in this range._");
        }
        else
        {
            foreach (var row in doc.Timeline)
                sb.AppendLine(FormatTimelineMarkdown(row));
        }

        sb.AppendLine();
    }

    private static string EscapeMdCell(string s) =>
        s.Replace("|", "/", StringComparison.Ordinal);

    private static string FormatTimelineMarkdown(AiTimelineRow row)
    {
        var state = row.State is "active" or "" or null ? "" : $" [{row.State}]";
        return $"- {row.StartLocal}–{row.EndClock} | **{row.Category}** | {row.What} ({FormatDuration(row.Seconds)}){state}";
    }

    private static string BuildJson(AiDocument doc)
    {
        var payload = new
        {
            format = "what-am-i-doing-ai-export",
            format_version = 3,
            detail = doc.Detail.ToString(),
            exported_local = doc.ExportedLocal,
            app_version = doc.AppVersion,
            range_local = new[] { doc.RangeStartLocal, doc.RangeEndLocal },
            sample_count = doc.SampleCount,
            totals_minutes = new
            {
                engaged = doc.EngagedMinutes,
                active = doc.ActiveMinutes,
                thinking = doc.ThinkingMinutes,
                idle = doc.IdleMinutes,
                ignored = doc.IgnoredMinutes,
            },
            categories = doc.Categories.Select(LineDto),
            time_of_day = doc.TimeOfDay.Select(LineDto),
            daily_summaries = doc.DailySummaries.Select(d => new { d.Date, engaged_minutes = d.EngagedMinutes, top = d.TopCategories }),
            top_apps = doc.TopApps.Select(LineDto),
            top_youtube = doc.TopYoutube.Select(LineDto),
            top_sites = doc.TopSites.Select(LineDto),
            top_projects = doc.TopProjects.Select(LineDto),
            daily_rollup = doc.DailyRollup.Count > 0
                ? doc.DailyRollup.Select(r => new { r.Date, r.Category, what = r.What, minutes = ToMinutes(r.Seconds), r.Sessions })
                : null,
            timeline = doc.Timeline.Count > 0
                ? doc.Timeline.Select(t => new
                {
                    start = t.StartLocal,
                    end = t.EndClock,
                    category = t.Category,
                    what = t.What,
                    seconds = t.Seconds,
                    state = t.State is "active" ? null : t.State,
                })
                : null,
            timeline_omitted_rows = doc.TimelineOmitted > 0 ? doc.TimelineOmitted : (int?)null,
            rule_notes = doc.RuleNotes,
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static object LineDto(AiRankedLine line) => new { line.Name, minutes = line.Minutes, percent = line.Percent };

    private static string BuildCsv(AiDocument doc)
    {
        var sb = new StringBuilder();
        if (doc.Detail == AiExportDetail.SummaryOnly || doc.DailyRollup.Count == 0)
        {
            sb.AppendLine("section,name,minutes,percent");
            foreach (var c in doc.Categories)
                sb.AppendLine($"category,{Csv(c.Name)},{c.Minutes},{c.Percent.ToString(CultureInfo.InvariantCulture)}");
            foreach (var c in doc.TopYoutube)
                sb.AppendLine($"youtube,{Csv(c.Name)},{c.Minutes},{c.Percent.ToString(CultureInfo.InvariantCulture)}");
            foreach (var c in doc.TopSites)
                sb.AppendLine($"site,{Csv(c.Name)},{c.Minutes},{c.Percent.ToString(CultureInfo.InvariantCulture)}");
            foreach (var c in doc.TopApps)
                sb.AppendLine($"app,{Csv(c.Name)},{c.Minutes},{c.Percent.ToString(CultureInfo.InvariantCulture)}");
            foreach (var d in doc.DailySummaries)
                sb.AppendLine($"day,{Csv(d.Date)},{d.EngagedMinutes},");
        }
        else
        {
            sb.AppendLine("date,category,what,minutes,sessions");
            foreach (var row in doc.DailyRollup)
                sb.AppendLine($"{Csv(row.Date)},{Csv(row.Category)},{Csv(row.What)},{ToMinutes(row.Seconds)},{row.Sessions}");
        }

        return sb.ToString();
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.Contains('"', StringComparison.Ordinal) || value.Contains(',', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return value;
    }

    private static string DetailLabel(AiExportDetail detail) => detail switch
    {
        AiExportDetail.SummaryOnly => "Summary only (small context)",
        AiExportDetail.DailyRollup => "Daily rollup",
        AiExportDetail.Timeline => "Timeline (capped)",
        AiExportDetail.Csv => "CSV",
        _ => detail.ToString(),
    };

    private static List<AiTimelineRow> BuildTimeline(IReadOnlyList<SampleRow> samples, int intervalSec)
    {
        var rows = new List<AiTimelineRow>();
        if (samples.Count == 0)
            return rows;

        TimelineBuilder? current = null;
        foreach (var s in samples)
        {
            if (IsIdleOrIgnored(s))
            {
                current = null;
                continue;
            }

            if (current is null || !current.Matches(s))
            {
                current = TimelineBuilder.Start(s, intervalSec);
                rows.Add(current.Row);
            }
            else
            {
                current.Extend(s, intervalSec);
            }
        }

        return rows;
    }

    private static bool IsIdleOrIgnored(SampleRow s) =>
        s.Ignored
        || s.UserIdle
        || string.Equals(s.ActivityState, "idle", StringComparison.OrdinalIgnoreCase)
        || string.Equals(s.Category, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase);

    private static int ToMinutes(int seconds) => (int)Math.Round(seconds / 60.0);

    private static string FormatMinutes(int minutes)
    {
        if (minutes < 1)
            return "<1m";
        if (minutes < 60)
            return $"{minutes}m";
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0 ? $"{h}h" : $"{h}h {m}m";
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds < 60)
            return $"{seconds}s";
        return FormatMinutes(ToMinutes(seconds));
    }

    private static string DescribeWhat(SampleRow s)
    {
        if (!string.IsNullOrWhiteSpace(s.ContextValue))
            return s.ContextValue.Trim();

        if (!string.IsNullOrWhiteSpace(s.WindowTitle))
            return s.WindowTitle.Trim();

        return s.ProcessName;
    }

    private sealed class AiDocument
    {
        public required AiExportDetail Detail { get; init; }
        public required string ExportedLocal { get; init; }
        public required string AppVersion { get; init; }
        public required string RangeStartLocal { get; init; }
        public required string RangeEndLocal { get; init; }
        public required int SampleIntervalSeconds { get; init; }
        public required int SampleCount { get; init; }
        public required int ActiveMinutes { get; init; }
        public required int ThinkingMinutes { get; init; }
        public required int IdleMinutes { get; init; }
        public int? IgnoredMinutes { get; init; }
        public required int EngagedMinutes { get; init; }
        public required List<AiRankedLine> Categories { get; init; }
        public required List<AiRankedLine> TopApps { get; init; }
        public required List<AiRankedLine> TopYoutube { get; init; }
        public required List<AiRankedLine> TopSites { get; init; }
        public required List<AiRankedLine> TopProjects { get; init; }
        public required List<AiRankedLine> TimeOfDay { get; init; }
        public required List<AiDailySummaryLine> DailySummaries { get; init; }
        public required List<AiTimelineRow> Timeline { get; init; }
        public required int TimelineOmitted { get; init; }
        public required List<AiDailyRollupRow> DailyRollup { get; init; }
        public List<string>? RuleNotes { get; init; }
    }

    private sealed record AiRankedLine(string Name, int Minutes, double Percent);

    private sealed record AiDailySummaryLine(string Date, int EngagedMinutes, string TopCategories);

    private sealed record AiDailyRollupRow(string Date, string Category, string What, int Seconds, int Sessions);

    private sealed class AiTimelineRow
    {
        public required string StartLocal { get; set; }
        public required string EndClock { get; set; }
        public int Seconds { get; set; }
        public required string Category { get; init; }
        public required string What { get; init; }
        public required string State { get; init; }
        public required bool Ignored { get; init; }
    }

    private sealed class TimelineBuilder
    {
        public AiTimelineRow Row { get; }

        private TimelineBuilder(AiTimelineRow row) => Row = row;

        public static TimelineBuilder Start(SampleRow s, int intervalSec)
        {
            var local = s.TsUtc.ToLocalTime();
            var what = DescribeWhat(s);
            var row = new AiTimelineRow
            {
                StartLocal = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
                EndClock = local.ToString("HH:mm", CultureInfo.CurrentCulture),
                Seconds = intervalSec,
                Category = s.Category,
                What = what,
                State = s.ActivityState,
                Ignored = s.Ignored,
            };
            return new TimelineBuilder(row);
        }

        public bool Matches(SampleRow s) =>
            string.Equals(Row.Category, s.Category, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Row.What, DescribeWhat(s), StringComparison.Ordinal)
            && string.Equals(Row.State, s.ActivityState, StringComparison.OrdinalIgnoreCase)
            && Row.Ignored == s.Ignored;

        public void Extend(SampleRow s, int intervalSec)
        {
            var local = s.TsUtc.ToLocalTime();
            Row.EndClock = local.ToString("HH:mm", CultureInfo.CurrentCulture);
            Row.Seconds += intervalSec;
        }
    }
}
