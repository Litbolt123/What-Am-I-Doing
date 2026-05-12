using WhatAmIDoing.Data;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

public enum TuneIntent
{
    Gaming,
    Video,
    Reading,
    Other,
}

public sealed record TuneAnalysisResult(
    int SampleCount,
    int ActiveSamples,
    int ThinkingSamples,
    int IdleSamples,
    double IdleFraction,
    string SummaryLines,
    int? SuggestedIdleOverrideMs,
    string? ExtraRecommendation);

public static class DetectionTuneAnalyzer
{
    public static TuneAnalysisResult Analyze(
        IReadOnlyList<SampleRow> samples,
        string processName,
        TuneIntent intent,
        int globalIdleMs)
    {
        var subset = samples
            .Where(s => s.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var n = subset.Count;
        if (n == 0)
        {
            return new TuneAnalysisResult(0, 0, 0, 0, 0,
                "No samples for that process in this time range.", null,
                "Try a wider window or pick another process.");
        }

        var active = 0;
        var thinking = 0;
        var idle = 0;
        foreach (var s in subset)
        {
            var st = ActivityStateExtensions.FromDbString(s.ActivityState);
            switch (st)
            {
                case ActivityState.Active:
                    active++;
                    break;
                case ActivityState.Thinking:
                    thinking++;
                    break;
                default:
                    idle++;
                    break;
            }
        }

        var idleFrac = idle / (double)n;
        var lines =
            $"Samples for “{processName}”: {n}\r\n" +
            $"• Active (keyboard/controller/audio engaged): {active} ({100.0 * active / n:0.#}%)\r\n" +
            $"• Thinking (foreground but “paused”): {thinking} ({100.0 * thinking / n:0.#}%)\r\n" +
            $"• Idle / AFK: {idle} ({100.0 * idle / n:0.#}%)\r\n";

        int? suggestIdle = null;
        string? extra = null;

        switch (intent)
        {
            case TuneIntent.Gaming when idleFrac >= 0.08:
                suggestIdle = Math.Clamp(globalIdleMs * 14, 15 * 60_000, 120 * 60_000);
                extra =
                    "Because you said this was gaming/emulation but a large share looked Idle, we suggest a **longer idle threshold** " +
                    "for this process only (below). Also enable **gamepad activity** and **speaker audio / peak** in Settings if you haven’t.";
                break;
            case TuneIntent.Video when idleFrac >= 0.08:
                extra =
                    "For watching video: confirm **passive speaker engagement** is on, consider raising the **YouTube factor** for muted tabs, " +
                    "and ensure the tab title shows **YouTube** or **YouTube Music** so stretched idle applies.";
                suggestIdle = Math.Clamp(globalIdleMs * 6, 10 * 60_000, 90 * 60_000);
                break;
            case TuneIntent.Reading when idleFrac >= 0.12 || thinking / (double)n >= 0.35:
                suggestIdle = Math.Clamp(globalIdleMs * 4, 10 * 60_000, 60 * 60_000);
                extra = "Reading often has little mouse movement — a longer idle override on this app reduces false Idle.";
                break;
            case TuneIntent.Other when idleFrac >= 0.15:
                suggestIdle = Math.Clamp(globalIdleMs * 3, 8 * 60_000, 45 * 60_000);
                break;
        }

        return new TuneAnalysisResult(n, active, thinking, idle, idleFrac, lines, suggestIdle, extra);
    }
}
