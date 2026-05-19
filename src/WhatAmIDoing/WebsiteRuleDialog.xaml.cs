using System.Windows;
using WhatAmIDoing.Models;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class WebsiteRuleDialog
{
    public WebsiteRuleDialog(IReadOnlyList<string> categorySuggestions)
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        CategoryBox.ItemsSource = categorySuggestions;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        var raw = PatternBox.Text.Trim();
        if (raw.Length == 0)
        {
            System.Windows.MessageBox.Show("Enter a website name or URL.", "Add website rule",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Editable ComboBox may not commit Text to the DP until LostFocus; OK runs first.
        var category = EditableComboHelper.GetText(CategoryBox);
        if (category.Length == 0)
        {
            System.Windows.MessageBox.Show("Enter a category label.", "Add website rule",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pattern = NormalizeWebsitePattern(raw);
        if (pattern.Length == 0)
            pattern = raw;

        try
        {
            App.Db.AddUserRule(
                MatchKind.ContextSiteContains,
                pattern,
                category,
                RulePriorityGuide.RecommendedUserSiteRulePriority,
                ignoreInTotals: false,
                idleThresholdMsOverride: null,
                thinkingExtraMsOverride: null,
                notes: null);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "Could not save the rule: " + ex.Message,
                "Add website rule",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
    }

    /// <summary>
    /// Turn pasted URLs into a stable substring (usually hostname) for title matching.
    /// </summary>
    internal static string NormalizeWebsitePattern(string input)
    {
        var s = input.Trim();
        if (s.Length == 0)
            return "";

        var tryUri = s;
        if (!tryUri.Contains("://", StringComparison.Ordinal))
            tryUri = "https://" + tryUri;

        if (Uri.TryCreate(tryUri, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            var host = uri.IdnHost.Length > 0 ? uri.IdnHost : uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && host.Length > 4)
                host = host[4..];
            return host;
        }

        return s;
    }
}
