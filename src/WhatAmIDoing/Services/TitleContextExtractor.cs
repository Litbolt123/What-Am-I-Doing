using WhatAmIDoing.Models;

namespace WhatAmIDoing.Services;

public readonly record struct TitleContext(ContextKind Kind, string Value);

/// <summary>
/// Heuristic enrichment of foreground window titles into structured signals:
/// site / page for browsers, video title for YouTube, project folder for IDEs.
/// Pure function for testability; does not touch the OS or DB.
/// </summary>
public static class TitleContextExtractor
{
    private static readonly string[] BrowserProcesses =
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc",
        "iexplore", "librewolf", "waterfox", "thorium", "comet",
    };

    private static readonly string[] IdeProcesses =
    {
        "Cursor", "Code", "Code - Insiders", "devenv", "rider", "rider64",
        "WebStorm", "PyCharm", "PyCharm64", "idea", "idea64", "CLion", "CLion64",
        "GoLand", "RubyMine", "PhpStorm",
    };

    /// <summary>
    /// Browsers and IDEs put a separator between the page/project title and the app name.
    /// We match these in priority order; the first that splits cleanly wins.
    /// </summary>
    private static readonly string[] TitleSeparators = { " — ", " - ", " – ", " | " };

    public static TitleContext Extract(string processName, string windowTitle)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
            return new TitleContext(ContextKind.None, "");

        if (IsBrowser(processName))
            return ExtractBrowser(windowTitle);

        if (IsIde(processName))
            return ExtractProject(processName, windowTitle);

        return new TitleContext(ContextKind.None, "");
    }

    private static bool IsBrowser(string processName) =>
        BrowserProcesses.Any(p => processName.Equals(p, StringComparison.OrdinalIgnoreCase));

    private static bool IsIde(string processName) =>
        IdeProcesses.Any(p => processName.Equals(p, StringComparison.OrdinalIgnoreCase));

    private static TitleContext ExtractBrowser(string windowTitle)
    {
        // YouTube: usually "Video title - YouTube". Some browsers append another segment on the right
        // ("Video title - YouTube - Arc", "… - Comet"), which made SplitOnceFromEnd treat "Comet" as
        // the app side — we never saw YouTube as the right chunk and lost video titles for Highlights.
        if (TryExtractYouTubeVideoTitle(windowTitle, out var youtubeVideo))
            return new TitleContext(ContextKind.YouTube, youtubeVideo);

        var (left, right) = SplitOnceFromEnd(windowTitle);

        // Fallback: classic two-part title ending in YouTube only.
        if (right is not null && right.Trim().Equals("YouTube", StringComparison.OrdinalIgnoreCase))
        {
            var video = StripYouTubeNotificationCount(left).Trim();
            if (video.Length > 0)
                return new TitleContext(ContextKind.YouTube, video);
        }

        // Otherwise return the page title (left side) as a "site / page" signal.
        // If we couldn't split, fall back to the entire title.
        var page = (left ?? windowTitle).Trim();
        return page.Length > 0
            ? new TitleContext(ContextKind.Site, page)
            : new TitleContext(ContextKind.None, "");
    }

    /// <summary>
    /// Finds the last "<sep>YouTube" in the title where YouTube is followed by end-of-string or by
    /// another separator (optional browser name after YouTube).
    /// </summary>
    private static bool TryExtractYouTubeVideoTitle(string windowTitle, out string videoTitle)
    {
        videoTitle = "";
        foreach (var sep in TitleSeparators)
        {
            var needle = sep + "YouTube";
            var idx = windowTitle.LastIndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx <= 0)
                continue;

            var after = windowTitle[(idx + needle.Length)..].TrimStart();
            if (after.Length > 0)
            {
                var tailOk = false;
                foreach (var s in TitleSeparators)
                {
                    if (after.StartsWith(s, StringComparison.Ordinal))
                    {
                        tailOk = true;
                        break;
                    }
                }

                if (!tailOk)
                    continue;
            }

            var before = windowTitle[..idx];
            var video = StripYouTubeNotificationCount(before).Trim();
            if (video.Length == 0)
                continue;

            videoTitle = video;
            return true;
        }

        return false;
    }

    private static TitleContext ExtractProject(string processName, string windowTitle)
    {
        // Cursor / VS Code: "filename.cs - ProjectFolder - Cursor"   or   "ProjectFolder - Cursor"
        // Rider:           "ProjectFolder [path] - branch - filename.cs - JetBrains Rider"
        // We aim for the project / folder segment.
        var parts = SplitOnSeparators(windowTitle);
        if (parts.Count == 0)
            return new TitleContext(ContextKind.None, "");

        // Drop the trailing app-name segment when it looks like the IDE name.
        var last = parts[^1];
        if (LooksLikeAppName(last, processName))
            parts.RemoveAt(parts.Count - 1);

        if (parts.Count == 0)
            return new TitleContext(ContextKind.None, "");

        // VS Code / Cursor convention: the project folder is the last remaining segment.
        // Rider convention: the first segment is the solution name, but it's often followed by [path].
        // Picking the last segment works for both because Rider's last meaningful segment before
        // the IDE name is the file, but we trim trailing file-path-y parts below.
        var candidate = parts[^1].Trim();

        // Strip "[path]" annotations Rider adds.
        var bracket = candidate.IndexOf('[');
        if (bracket > 0)
            candidate = candidate[..bracket].Trim();

        if (candidate.Length == 0 || LooksLikeFile(candidate))
        {
            // Try the second-to-last segment.
            if (parts.Count >= 2)
                candidate = parts[^2].Trim();
        }

        return candidate.Length > 0
            ? new TitleContext(ContextKind.Project, candidate)
            : new TitleContext(ContextKind.None, "");
    }

    private static bool LooksLikeAppName(string segment, string processName)
    {
        var s = segment.Trim();
        if (s.Length == 0)
            return false;
        if (s.Contains(processName, StringComparison.OrdinalIgnoreCase))
            return true;
        return s.Equals("Cursor", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("Visual Studio", StringComparison.OrdinalIgnoreCase)
            || s.Contains("JetBrains", StringComparison.OrdinalIgnoreCase)
            || s.Contains("IntelliJ", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Rider", StringComparison.OrdinalIgnoreCase)
            || s.Equals("WebStorm", StringComparison.OrdinalIgnoreCase)
            || s.Equals("PyCharm", StringComparison.OrdinalIgnoreCase)
            || s.Equals("PhpStorm", StringComparison.OrdinalIgnoreCase)
            || s.Equals("CLion", StringComparison.OrdinalIgnoreCase)
            || s.Equals("GoLand", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeFile(string segment)
    {
        // ".cs", ".ts", ".py", "."—two-or-three char extension
        var dot = segment.LastIndexOf('.');
        if (dot <= 0 || dot >= segment.Length - 1)
            return false;
        var ext = segment[(dot + 1)..];
        return ext.Length is >= 1 and <= 6 && ext.All(char.IsLetterOrDigit);
    }

    private static (string? Left, string? Right) SplitOnceFromEnd(string title)
    {
        foreach (var sep in TitleSeparators)
        {
            var idx = title.LastIndexOf(sep, StringComparison.Ordinal);
            if (idx <= 0 || idx + sep.Length >= title.Length)
                continue;
            return (title[..idx], title[(idx + sep.Length)..]);
        }

        return (null, null);
    }

    private static List<string> SplitOnSeparators(string title)
    {
        // Split greedily by the strongest separator that actually appears in the title.
        foreach (var sep in TitleSeparators)
        {
            if (title.Contains(sep, StringComparison.Ordinal))
                return title.Split(sep, StringSplitOptions.None).ToList();
        }

        return new List<string> { title };
    }

    private static string StripYouTubeNotificationCount(string? title)
    {
        if (string.IsNullOrEmpty(title))
            return "";
        // YouTube prefixes the tab title with notification counts: "(3) Video title".
        var t = title.TrimStart();
        if (t.Length > 1 && t[0] == '(')
        {
            var close = t.IndexOf(')');
            if (close > 0 && close < 8)
            {
                var inside = t[1..close];
                if (inside.All(c => char.IsDigit(c) || c == '+'))
                    return t[(close + 1)..].TrimStart();
            }
        }

        return t;
    }
}
