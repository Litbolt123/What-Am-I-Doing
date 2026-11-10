using System.IO;
using WhatAmIDoing.Data;
using WhatAmIDoing.Services;
using Xunit;

namespace WhatAmIDoing.Tests;

public class DayCacheParityTests
{
    [Fact]
    public void BuildRangeUsingCache_MatchesRawAggregation_OnLiveDb()
    {
        var cachePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "test-day-cache.sqlite3"));
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        if (File.Exists(cachePath))
            File.Delete(cachePath);
        foreach (var side in new[] { cachePath + "-wal", cachePath + "-shm" })
        {
            if (File.Exists(side))
                File.Delete(side);
        }

        var db = new AppDatabase();
        var cache = new DayAggregateCache(cachePath);
        // why: end at today (exclusive) so the live writer cannot append into the compared range
        var endLocal = DateTime.Today;
        var startLocal = endLocal.AddDays(-14);
        var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
        var rules = db.GetRules();
        var intervalSec = Math.Max(1, db.GetSampleIntervalMs() / 1000);

        var rawSamples = db.GetSamplesBetween(startUtc, endUtc, orderByTimestamp: false);
        var raw = ReportAggregator.Build(rawSamples, intervalSec, startLocal, endLocal, rules);

        var cold = ReportAggregator.BuildRangeUsingCache(db, cache, startLocal, endLocal, rules, intervalSec);
        var warm = ReportAggregator.BuildRangeUsingCache(db, cache, startLocal, endLocal, rules, intervalSec);

        AssertParity(raw, cold);
        AssertParity(raw, warm);

        try { File.Delete(cachePath); } catch { /* ignore locks */ }
    }

    private static void AssertParity(AggregatedReport expected, AggregatedReport actual)
    {
        Assert.Equal(expected.TotalSamples, actual.TotalSamples); // TotalSamples
        Assert.Equal(expected.DayCount, actual.DayCount);
        Assert.Equal(expected.SecondsIdle, actual.SecondsIdle);
        Assert.Equal(expected.SecondsThinking, actual.SecondsThinking);
        Assert.Equal(expected.SecondsIgnored, actual.SecondsIgnored);
        Assert.Equal(expected.SecondsActiveFocused, actual.SecondsActiveFocused);
        Assert.Equal(expected.VoiceWhileGamingSeconds, actual.VoiceWhileGamingSeconds);
        AssertDict(expected.SecondsByCategory, actual.SecondsByCategory);
        AssertDict(expected.ThinkingSecondsByCategory, actual.ThinkingSecondsByCategory);
        AssertDict(expected.ActiveSecondsByProcess, actual.ActiveSecondsByProcess);
        AssertDict(expected.ActiveSecondsBySite, actual.ActiveSecondsBySite);
        AssertDict(expected.ActiveSecondsByYouTube, actual.ActiveSecondsByYouTube);
        AssertDict(expected.ActiveSecondsByProject, actual.ActiveSecondsByProject);
        Assert.Equal(expected.DailyCategorySeconds.Length, actual.DailyCategorySeconds.Length);
        for (var d = 0; d < expected.DailyCategorySeconds.Length; d++)
            AssertDict(expected.DailyCategorySeconds[d], actual.DailyCategorySeconds[d]);
    }

    private static void AssertDict(IReadOnlyDictionary<string, int> expected, IReadOnlyDictionary<string, int> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (k, v) in expected)
        {
            Assert.True(actual.TryGetValue(k, out var got), $"missing key {k}");
            Assert.Equal(v, got);
        }
    }
}
