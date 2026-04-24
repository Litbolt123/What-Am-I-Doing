using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace WhatAmIDoing;

public partial class AboutWindow
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        VersionText.Text = $"Version {version}";
        DataPathBox.Text = AppPaths.DataDirectory;
    }

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
