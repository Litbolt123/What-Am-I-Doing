using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

/// <summary>
/// Priority bands used by built-in suggested rules and parent-friendly defaults.
/// Browser-wide rules run at <see cref="BuiltInBrowserProcessRulePriority"/>; user rules that
/// refine sites/titles must usually be higher or they never win.
/// </summary>
public static class RulePriorityGuide
{
    /// <summary>Built-in <c>chrome</c> / <c>comet</c> / … → "Web browser" suggested rules.</summary>
    public const int BuiltInBrowserProcessRulePriority = 190;

    /// <summary>Default priority for new user rules that match page/site/title context (beats browsers).</summary>
    public const int RecommendedUserSiteRulePriority = 200;

    /// <summary>Default priority for new rules that match process name only.</summary>
    public const int DefaultNewProcessRulePriority = 10;

    public static bool IsSiteLikeMatchKind(MatchKind kind) =>
        kind is MatchKind.ContextValueContains
            or MatchKind.ContextSiteContains
            or MatchKind.ContextYouTubeVideoContains
            or MatchKind.ContextProjectContains
            or MatchKind.WindowTitleContains;

    public static bool NeedsPriorityAboveBrowsers(MatchKind kind) => IsSiteLikeMatchKind(kind);

    /// <summary>
    /// Save-time warning: only when the rule can lose to built-in “Web browser” (process 190) in typical use.
    /// Process rules never; IDE <see cref="MatchKind.ContextProjectContains"/> never (different conflict than browser tabs).
    /// </summary>
    public static bool WarnIfSavingBelowBrowserBaseline(MatchKind kind) =>
        kind is MatchKind.ContextValueContains
            or MatchKind.ContextSiteContains
            or MatchKind.ContextYouTubeVideoContains
            or MatchKind.WindowTitleContains;
}
