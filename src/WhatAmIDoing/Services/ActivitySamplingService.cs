using System.Timers;
using WhatAmIDoing.Data;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

public sealed class ActivitySamplingService : IDisposable
{
    private readonly AppDatabase _db;
    private System.Timers.Timer? _timer;
    private bool _running;

    public ActivitySamplingService(AppDatabase db)
    {
        _db = db;
    }

    public void Start()
    {
        if (_running)
            return;
        _running = true;
        try
        {
            SampleOnce();
        }
        catch
        {
            /* sampling must not crash the app */
        }

        Schedule();
    }

    private void Schedule()
    {
        _timer?.Dispose();
        var ms = Math.Clamp(_db.GetSampleIntervalMs(), 1000, 60_000);
        _timer = new System.Timers.Timer(ms) { AutoReset = false };
        _timer.Elapsed += (_, _) => Tick();
        _timer.Start();
    }

    private void Tick()
    {
        try
        {
            SampleOnce();
        }
        finally
        {
            if (_running)
                Schedule();
        }
    }

    private void SampleOnce()
    {
        var globalIdleMs = _db.GetIdleThresholdMs();
        var thinkingExtraMs = _db.GetThinkingExtraMs();
        var inputIdleMs = (long)IdleHelper.GetIdleTime().TotalMilliseconds;

        var fg = ForegroundWindowHelper.TryGetForeground();
        var processName = fg?.ProcessName ?? "(unknown)";
        var title = fg?.WindowTitle ?? "";

        var ctx = TitleContextExtractor.Extract(processName, title);
        var contextKind = ctx.Kind.ToDbString();
        var contextValue = ctx.Value;

        // Resolve the matched rule FIRST so its per-rule idle threshold (if any) wins over the
        // global default. This is what lets "Cursor" stay Active for 5 min of reading code while
        // a game flips to Idle after the usual 2 min of no input.
        var rules = _db.GetRules();
        var matched = CategoryClassifier.MatchRule(rules, processName, title, contextValue, ctx.Kind);
        var effectiveIdleMs = (long)(matched?.IdleThresholdMsOverride ?? globalIdleMs);
        var effectiveThinkingMs = (long)(matched?.ThinkingExtraMsOverride ?? thinkingExtraMs);

        // Watching video (or listening in the foreground) without touching keyboard/mouse:
        // if the *foreground* app is producing speaker output, treat you as still engaged
        // so long documentaries and streaming aren’t mis-counted as AFK.
        if (_db.GetPassiveMediaAudioEngagementEnabled()
            && AudioSessionInspector.ForegroundAppHasActiveRenderAudio(processName))
            inputIdleMs = 0;
        // YouTube in-browser: when we recognize a YouTube tab, stretch idle+Thinking (muted
        // or quiet playback) so reading comments / watching without sound is still a long
        // “still here” window before we call it idle.
        else if (ctx.Kind == ContextKind.YouTube)
        {
            var yScale = _db.GetYouTubeContextIdleScale();
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

        var userIdle = state == ActivityState.Idle;

        string? companionAudio = null;
        if (state != ActivityState.Idle && _db.GetAudioDetectionEnabled())
        {
            try
            {
                companionAudio = AudioSessionInspector.Snapshot(excludeProcessName: processName);
            }
            catch
            {
                /* audio inspection failures must never break sampling */
                companionAudio = null;
            }
        }

        // Pick the category. Active AND Thinking resolve to the matched rule's category so the
        // category total reflects "time spent in this category" (with a Thinking sub-total
        // surfaced separately by the aggregator). Only the fully-Idle bucket collapses to the
        // generic idle category.
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

        _db.InsertSample(
            DateTime.UtcNow,
            processName,
            title,
            userIdle,
            category,
            ignored,
            string.IsNullOrEmpty(contextKind) ? null : contextKind,
            string.IsNullOrEmpty(contextValue) ? null : contextValue,
            companionAudio,
            state.ToDbString());
    }

    /// <summary>Reload interval from the database (call after changing sample interval in settings).</summary>
    public void Reschedule()
    {
        if (!_running)
            return;
        Schedule();
    }

    public void Dispose()
    {
        _running = false;
        _timer?.Dispose();
        _timer = null;
    }
}
