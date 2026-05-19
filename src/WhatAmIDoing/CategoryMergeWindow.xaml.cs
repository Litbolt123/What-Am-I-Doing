using System.Windows;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class CategoryMergeWindow
{
    public CategoryMergeWindow()
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        Loaded += (_, _) => AccessibilityUi.Apply(this, App.Db);
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => Close();

    private void Merge_OnClick(object sender, RoutedEventArgs e)
    {
        var from = FromBox.Text.Trim();
        var to = ToBox.Text.Trim();
        if (from.Length == 0 || to.Length == 0)
        {
            System.Windows.MessageBox.Show("Enter both labels.", "Merge categories", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show("Labels are the same.", "Merge categories", MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var n = App.Db.MergeCategoryLabels(from, to);
        System.Windows.MessageBox.Show(
            $"Updated {n} sample row(s). Rules and chart colors were adjusted where they matched “{from}”.",
            "Merge categories",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        DialogResult = true;
    }
}
