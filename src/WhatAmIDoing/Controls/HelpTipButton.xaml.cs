using System.Windows;
using WhatAmIDoing.Services;

namespace WhatAmIDoing.Controls;

public partial class HelpTipButton : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TipTextProperty = DependencyProperty.Register(
        nameof(TipText),
        typeof(string),
        typeof(HelpTipButton),
        new PropertyMetadata(string.Empty));

    public string TipText
    {
        get => (string)GetValue(TipTextProperty);
        set => SetValue(TipTextProperty, value);
    }

    public HelpTipButton()
    {
        // StaticResource in this control's XAML resolves against *this* dictionary, not the
        // parent window — merge the theme here before InitializeComponent or Settings/Rules fail to open.
        DashboardUi.EnsureTheme(this);
        InitializeComponent();
    }

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TipText))
            return;
        System.Windows.MessageBox.Show(
            TipText,
            "Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
