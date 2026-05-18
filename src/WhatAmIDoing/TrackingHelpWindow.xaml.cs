using System.Windows;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class TrackingHelpWindow
{
    public TrackingHelpWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => AccessibilityUi.Apply(this, App.Db);
    }

    public void LoadContent()
    {
        HelpText.Text = TrackingHelpText.Build(App.Db);
    }

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();
}
