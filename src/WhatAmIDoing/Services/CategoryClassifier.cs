using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

public static class CategoryClassifier
{
    public const string IdleCategory = "Idle (away from keyboard)";
    public const string Uncategorized = "Uncategorized";

    public static (string Category, bool Ignored) Classify(
        IReadOnlyList<ClassificationRule> rules,
        string processName,
        string windowTitle,
        bool userIdle,
        string? contextValue = null,
        ContextKind contextKind = ContextKind.None)
    {
        if (userIdle)
            return (IdleCategory, false);

        foreach (var rule in rules)
        {
            if (!Matches(rule, processName, windowTitle, contextValue ?? "", contextKind))
                continue;

            if (rule.IgnoreInTotals)
                return ("Ignored", true);

            return (rule.Category, false);
        }

        return (Uncategorized, false);
    }

    /// <summary>
    /// Returns the first matching rule for the given foreground signal, regardless of idle state.
    /// Used by the sampler to look up <see cref="ClassificationRule.IdleThresholdMsOverride"/> before
    /// deciding whether the sample is Active / Thinking / Idle.
    /// </summary>
    public static ClassificationRule? MatchRule(
        IReadOnlyList<ClassificationRule> rules,
        string processName,
        string windowTitle,
        string? contextValue = null,
        ContextKind contextKind = ContextKind.None)
    {
        foreach (var rule in rules)
        {
            if (Matches(rule, processName, windowTitle, contextValue ?? "", contextKind))
                return rule;
        }

        return null;
    }

    private static bool Matches(
        ClassificationRule rule,
        string processName,
        string windowTitle,
        string contextValue,
        ContextKind contextKind)
    {
        var pattern = rule.Pattern;
        return rule.MatchKind switch
        {
            MatchKind.ProcessNameEquals => processName.Equals(pattern, StringComparison.OrdinalIgnoreCase),
            MatchKind.ProcessNameContains => processName.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            MatchKind.WindowTitleContains => windowTitle.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            MatchKind.ContextValueContains => contextValue.Length > 0
                && contextValue.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            MatchKind.ContextSiteContains => contextKind == ContextKind.Site
                && contextValue.Length > 0
                && contextValue.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            MatchKind.ContextYouTubeVideoContains => contextKind == ContextKind.YouTube
                && contextValue.Length > 0
                && contextValue.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            MatchKind.ContextProjectContains => contextKind == ContextKind.Project
                && contextValue.Length > 0
                && contextValue.Contains(pattern, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
