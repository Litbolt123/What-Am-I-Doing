using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class AboutWindow
{
    private string? _pendingInstallerUrl;
    private string? _pendingInstallerVersion;

    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {GetDisplayVersion()}";
        DataPathBox.Text = AppPaths.DataDirectory;
        UpdateStatusText.Text =
            "Updates: use Check for updates below. If enabled in Settings, the app checks GitHub shortly after each start " +
            "and can show a tray tip when a newer release exists. When a setup file is attached to the release, you can download and run it from here " +
            "(the app will close first). If nothing is published yet, “Check for updates” will say so instead of an error.";
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
        InstallerUpgradePanel.Visibility = Visibility.Collapsed;
        _pendingInstallerUrl = null;
        _pendingInstallerVersion = null;

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
                $"A newer release is available on GitHub (release {r.LatestVersion}, this app {cur}). " +
                (string.IsNullOrEmpty(r.InstallerDownloadUrl)
                    ? "No installer file was attached to that release — use Open Releases page to download manually."
                    : "Use “Download and run installer…” below, or open the Releases / download link in your browser.");

            if (!string.IsNullOrEmpty(r.InstallerDownloadUrl))
            {
                _pendingInstallerUrl = r.InstallerDownloadUrl;
                _pendingInstallerVersion = r.LatestVersion ?? "";
                InstallerUpgradePanel.Visibility = Visibility.Visible;
            }
        }
        else
        {
            UpdateStatusText.Text =
                $"You appear to be up to date with the latest GitHub release tag ({r.LatestVersion}) for this check.";
        }
    }

    private async void DownloadAndRunInstaller_OnClick(object sender, RoutedEventArgs e)
    {
        var url = _pendingInstallerUrl;
        var ver = _pendingInstallerVersion;
        if (string.IsNullOrEmpty(url))
        {
            UpdateStatusText.Text = "Checking for download link…";
            var r = await UpdateCheckService.CheckLatestReleaseAsync();
            if (!r.Success || !r.IsNewerThanCurrent || string.IsNullOrEmpty(r.InstallerDownloadUrl))
            {
                System.Windows.MessageBox.Show(
                    "No installer download is available right now. Use “Open Releases page” and download " +
                    "WhatAmIDoing-Setup-….exe manually.",
                    "Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            url = r.InstallerDownloadUrl;
            ver = r.LatestVersion ?? "";
        }

        var q = System.Windows.MessageBox.Show(
            "What Am I Doing will STOP completely: it will not keep running in the system tray.\n\n" +
            "The installer will be saved under your user Temp folder, then the setup program will start.\n\n" +
            "If Windows shows SmartScreen, use “More info” / “Run anyway” only if you trust this download from GitHub.\n\n" +
            "Continue?",
            "Download and install update?",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (q != MessageBoxResult.OK)
            return;

        try
        {
            IsEnabled = false;
            DownloadRunInstallerButton.IsEnabled = false;
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            UpdateStatusText.Text = "Downloading installer (this may take a minute)…";

            var (path, err) = await UpdateCheckService.DownloadInstallerToTempAsync(url, ver).ConfigureAwait(true);
            if (path is null)
            {
                System.Windows.MessageBox.Show(err ?? "Download failed.", "Update", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Saved the installer but could not start it:\n" + ex.Message +
                    "\n\nYou can run this file yourself:\n" + path,
                    "Update",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var cur = GetDisplayVersion();
            var target = string.IsNullOrWhiteSpace(ver) ? "(unknown release)" : ver.Trim();
            try
            {
                App.Db.TryAppendLifecycleEvent("quit_update",
                    $"Stopped to install a newer version (this app was {cur}; GitHub release {target}). App fully exited (not tray) so the installer could run.");
            }
            catch
            {
                /* best-effort */
            }

            if (System.Windows.Application.Current is App app)
                app.ExitForInstallerUpgrade();
            else
                Environment.Exit(0);
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
            DownloadRunInstallerButton.IsEnabled = true;
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
