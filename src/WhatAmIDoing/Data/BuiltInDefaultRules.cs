using WhatAmIDoing.Models;

namespace WhatAmIDoing.Data;

/// <summary>
/// Suggested categories for common apps. Seeded once when the rules table is empty; user can edit or delete any row.
/// Higher priority runs first.
/// </summary>
public static class BuiltInDefaultRules
{
    public static IReadOnlyList<BuiltInRule> All => Rules;

    private static readonly BuiltInRule[] Rules =
    [
        // Context value (extracted from titles) — runs at very high priority so it can
        // refine generic browser categorization for specific sites / channels.
        new(MatchKind.ContextValueContains, "Khan Academy", "Educational video", 280),
        new(MatchKind.ContextValueContains, "Crash Course", "Educational video", 280),
        new(MatchKind.ContextValueContains, "MIT OpenCourseWare", "Educational video", 280),

        // Window title (specific sites / contexts)
        new(MatchKind.WindowTitleContains, "YouTube", "YouTube", 260),
        new(MatchKind.WindowTitleContains, "Netflix", "Streaming video", 255),
        new(MatchKind.WindowTitleContains, "Twitch", "Streaming video", 255),
        new(MatchKind.WindowTitleContains, "Disney+", "Streaming video", 254),
        new(MatchKind.WindowTitleContains, "Spotify", "Music & audio", 250),

        // Development & creative tools. IDEs get a modest idle override because reading code
        // is still productive. Cursor lands on Active 0–30s / Thinking 30s–1m / Idle 1m+.
        // Other IDEs / creative apps use 2.5 min idle so a long paragraph of reading still
        // counts as engaged time.
        new(MatchKind.ProcessNameContains, "Cursor", "Development", 220,
            IdleThresholdMsOverride: 30_000,
            ThinkingExtraMsOverride: 30_000),
        new(MatchKind.ProcessNameContains, "Code", "Development", 215, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "devenv", "Development", 215, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "rider", "Development", 215, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "WebStorm", "Development", 215, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "PyCharm", "Development", 215, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "sublime", "Development", 215, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "blender", "Creative", 200, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "Photoshop", "Creative", 200, IdleThresholdMsOverride: 150_000),

        // Browsers (process name)
        new(MatchKind.ProcessNameContains, "chrome", "Web browser", 190),
        new(MatchKind.ProcessNameContains, "msedge", "Web browser", 190),
        new(MatchKind.ProcessNameContains, "firefox", "Web browser", 190),
        new(MatchKind.ProcessNameContains, "brave", "Web browser", 190),
        new(MatchKind.ProcessNameContains, "opera", "Web browser", 190),
        new(MatchKind.ProcessNameContains, "vivaldi", "Web browser", 190),
        new(MatchKind.ProcessNameContains, "comet", "Web browser", 190),

        // Communication
        new(MatchKind.ProcessNameContains, "Discord", "Communication", 180),
        new(MatchKind.ProcessNameContains, "Slack", "Communication", 180),
        new(MatchKind.ProcessNameContains, "Teams", "Communication", 175),
        new(MatchKind.ProcessNameContains, "Zoom", "Communication", 175),
        new(MatchKind.ProcessNameContains, "spotify", "Music & audio", 170),

        // Gaming — process names (TopUp adds if missing). Low-priority title rule below catches
        // "Roblox" in the window title when the process row does not match.
        new(MatchKind.ProcessNameContains, "RobloxPlayerBeta", "Gaming", 165),
        new(MatchKind.ProcessNameContains, "RobloxStudio", "Gaming / creative", 164),
        new(MatchKind.ProcessNameContains, "PrismLauncher", "Gaming", 163),
        new(MatchKind.ProcessNameContains, "Minecraft.Windows", "Gaming", 10),
        new(MatchKind.ProcessNameContains, "Lunar Client", "Gaming", 161),

        // Gaming storefronts / common launchers
        new(MatchKind.ProcessNameContains, "steam", "Gaming", 160),
        new(MatchKind.ProcessNameContains, "EpicGamesLauncher", "Gaming", 160),
        new(MatchKind.ProcessNameContains, "Battle.net", "Gaming", 160),

        // Productivity
        new(MatchKind.ProcessNameContains, "WINWORD", "Documents", 120),
        new(MatchKind.ProcessNameContains, "EXCEL", "Documents", 120),
        new(MatchKind.ProcessNameContains, "POWERPNT", "Documents", 120),
        new(MatchKind.ProcessNameContains, "OneNote", "Documents", 115),

        // Notes / quick productivity. Sticky Notes renders via ApplicationFrameHost on Win11,
        // so match on the window title instead of the process name. (Family preset: count as Documents.)
        new(MatchKind.WindowTitleContains, "Sticky Notes", "Documents", 150),
        new(MatchKind.ProcessNameContains, "Notepad", "Notes", 140),

        // Hardware / monitoring utilities. PredatorSense you actually use (fan curves, temps),
        // so it stays visible as "System tools" and counts when you're interacting with it.
        // ThrottleStop and HWiNFO mostly sit in the background collecting data, so they're
        // excluded from totals by default — edit the rule if you want to count them.
        new(MatchKind.ProcessNameContains, "PredatorSense", "System tools", 75),
        new(MatchKind.ProcessNameContains, "ThrottleStop", "System tools", 75, IgnoreInTotals: true),
        new(MatchKind.ProcessNameContains, "HWiNFO", "System tools", 75, IgnoreInTotals: true),

        // System / shell (family preset: explorer time → Documents so homework/file work groups together)
        new(MatchKind.ProcessNameEquals, "explorer", "Documents", 80),
        new(MatchKind.ProcessNameContains, "Taskmgr", "System tools", 70),

        // Lock screen / idle PC — ignore in totals so it does not look like active “Ignored” study time.
        new(MatchKind.WindowTitleContains, "Windows Default Lock Screen", "Ignored", 10, IgnoreInTotals: true),

        // Dev / capture — low priority so IDE and browser rules win when they apply.
        new(MatchKind.ProcessNameContains, "GitHubDesktop", "Development", 10),
        // User-reported: Adobe Bridge or similar tooling; narrow pattern — edit if this matches the wrong app on your PC.
        new(MatchKind.ProcessNameContains, "bridge", "Development", 10),
        new(MatchKind.ProcessNameContains, "obs64", "Video Recording", 10),
        new(MatchKind.WindowTitleContains, "Roblox", "Gaming", 10),

        // This app (low priority so user can override)
        new(MatchKind.ProcessNameContains, "WhatAmIDoing", "Activity tracker", 40),
    ];
}

public readonly record struct BuiltInRule(
    MatchKind MatchKind,
    string Pattern,
    string Category,
    int Priority,
    bool IgnoreInTotals = false,
    int? IdleThresholdMsOverride = null,
    int? ThinkingExtraMsOverride = null);
