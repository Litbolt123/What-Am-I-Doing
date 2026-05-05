using System.Diagnostics;
using System.Reflection;
using System.Windows;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class AboutWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {GetDisplayVersion()}";
        DataPathBox.Text = AppPaths.DataDirectory;
        UpdateStatusText.Text =
            "Updates: this build checks GitHub Releases for a newer tag. There is no in-app installer — " +
            "download the latest setup from Releases when an update is available (see Updating the app in the repo docs). " +
            "If nothing is published yet, “Check for updates” will say so instead of an error.";
    }

    /// <summary>Prefer MSBuild <c>Version</c> (informational); fall back to full assembly version so 1.0.2.5 is not shown as 1.0.2.</summary>
    private static string GetDisplayVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var i = info.IndexOf('+');
            var s = (i >= 0 ? info[..i] : info).Trim();
            if (s.Length > 0 && char.IsDigit(s[0]))
                return s;
        }

        return asm.GetName().Version?.ToString(4) ?? "0.0.0";
    }

    private async void CheckUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = "Checking GitHub Releases…";
        var r = await UpdateCheckService.CheckLatestReleaseAsync();
        if (r.NoPublishedReleases)
        {
            UpdateStatusText.Text =
                "No published release on GitHub yet (the Releases page may be empty until the first installer is uploaded). " +
                "Use “Open Releases page” below to check in your browser.";
            return;
        }

        if (!r.Success)
        {
            UpdateStatusText.Text = "Could not check for updates: " + (r.ErrorMessage ?? "unknown error");
            return;
        }

        var cur = GetDisplayVersion();
        if (r.IsNewerThanCurrent)
        {
            UpdateStatusText.Text =
                $"A newer release may be available (latest tag on GitHub: {r.LatestVersion}, this app: {cur}). " +
                "Open the Releases page to download the installer.";
            var open = System.Windows.MessageBox.Show(
                $"GitHub shows release version {r.LatestVersion}. You are on {cur}.\n\nOpen the Releases page in your browser?",
                "Check for updates",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (open == MessageBoxResult.Yes)
                UpdateCheckService.OpenReleasesInBrowser();
        }
        else
        {
            UpdateStatusText.Text =
                $"You appear to be up to date with the latest GitHub release tag ({r.LatestVersion}) for this check.";
        }
    }

    private void OpenReleases_OnClick(object sender, RoutedEventArgs e) =>
        UpdateCheckService.OpenReleasesInBrowser();

    private void OpenFolder_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.DataDirectory,
                UseShellExecute = true,
            });
        }
        catch
        {
            /* shell not available; ignore */
        }
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
