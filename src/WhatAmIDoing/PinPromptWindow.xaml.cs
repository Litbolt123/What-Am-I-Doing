using System.Windows;
using WhatAmIDoing.Services;

namespace WhatAmIDoing;

public partial class PinPromptWindow
{
    public PinPromptWindow(string header = "Enter the PIN to continue")
    {
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
        HeaderText.Text = header;
        Loaded += (_, _) => PinBox.Focus();
    }

    public string EnteredPin { get; private set; } = "";

    private void Ok_OnClick(object sender, RoutedEventArgs e)
    {
        EnteredPin = PinBox.Password;
        if (!PinManager.Verify(App.Db, EnteredPin))
        {
            ErrorText.Text = "Incorrect PIN.";
            PinBox.Clear();
            PinBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
