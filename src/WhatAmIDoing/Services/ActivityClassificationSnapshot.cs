using WhatAmIDoing.Data;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

/// <summary>
/// Shared foreground + rule classification snapshot used by the sampler and the rule inspector UI.
/// </summary>
public static class ActivityClassificationSnapshot
{
    public sealed record Result(
        string ProcessName,
        string WindowTitle,
        TitleContext Context,
        ClassificationRule? MatchedRule,
        long InputIdleMs,
        long EffectiveIdleMs,
        long EffectiveThinkingMs,
        ActivityState State,
        string Category,
        bool Ignored);

    /// <summary>Computes how the foreground window would be classified right now (same logic as <see cref="ActivitySamplingService"/>).</summary>
    public static Result Compute(AppDatabase db)
    {
        var globalIdleMs = db.GetIdleThresholdMs();
        var thinkingExtraMs = db.GetThinkingExtraMs();
        GameControllerIdleTracker.Poll();
        var inputIdleMs = (long)IdleHelper.GetCombinedIdleTime(db.GetControllerInputEngagementEnabled())
            .TotalMilliseconds;

        var fg = ForegroundWindowHelper.TryGetForeground();
        var processName = fg?.ProcessName ?? "(unknown)";
        var title = fg?.WindowTitle ?? "";

        var ctx = TitleContextExtractor.Extract(processName, title);
        var contextValue = ctx.Value;

        var rules = db.GetRules();
        var matched = CategoryClassifier.MatchRule(rules, processName, title, contextValue, ctx.Kind);
        var effectiveIdleMs = (long)(matched?.IdleThresholdMsOverride ?? globalIdleMs);
        var effectiveThinkingMs = (long)(matched?.ThinkingExtraMsOverride ?? thinkingExtraMs);

        if (db.GetPassiveMediaAudioEngagementEnabled()
            && AudioSessionInspector.ForegroundAppRenderEngagement(processName,
                db.GetPassiveMediaPeakFallbackEnabled()))
            inputIdleMs = 0;
        else if (ctx.Kind == ContextKind.YouTube)
        {
            var yScale = db.GetYouTubeContextIdleScale();
            effectiveIdleMs = (long)(effectiveIdleMs * yScale);
            effectiveThinkingMs = (long)(effectiveThinkingMs * yScale);
            effectiveIdleMs = Math.Min(effectiveIdleMs, 120L * 60_000);
            effectiveThinkingMs = Math.Min(effectiveThinkingMs, 60L * 60_000);
        }

        ActivityState state;
        if (inputIdleMs < effectiveIdleMs)
            state = ActivityState.Active;
        else if (inputIdleMs < effectiveIdleMs + effectiveThinkingMs)
            state = ActivityState.Thinking;
        else
            state = ActivityState.Idle;

        string category;
        bool ignored;
        if (state == ActivityState.Idle)
        {
            category = CategoryClassifier.IdleCategory;
            ignored = false;
        }
        else if (matched is null)
        {
            category = CategoryClassifier.Uncategorized;
            ignored = false;
        }
        else if (matched.IgnoreInTotals)
        {
            category = "Ignored";
            ignored = true;
        }
        else
        {
            category = matched.Category;
            ignored = false;
        }

        return new Result(processName, title, ctx, matched, inputIdleMs, effectiveIdleMs, effectiveThinkingMs,
            state, category, ignored);
    }
}
