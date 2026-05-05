using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace WhatAmIDoing.Services;

/// <summary>
/// Best-effort check against GitHub Releases (no auto-install; opens the browser if newer).
/// </summary>
public static class UpdateCheckService
{
    /// <summary>Owner/repo for the public GitHub project (releases/latest API).</summary>
    public const string GitHubRepo = "Litbolt123/What-Am-I-Doing";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WhatAmIDoing-UpdateCheck/1.0");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        return c;
    }

    public static string ReleasesPageUrl => $"https://github.com/{GitHubRepo}/releases";

    public static Version CurrentAssemblyVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public static async Task<UpdateCheckResult> CheckLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
        try
        {
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            // GitHub returns 404 when the repo has no Releases yet — not a network failure.
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(true, null, ReleasesPageUrl, null)
                {
                    NoPublishedReleases = true,
                    IsNewerThanCurrent = false,
                };
            }

            if (!resp.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(false, null, null,
                    $"GitHub returned {(int)resp.StatusCode} ({resp.ReasonPhrase}).");
            }

            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("tag_name", out var tagEl))
                return new UpdateCheckResult(false, null, null, "Release response had no tag.");
            var tag = tagEl.GetString()?.Trim() ?? "";
            if (tag.StartsWith('v') || tag.StartsWith('V'))
                tag = tag[1..];
            if (!Version.TryParse(tag, out var remote))
                return new UpdateCheckResult(false, null, null, "Could not parse release version.");

            var cur = CurrentAssemblyVersion;
            var newer = remote > cur;
            return new UpdateCheckResult(true, remote.ToString(3), ReleasesPageUrl, null) { IsNewerThanCurrent = newer };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, null, null, ex.Message);
        }
    }

    public static void OpenReleasesInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ReleasesPageUrl,
                UseShellExecute = true,
            });
        }
        catch
        {
            /* ignore */
        }
    }
}

public sealed record UpdateCheckResult(
    bool Success,
    string? LatestVersion,
    string? ReleasePageUrl,
    string? ErrorMessage)
{
    public bool IsNewerThanCurrent { get; init; }

    /// <summary>True when the API returned 404 — usually no published GitHub Release yet.</summary>
    public bool NoPublishedReleases { get; init; }
}
