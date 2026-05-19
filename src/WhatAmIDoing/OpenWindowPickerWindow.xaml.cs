using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class OpenWindowPickerWindow : Window
{
    public ForegroundWindowInfo? Selected { get; private set; }

    public OpenWindowPickerWindow()
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        WindowsList.ItemsSource = ForegroundWindowHelper.EnumerateVisibleWindows();
        if (WindowsList.Items.Count > 0)
            WindowsList.SelectedIndex = 0;
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e) => CommitSelection();

    private void WindowsList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => CommitSelection();

    private void CommitSelection()
    {
        if (WindowsList.SelectedItem is not ForegroundWindowInfo info)
        {
            System.Windows.MessageBox.Show(this, "Select a window from the list.", "Pick a window",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Selected = info;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
