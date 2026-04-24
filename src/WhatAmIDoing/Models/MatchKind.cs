namespace WhatAmIDoing.Models;

public enum MatchKind
{
    ProcessNameEquals = 0,
    ProcessNameContains = 1,
    WindowTitleContains = 2,

    /// <summary>Match against the extracted context value (any of site / YouTube / project).</summary>
    ContextValueContains = 3,

    /// <summary>Page or site title — only when the extractor tagged the window as a browser <see cref="ContextKind.Site"/>.</summary>
    ContextSiteContains = 4,

    /// <summary>Video title (YouTube) — only when the extractor tagged <see cref="ContextKind.YouTube"/>.</summary>
    ContextYouTubeVideoContains = 5,

    /// <summary>Project / folder name — only when the extractor tagged an IDE with <see cref="ContextKind.Project"/>.</summary>
    ContextProjectContains = 6,
}
