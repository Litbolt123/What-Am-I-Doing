using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>Parent-friendly explanation of Active / Thinking / Idle — shown outside the main dashboard.</summary>
public static class TrackingHelpText
{
    public static string Build(AppDatabase db)
    {
        var idleMin = db.GetIdleThresholdMs() / 60000.0;
        var thinkMin = db.GetThinkingExtraMs() / 60000.0;
        var intervalSec = Math.Max(1, db.GetSampleIntervalMs() / 1000);

        return
            "How time is counted\n\n" +
            "The app checks what is on screen about every " + intervalSec + " seconds and classifies each moment as:\n\n" +
            "• Active — you are typing, clicking, or (if enabled) moving a gamepad.\n" +
            "• Thinking — you stopped input for about " + FormatMinutes(idleMin) +
            " but are still at the computer (reading, watching, pausing in an editor).\n" +
            "• Idle / AFK — no meaningful input for about " + FormatMinutes(idleMin + thinkMin) + " total.\n\n" +
            "Watching video or listening\n" +
            "If the focused app is playing sound, that time usually stays Active even without keyboard use. " +
            "Muted YouTube tabs can use a longer grace period (see Settings → Video & long-form watching).\n\n" +
            "Rules & categories\n" +
            "Rules decide labels (Gaming, YouTube, School, etc.). Changing a rule updates past totals on the dashboard and in exports.\n\n" +
            "Per-app tuning\n" +
            "Use Tune detection on the dashboard to suggest idle overrides for specific programs. " +
            "Cursor has a built-in shorter idle + thinking pair in Rules.\n\n" +
            "Privacy\n" +
            "Everything stays on this PC unless you export HTML or a backup.";
    }

    private static string FormatMinutes(double minutes) =>
        minutes < 1 ? $"{minutes * 60:0} sec" : $"{minutes:0.#} min";
}
