namespace WhatAmIDoing.Services;

public enum ActivityHistoryPickMode
{
    Process,
    WindowTitle,
    PageOrVideo,
}

public sealed record ActivityHistoryPickResult(
    ActivityHistoryPickMode Mode,
    string Primary,
    string? Secondary,
    string? ContextKind);
