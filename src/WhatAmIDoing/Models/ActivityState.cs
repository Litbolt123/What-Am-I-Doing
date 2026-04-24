namespace WhatAmIDoing.Models;

/// <summary>
/// Three-way bucketing of a foreground sample by how recently the user actually touched the machine.
/// - <see cref="Active"/>: input within the effective idle threshold (rule override or global).
/// - <see cref="Thinking"/>: between the idle threshold and idle + "thinking grace" (default 3 min).
///   Counts toward the category total (you're still "in" Cursor reading code, or "in" a game) but
///   surfaces separately so it's honest about how much typing / clicking actually happened.
/// - <see cref="Idle"/>: past idle + thinking grace. Shown as "Idle / AFK".
/// </summary>
public enum ActivityState
{
    Active = 0,
    Thinking = 1,
    Idle = 2,
}

public static class ActivityStateExtensions
{
    public static string ToDbString(this ActivityState s) => s switch
    {
        ActivityState.Active => "active",
        ActivityState.Thinking => "thinking",
        ActivityState.Idle => "idle",
        _ => "active",
    };

    public static ActivityState FromDbString(string? s) => s switch
    {
        "active" => ActivityState.Active,
        "thinking" => ActivityState.Thinking,
        "idle" => ActivityState.Idle,
        _ => ActivityState.Active,
    };
}
