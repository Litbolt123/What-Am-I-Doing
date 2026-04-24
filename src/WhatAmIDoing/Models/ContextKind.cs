namespace WhatAmIDoing.Models;

/// <summary>
/// What kind of "what was on screen" enrichment we extracted from the foreground window title.
/// Stored in samples.context_kind as the lowercase name.
/// </summary>
public enum ContextKind
{
    None = 0,
    Site = 1,
    YouTube = 2,
    Project = 3,
}

public static class ContextKindExtensions
{
    public static string ToDbString(this ContextKind kind) => kind switch
    {
        ContextKind.Site => "site",
        ContextKind.YouTube => "youtube",
        ContextKind.Project => "project",
        _ => "",
    };

    public static ContextKind FromDbString(string? s) => s switch
    {
        "site" => ContextKind.Site,
        "youtube" => ContextKind.YouTube,
        "project" => ContextKind.Project,
        _ => ContextKind.None,
    };
}
