using WhatAmIDoing.Data;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

/// <summary>One row of drill-down evidence for a category click.</summary>
public sealed record CategoryDrillRow(
    DateTime LocalTimestamp,
    string ProcessName,
    string WindowTitle,
    int Seconds,
    int ThinkingSeconds = 0);

public sealed class AggregatedReport
{
    public required DateTime RangeStartLocal { get; init; }
    public required DateTime RangeEndLocal { get; init; }
    public int SampleIntervalSeconds { get; init; }
    public int TotalSamples { get; init; }

    /// <summary>Number of full days covered by this report (1 for day mode, 7 for week mode).</summary>
    public int DayCount { get; init; } = 1;

    /// <summary>
    /// [hourOfDay 0..23] -> category -> seconds. Aggregated across every day in the range,
    /// so 7-day mode shows "what hours of the day you're typically active".
    /// </summary>
    public IReadOnlyDictionary<string, int>[] HourlySecondsByCategory { get; init; } =
        Array.Empty<IReadOnlyDictionary<string, int>>();

    /// <summary>
    /// [dayIndex 0..DayCount-1][hourOfDay 0..23] -> seconds. Used by the heatmap. Counts
    /// both Active and Thinking so the grid reflects "you were engaged with the computer".
    /// Day 0 = RangeStartLocal.Date.
    /// </summary>
    public int[,] DailyHourlyActiveSeconds { get; init; } = new int[0, 24];

    /// <summary>
    /// [dayIndex 0..DayCount-1] -> category -> engaged seconds (Active + Thinking, skipping
    /// Idle and Ignored). Used by the 7-day stacked-bar chart so you can see how much of
    /// what you did each day with the same colors as the daily view.
    /// </summary>
    public IReadOnlyDictionary<string, int>[] DailyCategorySeconds { get; init; } =
        Array.Empty<IReadOnlyDictionary<string, int>>();

    /// <summary>Drill-down rows per category (top window titles + sample timestamps).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<CategoryDrillRow>> DrillDownByCategory { get; init; } =
        new Dictionary<string, IReadOnlyList<CategoryDrillRow>>();

