using WhatAmIDoing.Data;
using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

/// <summary>Fills finished-day rollups in the background so the next 28-day view is warm.</summary>
public static class DayCacheWarmer
{
    private static int _started;
    private static CancellationTokenSource? _cts;

    public static void Start(AppDatabase db)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                // why: let UI + writer settle after launch before scanning samples
                await Task.Delay(TimeSpan.FromSeconds(8), token).ConfigureAwait(false);
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        WarmRecentDays(db, lookbackDays: 60);
                    }
                    catch
                    {
                        // best-effort warmer; next loop retries
                    }

                    await Task.Delay(TimeSpan.FromMinutes(15), token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    public static void WarmRecentDays(AppDatabase db, int lookbackDays)
    {
        var today = DateTime.Today;
        var start = today.AddDays(-Math.Max(1, lookbackDays));
        var rules = db.GetRules();
        var intervalSec = Math.Max(1, db.GetSampleIntervalMs() / 1000);
        // Build through yesterday only; today stays live.
        _ = ReportAggregator.BuildRangeUsingCache(
            db,
            DayAggregateCache.Shared,
            start,
            today,
            rules,
            intervalSec);
    }
}
