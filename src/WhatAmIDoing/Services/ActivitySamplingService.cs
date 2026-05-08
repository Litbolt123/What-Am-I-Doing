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
        var snap = ActivityClassificationSnapshot.Compute(_db);
        var userIdle = snap.State == ActivityState.Idle;

        string? companionAudio = null;
        if (snap.State != ActivityState.Idle && _db.GetAudioDetectionEnabled())
        {
            try
            {
                companionAudio = AudioSessionInspector.Snapshot(excludeProcessName: snap.ProcessName);
            }
            catch
            {
                companionAudio = null;
            }
        }

        var ck = snap.Context.Kind == ContextKind.None ? null : snap.Context.Kind.ToDbString();
        var cv = string.IsNullOrEmpty(snap.Context.Value) ? null : snap.Context.Value;

        _db.InsertSample(
            DateTime.UtcNow,
            snap.ProcessName,
            snap.WindowTitle,
            userIdle,
            snap.Category,
            snap.Ignored,
            ck,
            cv,
            companionAudio,
            snap.State.ToDbString());
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
