using WhatAmIDoing.Data;
using WhatAmIDoing.Models;
using WhatAmIDoing.Services;
using Xunit;

namespace WhatAmIDoing.Tests;

public class ReportPerfSemanticsTests
{
    [Fact]
    public void OnComputerSeconds_FromReport_MatchesIgnoreClassifyPass()
    {
        var rules = new List<ClassificationRule>
        {
            new()
            {
                Id = 1,
                MatchKind = MatchKind.ProcessNameEquals,
                Pattern = "IgnoreMe",
                Category = "Noise",
                Priority = 10,
                IgnoreInTotals = true,
            },
            new()
            {
                Id = 2,
                MatchKind = MatchKind.ProcessNameEquals,
                Pattern = "Work",
                Category = "Work",
                Priority = 5,
                IgnoreInTotals = false,
            },
        };

        var start = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Local);
        var end = start.AddDays(1);
        var samples = new List<SampleRow>
        {
            Row(start.ToUniversalTime().AddMinutes(1), "Work", "doc", false, "active"),
            Row(start.ToUniversalTime().AddMinutes(2), "Work", "doc", false, "thinking"),
            Row(start.ToUniversalTime().AddMinutes(3), "Work", "doc", true, "idle"),
            Row(start.ToUniversalTime().AddMinutes(4), "IgnoreMe", "x", false, "active"),
        };

        const int interval = 5;
        var report = ReportAggregator.Build(samples, interval, start, end, rules);
        var fromReport = report.SecondsActiveFocused + report.SecondsThinking + report.SecondsIdle;

        var counted = 0;
        var ignored = 0;
        foreach (var s in samples)
        {
            var (_, ign) = CategoryClassifier.Classify(
                rules, s.ProcessName, s.WindowTitle ?? "", s.UserIdle, s.ContextValue,
                ContextKindExtensions.FromDbString(s.ContextKind));
            if (ign) ignored++; else counted++;
        }

        Assert.Equal(counted * interval, fromReport);
        Assert.Equal(ignored * interval, report.SecondsIgnored);
        Assert.Equal(3 * interval, fromReport);
        Assert.Equal(1 * interval, report.SecondsIgnored);
    }

    private static SampleRow Row(DateTime utc, string proc, string title, bool idle, string state) => new()
    {
        TsUtc = DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        ProcessName = proc,
        WindowTitle = title,
        UserIdle = idle,
        Category = "Uncategorized",
        Ignored = false,
        ActivityState = state,
    };
}
