using WhatAmIDoing.Models;
using WhatAmIDoing.Services;

namespace WhatAmIDoing.Data;

/// <summary>
/// Suggested categories for common apps. An empty rules table gets every row; an existing
/// database only gains rows whose kind+pattern is not already stored (see top-up in
/// <c>AppDatabase</c>). Higher priority runs first.
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

        // Above browser process rules (190) and YouTube (260) so a Bible tab is Faith, not "Web browser".
        new(MatchKind.WindowTitleContains, "Bible", "Faith", 270),
        new(MatchKind.WindowTitleContains, "bible.com", "Faith", 270),
        new(MatchKind.WindowTitleContains, "YouVersion", "Faith", 270),
        new(MatchKind.WindowTitleContains, "ESV", "Faith", 270),

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
        // More specific than the older "RobloxStudio" → Gaming / creative row (164), so Studio wins.
        new(MatchKind.ProcessNameContains, "RobloxStudioBeta", "Development", 215, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "WindowsTerminal", "Development", 210, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "mintty", "Development", 210, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "node", "Development", 205, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "python", "Development", 205, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "Compil32", "Development", 100),
        new(MatchKind.ProcessNameContains, "DbgX.Shell", "Development", 100),
        new(MatchKind.ProcessNameContains, "WinDbg", "Development", 100),
        new(MatchKind.ProcessNameContains, "FileZilla", "Development", 100),
        new(MatchKind.ProcessNameContains, "ollama", "Development", 100),
        new(MatchKind.ProcessNameContains, "blender", "Creative", 200, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "Photoshop", "Creative", 200, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "Resolve", "Creative", 200, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "obs64", "Creative", 200, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "Blockbench", "Creative", 200, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "Clipchamp", "Creative", 200, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "PhotoHub", "Creative", 200, IdleThresholdMsOverride: 150_000),
        new(MatchKind.ProcessNameContains, "Voicemod", "Creative", 200),

        // Browsers (process name)
        new(MatchKind.ProcessNameContains, "chrome", "Web browser", RulePriorityGuide.BuiltInBrowserProcessRulePriority),
        new(MatchKind.ProcessNameContains, "msedge", "Web browser", RulePriorityGuide.BuiltInBrowserProcessRulePriority),
        new(MatchKind.ProcessNameContains, "firefox", "Web browser", RulePriorityGuide.BuiltInBrowserProcessRulePriority),
        new(MatchKind.ProcessNameContains, "brave", "Web browser", RulePriorityGuide.BuiltInBrowserProcessRulePriority),
        new(MatchKind.ProcessNameContains, "opera", "Web browser", RulePriorityGuide.BuiltInBrowserProcessRulePriority),
        new(MatchKind.ProcessNameContains, "vivaldi", "Web browser", RulePriorityGuide.BuiltInBrowserProcessRulePriority),
        new(MatchKind.ProcessNameContains, "comet", "Web browser", RulePriorityGuide.BuiltInBrowserProcessRulePriority),

        // Communication
        new(MatchKind.ProcessNameContains, "Discord", "Communication", 180),
        new(MatchKind.ProcessNameContains, "Slack", "Communication", 180),
        new(MatchKind.ProcessNameContains, "Teams", "Communication", 175),
        new(MatchKind.ProcessNameContains, "Zoom", "Communication", 175),
        new(MatchKind.ProcessNameContains, "OUTLOOK", "Communication", 175),
        new(MatchKind.ProcessNameEquals, "olk", "Communication", 175),
        new(MatchKind.ProcessNameContains, "PhoneExperienceHost", "Communication", 175),
        new(MatchKind.ProcessNameContains, "spotify", "Music & audio", 170),
        new(MatchKind.ProcessNameContains, "MusicBee", "Music & audio", 170),
        new(MatchKind.ProcessNameContains, "iTunes", "Music & audio", 170),
        new(MatchKind.ProcessNameContains, "LocalMusicHub", "Music & audio", 170),
        new(MatchKind.ProcessNameContains, "LocalVideoHub", "Streaming video", 170),

        // Gaming — process names (TopUp adds if missing). Low-priority title rule below catches
        // "Roblox" in the window title when the process row does not match.
        new(MatchKind.ProcessNameContains, "RobloxPlayerBeta", "Gaming", 165),
        new(MatchKind.ProcessNameContains, "RobloxStudio", "Gaming / creative", 164),
        new(MatchKind.ProcessNameContains, "PrismLauncher", "Gaming", 163),
        new(MatchKind.ProcessNameContains, "HytaleClient", "Gaming", 162),
        new(MatchKind.ProcessNameContains, "Hytale", "Gaming", 161),
        new(MatchKind.ProcessNameContains, "Minecraft.Windows", "Gaming", 10),
        new(MatchKind.ProcessNameContains, "Lunar Client", "Gaming", 161),

        // Gaming storefronts / common launchers
        new(MatchKind.ProcessNameContains, "steam", "Gaming", 160),
        new(MatchKind.ProcessNameContains, "EpicGamesLauncher", "Gaming", 160),
        new(MatchKind.ProcessNameContains, "Battle.net", "Gaming", 160),
        // Exact name so this does not also match Minecraft.Windows (that row stays at priority 10).
        new(MatchKind.ProcessNameEquals, "Minecraft", "Gaming", 160),
        new(MatchKind.ProcessNameContains, "CurseForge", "Gaming", 160),
        new(MatchKind.ProcessNameContains, "Modrinth", "Gaming", 160),
        new(MatchKind.ProcessNameContains, "GeForceNOW", "Gaming", 160),
        new(MatchKind.ProcessNameContains, "azahar", "Gaming", 161),
        new(MatchKind.ProcessNameContains, "PTCGLive", "Gaming", 161),
        new(MatchKind.ProcessNameContains, "wotblitz", "Gaming", 161),
        new(MatchKind.ProcessNameContains, "ROUNDS", "Gaming", 161),

        // Productivity
        new(MatchKind.ProcessNameContains, "WINWORD", "Documents", 120),
        new(MatchKind.ProcessNameContains, "EXCEL", "Documents", 120),
        new(MatchKind.ProcessNameContains, "POWERPNT", "Documents", 120),
        new(MatchKind.ProcessNameContains, "OneNote", "Documents", 115),

        // Notes / quick productivity. Sticky Notes renders via ApplicationFrameHost on Win11,
        // so match on the window title instead of the process name. (Family preset: count as Documents.)
        new(MatchKind.WindowTitleContains, "Sticky Notes", "Documents", 150),
        new(MatchKind.ProcessNameContains, "Notepad", "Notes", 140),
        new(MatchKind.ProcessNameContains, "Obsidian", "Notes", 140),
        new(MatchKind.ProcessNameContains, "Notion", "Notes", 140),
        new(MatchKind.ProcessNameContains, "Todo", "Notes", 140),

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
        new(MatchKind.WindowTitleContains, "Roblox", "Gaming", 10),
        // Priority 10 so a browser process rule (190) still wins; javaw titled Minecraft does not.
        new(MatchKind.WindowTitleContains, "Minecraft", "Gaming", 10),

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
