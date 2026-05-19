using System.Diagnostics;
using System.Reflection;
using System.Windows;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class AboutWindow
{
    public AboutWindow()
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        VersionText.Text = $"Version {GetDisplayVersion()}";
        DataPathBox.Text = AppPaths.DataDirectory;
        var tracker = App.Db.GetTrackerReportInfo();
        InstallIdText.Text =
            $"Install id {tracker.InstanceIdShort} · first run {tracker.FirstRunLocal} (also included in exported HTML reports).";
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
