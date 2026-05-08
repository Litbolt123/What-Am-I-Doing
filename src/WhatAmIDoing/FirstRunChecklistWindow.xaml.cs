using System.Windows;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class FirstRunChecklistWindow
{
    public FirstRunChecklistWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => AccessibilityUi.Apply(this, App.Db);
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Dismiss_OnClick(object sender, RoutedEventArgs e)
    {
        App.Db.SetSetting("first_run_checklist_dismissed", "1");
        Close();
    }
}
