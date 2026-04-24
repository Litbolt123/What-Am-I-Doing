using System.Globalization;
using System.IO;
using System.Text;
using WhatAmIDoing.Data;
using WhatAmIDoing.Services;

namespace WhatAmIDoing.Export;

public static class HtmlReportExporter
{
    public static void WriteFile(string path, AggregatedReport report, string title,
        IReadOnlyList<ScreenEventRow>? screenEvents = null,
        bool includeEvidenceImages = false)
    {
        var html = BuildHtml(report, title, screenEvents, includeEvidenceImages, Path.GetDirectoryName(path));
        File.WriteAllText(path, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static string BuildHtml(AggregatedReport report, string title) =>
        BuildHtml(report, title, null, false, null);

    public static string BuildHtml(AggregatedReport report, string title,
        IReadOnlyList<ScreenEventRow>? screenEvents,
        bool includeEvidenceImages,
        string? evidenceTargetDir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{Escape(title)}</title>");
        sb.AppendLine("""
            <style>
              body { font-family: Segoe UI, system-ui, sans-serif; background:#f4f6f9; color:#1a1a1a; margin:0; padding:24px; }
              .card { background:#fff; border-radius:12px; padding:20px 24px; max-width:880px; margin:0 auto 16px; box-shadow:0 1px 3px rgba(0,0,0,.08); }
              h1 { font-size:1.35rem; margin:0 0 8px; }
              .meta { color:#5c6370; font-size:0.9rem; margin-bottom:16px; }
              table { width:100%; border-collapse:collapse; font-size:0.95rem; }
              th, td { text-align:left; padding:8px 10px; border-bottom:1px solid #e8eaef; }
              th { color:#3d4450; font-weight:600; }
              .num { text-align:right; font-variant-numeric: tabular-nums; }
              .note { font-size:0.85rem; color:#5c6370; margin-top:16px; line-height:1.45; }
            </style>
            </head><body>
            """);

        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine($"<h1>{Escape(title)}</h1>");
        sb.AppendLine(
            $"<div class=\"meta\">Range (local): {Escape(FormatLocal(report.RangeStartLocal))} → {Escape(FormatLocal(report.RangeEndLocal))} · Sample every {report.SampleIntervalSeconds}s · {report.TotalSamples} samples</div>");

        sb.AppendLine("<h2 style=\"font-size:1.05rem;margin:16px 0 8px;\">Summary</h2>");
        sb.AppendLine("<table><thead><tr><th>Metric</th><th class=\"num\">Time</th></tr></thead><tbody>");
        sb.AppendLine($"<tr><td>Active (typing / clicking)</td><td class=\"num\">{FormatDuration(report.SecondsActiveFocused)}</td></tr>");
        if (report.SecondsThinking > 0)
            sb.AppendLine($"<tr><td>Thinking (app in foreground, not typing)</td><td class=\"num\">{FormatDuration(report.SecondsThinking)}</td></tr>");
        sb.AppendLine($"<tr><td>Idle / AFK (no input past threshold)</td><td class=\"num\">{FormatDuration(report.SecondsIdle)}</td></tr>");
        if (report.SecondsIgnored > 0)
            sb.AppendLine($"<tr><td>Ignored (rules)</td><td class=\"num\">{FormatDuration(report.SecondsIgnored)}</td></tr>");
        sb.AppendLine("</tbody></table>");

        AppendWebsitesAtAGlance(sb, report);

        if (report.DayCount > 1)
        {
            sb.AppendLine("<h2 style=\"font-size:1.05rem;margin:20px 0 8px;\">Activity by day</h2>");
            sb.AppendLine(BuildLegendHtml(report));
            sb.AppendLine(ChartRenderer.BuildDailyStackedBarsSvg(report));
        }
        else
        {
            sb.AppendLine("<h2 style=\"font-size:1.05rem;margin:20px 0 8px;\">Hourly activity</h2>");
            sb.AppendLine(BuildLegendHtml(report));
            sb.AppendLine(ChartRenderer.BuildHourlyTimelineSvg(report));
        }

        sb.AppendLine("<h2 style=\"font-size:1.05rem;margin:20px 0 8px;\">By category</h2>");
        sb.AppendLine("<table><thead><tr><th>Category</th><th class=\"num\">Active</th><th class=\"num\">Thinking</th><th class=\"num\">Total</th></tr></thead><tbody>");
        foreach (var kv in report.SecondsByCategory.OrderByDescending(k => k.Value))
        {
            var thinking = report.ThinkingSecondsByCategory.TryGetValue(kv.Key, out var th) ? th : 0;
            var active = Math.Max(0, kv.Value - thinking);
            var isIdle = string.Equals(kv.Key, CategoryClassifier.IdleCategory, StringComparison.OrdinalIgnoreCase);
            sb.AppendLine(
                $"<tr><td>{Escape(kv.Key)}</td>" +
                $"<td class=\"num\">{(isIdle ? "—" : FormatDuration(active))}</td>" +
                $"<td class=\"num\">{(thinking > 0 ? FormatDuration(thinking) : "—")}</td>" +
                $"<td class=\"num\">{FormatDuration(kv.Value)}</td></tr>");
        }

        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<h2 style=\"font-size:1.05rem;margin:20px 0 8px;\">By app (engaged time)</h2>");
        sb.AppendLine("<table><thead><tr><th>Process</th><th class=\"num\">Active</th><th class=\"num\">Thinking</th><th class=\"num\">Total</th></tr></thead><tbody>");
        var procKeys = new HashSet<string>(report.ActiveSecondsByProcess.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in report.ThinkingSecondsByProcess.Keys)
            procKeys.Add(k);
        var procRows = procKeys
            .Select(k =>
            {
                var a = report.ActiveSecondsByProcess.TryGetValue(k, out var av) ? av : 0;
                var t = report.ThinkingSecondsByProcess.TryGetValue(k, out var tv) ? tv : 0;
                return (Proc: k, Active: a, Thinking: t, Total: a + t);
            })
            .OrderByDescending(r => r.Total)
            .Take(24)
            .ToList();
        foreach (var r in procRows)
            sb.AppendLine(
                $"<tr><td>{Escape(r.Proc)}</td>" +
                $"<td class=\"num\">{FormatDuration(r.Active)}</td>" +
                $"<td class=\"num\">{(r.Thinking > 0 ? FormatDuration(r.Thinking) : "—")}</td>" +
                $"<td class=\"num\">{FormatDuration(r.Total)}</td></tr>");
        if (procRows.Count == 0)
            sb.AppendLine("<tr><td colspan=\"4\">No active samples in this range (or only idle/ignored).</td></tr>");
        sb.AppendLine("</tbody></table>");

        AppendTopSection(sb, "Top YouTube videos / streams", "Video / channel", report.ActiveSecondsByYouTube);
        AppendTopSection(sb, "Top sites / pages", "Page title", report.ActiveSecondsBySite);
        AppendTopSection(sb, "Top projects (IDE)", "Project / folder", report.ActiveSecondsByProject);

        AppendScreenSection(sb, screenEvents, includeEvidenceImages, evidenceTargetDir);

        if (report.CompanionAudioSeconds.Count > 0 || report.VoiceWhileGamingSeconds > 0)
        {
            sb.AppendLine("<h2 style=\"font-size:1.05rem;margin:20px 0 8px;\">Voice / call activity</h2>");
            sb.AppendLine("<table><thead><tr><th>App with active mic / speaker session</th><th class=\"num\">Time</th></tr></thead><tbody>");
            foreach (var kv in report.CompanionAudioSeconds.OrderByDescending(k => k.Value).Take(12))
                sb.AppendLine($"<tr><td>{Escape(kv.Key)}</td><td class=\"num\">{FormatDuration(kv.Value)}</td></tr>");
            if (report.VoiceWhileGamingSeconds > 0)
                sb.AppendLine($"<tr><td><em>…of which while a game was in the foreground</em></td><td class=\"num\">{FormatDuration(report.VoiceWhileGamingSeconds)}</td></tr>");
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("<p class=\"note\" style=\"margin-top:6px\">Voice / call time is measured by detecting an active microphone or audio render session. It indicates voice / mic activity, not membership in a specific Discord channel. Companion time is reported separately from the active total above so it is not double-counted.</p>");
        }

        sb.AppendLine("<p class=\"note\">");
        sb.AppendLine("What Am I Doing records the foreground window on a fixed interval and classifies time using your rules. ");
        sb.AppendLine("<strong>Active</strong> means keyboard or mouse activity within the idle threshold. ");
        sb.AppendLine("<strong>Thinking</strong> means the app was still in the foreground past the idle threshold, ");
        sb.AppendLine("but within the \u201Cthinking grace\u201D window (for reading code, watching a long paragraph, etc.). ");
        sb.AppendLine("<strong>Idle / AFK</strong> means no input for longer than that. IDE-style rules use a longer idle threshold by default. ");
        sb.AppendLine("Browser titles often include the site or video name (e.g. YouTube tab titles). This report is generated locally on your PC.");
        sb.AppendLine("</p>");
        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string BuildLegendHtml(AggregatedReport report)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"display:flex;flex-wrap:wrap;gap:8px 14px;margin:0 0 6px;font-size:0.85rem;color:#3d4450\">");
        foreach (var kv in report.SecondsByCategory.OrderByDescending(k => k.Value).Take(8))
        {
            sb.Append("<span style=\"display:inline-flex;align-items:center;gap:6px\">");
            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                "<span style=\"display:inline-block;width:10px;height:10px;border-radius:2px;background:{0}\"></span>",
                CategoryColors.Pick(kv.Key));
            sb.Append(Escape(kv.Key));
            sb.Append("</span>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendScreenSection(StringBuilder sb, IReadOnlyList<ScreenEventRow>? events,
        bool includeImages, string? evidenceTargetDir)
    {
        if (events is null || events.Count == 0)
            return;

        sb.AppendLine("<h2 style=\"font-size:1.05rem;margin:20px 0 8px;\">On-screen signals</h2>");
        sb.AppendLine($"<p class=\"note\" style=\"margin-top:0\">From {events.Count} screen capture(s) in this range.</p>");

        // Top keywords from OCR text (very simple frequency, lower-cased, filtered).
        var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var ev in events)
        {
            if (string.IsNullOrEmpty(ev.Text))
                continue;
            foreach (var raw in ev.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var w = new string(raw.Where(char.IsLetter).ToArray());
                if (w.Length < 4 || StopWords.Contains(w.ToLowerInvariant()))
                    continue;
                if (!freq.TryGetValue(w, out var c))
                    c = 0;
                freq[w] = c + 1;
            }
        }

        if (freq.Count > 0)
        {
            sb.AppendLine("<table><thead><tr><th>Most-seen on-screen word</th><th class=\"num\">Frames</th></tr></thead><tbody>");
            foreach (var kv in freq.OrderByDescending(k => k.Value).Take(20))
                sb.AppendLine($"<tr><td>{Escape(kv.Key)}</td><td class=\"num\">{kv.Value}</td></tr>");
            sb.AppendLine("</tbody></table>");
        }

        if (includeImages && evidenceTargetDir is not null)
        {
            try
            {
                var thumbsDir = Path.Combine(evidenceTargetDir, "evidence");
                Directory.CreateDirectory(thumbsDir);
                sb.AppendLine("<div style=\"display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:8px;margin-top:10px;\">");
                var sample = events.Take(12).ToList();
                foreach (var ev in sample)
                {
                    var name = $"evidence-{ev.Id}.jpg";
                    var target = Path.Combine(thumbsDir, name);
                    if (Services.ScreenCaptureService.TryDecryptToFile(ev.ImagePath, target))
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "<figure style=\"margin:0\"><img src=\"evidence/{0}\" style=\"width:100%;border-radius:6px;border:1px solid #e8eaef\" loading=\"lazy\"/><figcaption style=\"font-size:11px;color:#5c6370;margin-top:2px\">{1} — {2}</figcaption></figure>",
                            name,
                            Escape(FormatLocal(ev.TsUtc.ToLocalTime())),
                            Escape(ev.ForegroundProcess ?? ""));
                    }
                }

                sb.AppendLine("</div>");
            }
            catch
            {
                // Evidence is optional; skip silently if it fails.
            }
        }
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","for","with","that","this","from","your","have","will","what","when","where",
        "there","they","their","them","then","than","into","over","under","about","more","less",
        "just","also","because","while","until","after","before","been","being","here","very",
        "such","some","most","other","another","much","many","each","every","both","through",
        "without","within","between","across","still","does","done","like","only","even","make",
        "made","take","took","want","need","know","look","work","time","year","good","high","new",
    };

    /// <summary>
    /// Surfaces YouTube + other page titles right under the summary metrics so parents
    /// see “where on the web” without scrolling past the chart. Deeper top-15 tables follow.
    /// </summary>
    private static void AppendWebsitesAtAGlance(StringBuilder sb, AggregatedReport report)
    {
        var yt = report.ActiveSecondsByYouTube.OrderByDescending(k => k.Value).Take(6).ToList();
        var sites = report.ActiveSecondsBySite.OrderByDescending(k => k.Value).Take(8).ToList();
        if (yt.Count == 0 && sites.Count == 0)
        {
            sb.AppendLine(
                "<p class=\"note\" style=\"margin:12px 0 0\">" +
                "<strong>Websites &amp; web content:</strong> no tab-title data in this range. " +
                "A supported browser with a visible page or video title, and time that is not fully idle, is required. " +
                "The detailed site lists below will fill in when that data exists.</p>");
            return;
        }

        sb.AppendLine("<h2 style=\"font-size:1.05rem;margin:18px 0 6px;\">Websites &amp; web content (at a glance)</h2>");
        sb.AppendLine(
            "<p class=\"note\" style=\"margin:0 0 8px\">Engaged time (active + thinking) per tab title or video name. " +
            "YouTube is listed separately from other pages because we parse it from the tab title.</p>");

        if (yt.Count > 0)
        {
            sb.AppendLine("<h3 style=\"font-size:0.95rem;margin:10px 0 4px;color:#3d4450\">YouTube (video in tab)</h3>");
            sb.AppendLine("<table><thead><tr><th>Video / channel (from title)</th><th class=\"num\">Time</th></tr></thead><tbody>");
            foreach (var kv in yt)
                sb.AppendLine($"<tr><td>{Escape(TruncateForHtmlAtAGlance(kv.Key))}</td><td class=\"num\">{FormatDuration(kv.Value)}</td></tr>");
            sb.AppendLine("</tbody></table>");
        }

        if (sites.Count > 0)
        {
            sb.AppendLine("<h3 style=\"font-size:0.95rem;margin:14px 0 4px;color:#3d4450\">Other pages &amp; sites</h3>");
            sb.AppendLine("<table><thead><tr><th>Page title (from tab)</th><th class=\"num\">Time</th></tr></thead><tbody>");
            foreach (var kv in sites)
                sb.AppendLine($"<tr><td>{Escape(TruncateForHtmlAtAGlance(kv.Key))}</td><td class=\"num\">{FormatDuration(kv.Value)}</td></tr>");
            sb.AppendLine("</tbody></table>");
        }
    }

    private static string TruncateForHtmlAtAGlance(string? s, int max = 72)
    {
        if (string.IsNullOrWhiteSpace(s))
            return "(no title)";
        s = s.Trim();
        if (s.Length <= max)
            return s;
        return s[..(max - 1)] + "…";
    }

    private static void AppendTopSection(StringBuilder sb, string heading, string columnLabel,
        IReadOnlyDictionary<string, int> bucket)
    {
        if (bucket.Count == 0)
            return;

        sb.AppendLine($"<h2 style=\"font-size:1.05rem;margin:20px 0 8px;\">{Escape(heading)}</h2>");
        sb.AppendLine($"<table><thead><tr><th>{Escape(columnLabel)}</th><th class=\"num\">Time</th></tr></thead><tbody>");
        foreach (var kv in bucket.OrderByDescending(k => k.Value).Take(15))
            sb.AppendLine($"<tr><td>{Escape(kv.Key)}</td><td class=\"num\">{FormatDuration(kv.Value)}</td></tr>");
        sb.AppendLine("</tbody></table>");
    }

    private static string FormatLocal(DateTime dt) =>
        dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 0)
            totalSeconds = 0;
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return "";
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
