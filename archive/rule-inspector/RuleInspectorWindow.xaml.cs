using System.Text;
using System.Windows;
using System.Windows.Threading;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class RuleInspectorWindow
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };

    public RuleInspectorWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AccessibilityUi.Apply(this, App.Db);
            _timer.Tick += (_, _) => RefreshBody();
            _timer.Start();
            RefreshBody();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void Refresh_OnClick(object sender, RoutedEventArgs e) => RefreshBody();

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void RefreshBody()
    {
        var snap = ActivityClassificationSnapshot.Compute(App.Db);
        var sb = new StringBuilder();

        sb.AppendLine($"Process: {snap.ProcessName}");
        sb.AppendLine($"Window title: {snap.WindowTitle}");
        sb.AppendLine($"Context: {snap.Context.Kind} — {snap.Context.Value}");
        sb.AppendLine($"Input idle: {snap.InputIdleMs:N0} ms");
        sb.AppendLine($"Effective idle threshold: {snap.EffectiveIdleMs:N0} ms");
        sb.AppendLine($"Effective thinking grace: {snap.EffectiveThinkingMs:N0} ms");
        sb.AppendLine($"Activity state: {snap.State}");
        sb.AppendLine();

        if (snap.MatchedRule is { } r)
        {
            sb.AppendLine("First matching rule (priority order):");
            sb.AppendLine($"  Rule #{r.Id}  priority {r.Priority}");
            sb.AppendLine($"  {r.MatchKind}: {r.Pattern}");
            sb.AppendLine($"  Category: {r.Category}  ignoreInTotals: {r.IgnoreInTotals}");
            if (r.IdleThresholdMsOverride is int io)
                sb.AppendLine($"  Idle override: {io} ms");
            if (r.ThinkingExtraMsOverride is int to)
                sb.AppendLine($"  Thinking override: {to} ms");
        }
        else
        {
            sb.AppendLine("No rule matched before Uncategorized.");
        }

        sb.AppendLine();
        sb.AppendLine($"Resolved category label: {snap.Category}");
        sb.AppendLine($"Excluded from totals (ignored): {snap.Ignored}");

        BodyText.Text = sb.ToString();
    }
}
