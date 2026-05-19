using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

/// <summary>UI grouping for rules (apps vs sites/pages).</summary>
public static class MatchKindUi
{
    public static bool IsAppRule(MatchKind kind) =>
        kind is MatchKind.ProcessNameEquals
            or MatchKind.ProcessNameContains
            or MatchKind.WindowTitleContains;

    public static bool IsSiteOrPageRule(MatchKind kind) => !IsAppRule(kind);

    public static string GetMatchLabel(MatchKind kind) => kind switch
    {
        MatchKind.ProcessNameEquals => "App name is",
        MatchKind.ProcessNameContains => "App contains",
        MatchKind.WindowTitleContains => "Title contains",
        MatchKind.ContextValueContains => "Page / video / project",
        MatchKind.ContextSiteContains => "Page (site)",
        MatchKind.ContextYouTubeVideoContains => "YouTube video",
        MatchKind.ContextProjectContains => "Project (IDE)",
        _ => kind.ToString(),
    };
}
