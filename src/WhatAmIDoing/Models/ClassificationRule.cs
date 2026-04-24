namespace WhatAmIDoing.Models;

public sealed class ClassificationRule
{
    public long Id { get; init; }
    public MatchKind MatchKind { get; init; }
    public string Pattern { get; init; } = "";
    public string Category { get; init; } = "";
    public int Priority { get; init; }
    public bool IgnoreInTotals { get; init; }

    /// <summary>
    /// Optional per-rule override for "treat as idle after N ms without input". When null the global
    /// <see cref="Data.AppDatabase.GetIdleThresholdMs"/> value applies. IDE-style rules (Cursor, VS Code,
    /// JetBrains IDEs) set a longer value so reading code for a minute doesn't flip to idle.
    /// </summary>
    public int? IdleThresholdMsOverride { get; init; }

    /// <summary>
    /// Optional per-rule override for the "Thinking grace" window added on top of the idle threshold.
    /// When null the global <see cref="Data.AppDatabase.GetThinkingExtraMs"/> value applies. Together
    /// these two overrides let a rule shape all three buckets, e.g. Cursor at 3 min idle + 2 min
    /// thinking yields Active 0–3 / Thinking 3–5 / Idle 5+.
    /// </summary>
    public int? ThinkingExtraMsOverride { get; init; }

    /// <summary>True when this row came from the built-in suggested set (still editable by deleting in the UI).</summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// Optional free-form explanation of why this rule exists — surfaced as a tooltip in the
    /// rules grid and visible to parents when sharing reports. Null or empty means "no note".
    /// </summary>
    public string? Notes { get; init; }
}