    /// <summary>Seconds attributed to categories (includes Idle line and counts Thinking inside its category).</summary>
    public IReadOnlyDictionary<string, int> SecondsByCategory { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Thinking-only seconds per category (subset of <see cref="SecondsByCategory"/>).</summary>
    public IReadOnlyDictionary<string, int> ThinkingSecondsByCategory { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Seconds for foreground app where the user was actively inputting (state = Active).</summary>
    public IReadOnlyDictionary<string, int> ActiveSecondsByProcess { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Thinking-only seconds per foreground app.</summary>
    public IReadOnlyDictionary<string, int> ThinkingSecondsByProcess { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Top website / page titles (Active + Thinking samples).</summary>
    public IReadOnlyDictionary<string, int> ActiveSecondsBySite { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Top YouTube video titles (Active + Thinking samples).</summary>
    public IReadOnlyDictionary<string, int> ActiveSecondsByYouTube { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Top IDE projects / folders (Active + Thinking samples).</summary>
    public IReadOnlyDictionary<string, int> ActiveSecondsByProject { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Seconds with at least one companion audio session detected (e.g. Discord while gaming).</summary>
    public IReadOnlyDictionary<string, int> CompanionAudioSeconds { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Seconds where the foreground was a Game and any companion audio was active.</summary>
    public int VoiceWhileGamingSeconds { get; init; }

    public int SecondsIdle { get; init; }

    /// <summary>Samples where the user was past the idle threshold but within the "Thinking grace"
    /// window — in foreground with that app, but not typing or clicking.</summary>
    public int SecondsThinking { get; init; }

    public int SecondsIgnored { get; init; }

    /// <summary>Active + thinking + idle + ignored (full coverage of samples).</summary>
    public int SecondsTotalTracked =>
        ActiveSecondsByProcess.Values.Sum() + SecondsThinking + SecondsIdle + SecondsIgnored;

    public int SecondsActiveFocused =>
        ActiveSecondsByProcess.Values.Sum();
}

public static class ReportAggregator
{
    /// <summary>
    /// Build an aggregated report.
    ///
    /// When <paramref name="currentRules"/> is supplied, every sample is re-classified against
    /// the current rule set instead of trusting the category/ignored columns that were written
    /// at sample time. That's what makes rule changes retroactive — if you add a
    /// "Minecraft → Games" rule today, yesterday's Minecraft samples move from Uncategorized
    /// into Games the next time a report is built.
    ///
    /// Pass null only for tests or one-off callers that genuinely want the snapshot view.
    /// </summary>
    public static AggregatedReport Build(
        IReadOnlyList<SampleRow> samples,
        int sampleIntervalSeconds,
        DateTime rangeStartLocal,
        DateTime rangeEndLocal,
        IReadOnlyList<ClassificationRule>? currentRules = null)
    {
        var byCat = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byCatThinking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byProc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byProcThinking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var bySite = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byYouTube = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byProject = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byCompanion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var idle = 0;
        var thinking = 0;
        var ignored = 0;
        var voiceWhileGaming = 0;

        var dayCount = Math.Max(1, (int)Math.Round((rangeEndLocal - rangeStartLocal).TotalDays));
        var hourly = new Dictionary<string, int>[24];
        for (var h = 0; h < 24; h++)
            hourly[h] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dailyHourly = new int[dayCount, 24];
        var dailyCategory = new Dictionary<string, int>[dayCount];
        for (var d = 0; d < dayCount; d++)
            dailyCategory[d] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Per-category drill-down: per (process, title) we track Active vs Thinking seconds
        // so the drill window can split "typing" from "reading / paused".
        var drill = new Dictionary<string,
            Dictionary<(string Proc, string Title), (DateTime FirstSeen, int ActiveSec, int ThinkingSec)>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var s in samples)
        {
            var add = sampleIntervalSeconds;
            var state = ActivityStateExtensions.FromDbString(s.ActivityState);
            var localTs = s.TsUtc.ToLocalTime();
            var hourOfDay = localTs.Hour;
            var dayIndex = Math.Clamp((int)(localTs.Date - rangeStartLocal.Date).TotalDays, 0, dayCount - 1);

            // Re-run the current rules over this sample instead of trusting the stored category.
            // This is what makes "I added a Minecraft rule, now last week's Minecraft time moves
            // from Uncategorized to Games" work without touching the database.
            string effectiveCategory;
            bool effectiveIgnored;
            if (currentRules is not null)
            {
                var (cat, ign) = CategoryClassifier.Classify(
                    currentRules,
                    s.ProcessName,
                    s.WindowTitle ?? "",
                    s.UserIdle,
                    s.ContextValue,
                    ContextKindExtensions.FromDbString(s.ContextKind));
                effectiveCategory = cat;
                effectiveIgnored = ign;
            }
            else
            {
                effectiveCategory = s.Category;
                effectiveIgnored = s.Ignored;
            }

            if (!byCat.TryGetValue(effectiveCategory, out var c))
                c = 0;
            byCat[effectiveCategory] = c + add;

            // Hourly distribution (all categories, including Idle, so the timeline shows AFK gaps).
            var hourBucket = hourly[hourOfDay];
            if (!hourBucket.TryGetValue(effectiveCategory, out var hv))
                hv = 0;
            hourBucket[effectiveCategory] = hv + add;

            if (effectiveIgnored)
            {
                ignored += add;
                continue;
            }

            if (state == ActivityState.Idle || s.UserIdle || effectiveCategory == CategoryClassifier.IdleCategory)
            {
                idle += add;
                continue;
            }

            // From here: Active or Thinking. Both contribute to "engaged" heatmap, drill-down,
            // context rollups and companion audio — Thinking is split out as a separate column
            // rather than excluded entirely.
            dailyHourly[dayIndex, hourOfDay] += add;

            var dayCatBucket = dailyCategory[dayIndex];
            if (!dayCatBucket.TryGetValue(effectiveCategory, out var dcv))
                dcv = 0;
            dayCatBucket[effectiveCategory] = dcv + add;

            if (!drill.TryGetValue(effectiveCategory, out var bucket))
            {
                bucket = new Dictionary<(string, string), (DateTime, int, int)>();
                drill[effectiveCategory] = bucket;
            }

            var key = (s.ProcessName, s.WindowTitle ?? "");
            if (bucket.TryGetValue(key, out var existing))
            {
                var newActive = existing.ActiveSec + (state == ActivityState.Active ? add : 0);
                var newThinking = existing.ThinkingSec + (state == ActivityState.Thinking ? add : 0);
                bucket[key] = (existing.FirstSeen, newActive, newThinking);
            }
            else
            {
                bucket[key] = (localTs,
                    state == ActivityState.Active ? add : 0,
                    state == ActivityState.Thinking ? add : 0);
            }

            if (state == ActivityState.Thinking)
            {
                thinking += add;

                if (!byCatThinking.TryGetValue(effectiveCategory, out var ct))
                    ct = 0;
                byCatThinking[effectiveCategory] = ct + add;

                if (!byProcThinking.TryGetValue(s.ProcessName, out var pt))
                    pt = 0;
                byProcThinking[s.ProcessName] = pt + add;
            }
            else
            {
                if (!byProc.TryGetValue(s.ProcessName, out var p))
                    p = 0;
                byProc[s.ProcessName] = p + add;
            }

            // Site / YouTube / project highlights are derived from the same title parsing as live sampling.
            // Do not trust stored context_kind/context_value alone: older builds misclassified
            // "… - YouTube - Browser" tabs as generic Site; drill-down still showed full titles.
            var extracted = TitleContextExtractor.Extract(s.ProcessName, s.WindowTitle ?? "");
            if (extracted.Kind != ContextKind.None && !string.IsNullOrEmpty(extracted.Value))
            {
                var ctxBucket = extracted.Kind switch
                {
                    ContextKind.Site => bySite,
                    ContextKind.YouTube => byYouTube,
                    ContextKind.Project => byProject,
                    _ => null,
                };
                if (ctxBucket is not null)
                {
                    if (!ctxBucket.TryGetValue(extracted.Value, out var v))
                        v = 0;
                    ctxBucket[extracted.Value] = v + add;
                }
            }

            if (!string.IsNullOrEmpty(s.CompanionAudio))
            {
                foreach (var name in s.CompanionAudio.Split(',',
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!byCompanion.TryGetValue(name, out var v))
                        v = 0;
                    byCompanion[name] = v + add;
                }

                if (effectiveCategory.Contains("Game", StringComparison.OrdinalIgnoreCase))
                    voiceWhileGaming += add;
            }
        }

        var drillFinal = drill.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<CategoryDrillRow>)kv.Value
                .Select(b => new CategoryDrillRow(
                    b.Value.FirstSeen, b.Key.Proc, b.Key.Title, b.Value.ActiveSec, b.Value.ThinkingSec))
                .OrderByDescending(r => r.Seconds + r.ThinkingSeconds)
                .Take(20)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

        return new AggregatedReport
        {
            RangeStartLocal = rangeStartLocal,
            RangeEndLocal = rangeEndLocal,
            SampleIntervalSeconds = sampleIntervalSeconds,
            TotalSamples = samples.Count,
            DayCount = dayCount,
            SecondsByCategory = byCat,
            ThinkingSecondsByCategory = byCatThinking,
            ActiveSecondsByProcess = byProc,
            ThinkingSecondsByProcess = byProcThinking,
            ActiveSecondsBySite = bySite,
            ActiveSecondsByYouTube = byYouTube,
            ActiveSecondsByProject = byProject,
            CompanionAudioSeconds = byCompanion,
            VoiceWhileGamingSeconds = voiceWhileGaming,
            SecondsIdle = idle,
            SecondsThinking = thinking,
            SecondsIgnored = ignored,
            HourlySecondsByCategory = hourly,
            DailyHourlyActiveSeconds = dailyHourly,
            DailyCategorySeconds = dailyCategory
                .Select(d => (IReadOnlyDictionary<string, int>)d)
                .ToArray(),
            DrillDownByCategory = drillFinal,
        };
    }

    public static AggregatedReport BuildRangeUsingCache(
        AppDatabase db,
        DayAggregateCache cache,
        DateTime rangeStartLocal,
        DateTime rangeEndLocal,
        IReadOnlyList<ClassificationRule> rules,
        int sampleIntervalSeconds)
    {
        var startDate = rangeStartLocal.Date;
        var endDate = rangeEndLocal.Date;
        if (endDate <= startDate)
            endDate = startDate.AddDays(1);

        var today = DateTime.Today;
        var rulesFp = DayAggregateCache.RulesFingerprint(rules);
        var fingerprints = db.GetSamplesFingerprintsByLocalDay(startDate, endDate);
        var dayPayloads = new List<(DateTime DayStart, DayAggregatePayload Payload)>();

        for (var day = startDate; day < endDate; day = day.AddDays(1))
        {
            var dayOnly = DateOnly.FromDateTime(day);
            fingerprints.TryGetValue(dayOnly, out var fp);
            var isOpenDay = day >= today;

            if (!isOpenDay
                && cache.TryGet(dayOnly, rulesFp, sampleIntervalSeconds, fp, out var cached))
            {
                dayPayloads.Add((day, cached));
                continue;
            }

            var dayEnd = day.AddDays(1);
            var dayStartUtc = DateTime.SpecifyKind(day, DateTimeKind.Local).ToUniversalTime();
            var dayEndUtc = DateTime.SpecifyKind(dayEnd, DateTimeKind.Local).ToUniversalTime();
            var samples = db.GetSamplesBetween(dayStartUtc, dayEndUtc, orderByTimestamp: false);
            var dayReport = Build(samples, sampleIntervalSeconds, day, dayEnd, rules);
            var payload = DayAggregateCache.FromReport(dayReport);
            if (!isOpenDay)
                cache.Upsert(dayOnly, rulesFp, sampleIntervalSeconds, fp, payload);
            dayPayloads.Add((day, payload));
        }

        return MergeDayPayloads(dayPayloads, rangeStartLocal, rangeEndLocal, sampleIntervalSeconds);
    }

    public static AggregatedReport MergeDayPayloads(
        IReadOnlyList<(DateTime DayStart, DayAggregatePayload Payload)> days,
        DateTime rangeStartLocal,
        DateTime rangeEndLocal,
        int sampleIntervalSeconds)
    {
        var dayCount = Math.Max(1, (int)Math.Round((rangeEndLocal - rangeStartLocal).TotalDays));
        var byCat = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byCatThinking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byProc = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byProcThinking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var bySite = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byYouTube = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byProject = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byCompanion = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var idle = 0;
        var thinking = 0;
        var ignored = 0;
        var voiceWhileGaming = 0;
        var totalSamples = 0;

        var hourly = new Dictionary<string, int>[24];
        for (var h = 0; h < 24; h++)
            hourly[h] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dailyHourly = new int[dayCount, 24];
        var dailyCategory = new Dictionary<string, int>[dayCount];
        for (var d = 0; d < dayCount; d++)
            dailyCategory[d] = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var drill = new Dictionary<string, List<CategoryDrillRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (dayStart, payload) in days)
        {
            var dayIndex = Math.Clamp((int)(dayStart.Date - rangeStartLocal.Date).TotalDays, 0, dayCount - 1);
            totalSamples += payload.TotalSamples;
            idle += payload.SecondsIdle;
            thinking += payload.SecondsThinking;
            ignored += payload.SecondsIgnored;
            voiceWhileGaming += payload.VoiceWhileGamingSeconds;

            AddDict(byCat, payload.SecondsByCategory);
            AddDict(byCatThinking, payload.ThinkingSecondsByCategory);
            AddDict(byProc, payload.ActiveSecondsByProcess);
            AddDict(byProcThinking, payload.ThinkingSecondsByProcess);
            AddDict(bySite, payload.ActiveSecondsBySite);
            AddDict(byYouTube, payload.ActiveSecondsByYouTube);
            AddDict(byProject, payload.ActiveSecondsByProject);
            AddDict(byCompanion, payload.CompanionAudioSeconds);

            for (var h = 0; h < 24 && h < payload.HourlySecondsByCategory.Count; h++)
                AddDict(hourly[h], payload.HourlySecondsByCategory[h]);

            for (var h = 0; h < 24; h++)
            {
                var engaged = h < payload.HourlyEngagedSeconds.Length ? payload.HourlyEngagedSeconds[h] : 0;
                dailyHourly[dayIndex, h] += engaged;
            }

            AddDict(dailyCategory[dayIndex], payload.DailyCategorySeconds);

            foreach (var row in payload.DrillRows)
            {
                if (!drill.TryGetValue(row.Category, out var list))
                {
                    list = new List<CategoryDrillRow>();
                    drill[row.Category] = list;
                }

                list.Add(new CategoryDrillRow(
                    row.LocalTimestamp, row.ProcessName, row.WindowTitle, row.Seconds, row.ThinkingSeconds));
            }
        }

        var drillFinal = drill.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<CategoryDrillRow>)kv.Value
                .GroupBy(r => (Proc: r.ProcessName, Title: r.WindowTitle))
                .Select(g => new CategoryDrillRow(
                    g.Min(x => x.LocalTimestamp),
                    g.Key.Proc,
                    g.Key.Title,
                    g.Sum(x => x.Seconds),
                    g.Sum(x => x.ThinkingSeconds)))
                .OrderByDescending(r => r.Seconds + r.ThinkingSeconds)
                .Take(20)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

        return new AggregatedReport
        {
            RangeStartLocal = rangeStartLocal,
            RangeEndLocal = rangeEndLocal,
            SampleIntervalSeconds = sampleIntervalSeconds,
            TotalSamples = totalSamples,
            DayCount = dayCount,
            SecondsByCategory = byCat,
            ThinkingSecondsByCategory = byCatThinking,
            ActiveSecondsByProcess = byProc,
            ThinkingSecondsByProcess = byProcThinking,
            ActiveSecondsBySite = bySite,
            ActiveSecondsByYouTube = byYouTube,
            ActiveSecondsByProject = byProject,
            CompanionAudioSeconds = byCompanion,
            VoiceWhileGamingSeconds = voiceWhileGaming,
            SecondsIdle = idle,
            SecondsThinking = thinking,
            SecondsIgnored = ignored,
            HourlySecondsByCategory = hourly,
            DailyHourlyActiveSeconds = dailyHourly,
            DailyCategorySeconds = dailyCategory
                .Select(d => (IReadOnlyDictionary<string, int>)d)
                .ToArray(),
            DrillDownByCategory = drillFinal,
        };
    }

    private static void AddDict(Dictionary<string, int> target, IReadOnlyDictionary<string, int>? src)
    {
        if (src is null)
            return;
        foreach (var (k, v) in src)
        {
            target.TryGetValue(k, out var cur);
            target[k] = cur + v;
        }
    }
}
