namespace WhatAmIDoing.Services;

/// <summary>
/// Editable <see cref="System.Windows.Controls.ComboBox"/> often does not push text to the <c>Text</c>
/// dependency property until LostFocus; OK can run before that. Read the template text box when present.
/// </summary>
public static class EditableComboHelper
{
    public static string GetText(System.Windows.Controls.ComboBox comboBox)
    {
        if (comboBox.Template?.FindName("PART_EditableTextBox", comboBox) is System.Windows.Controls.TextBox tb)
        {
            var inner = tb.Text?.Trim() ?? "";
            if (inner.Length > 0)
                return inner;
        }

        return comboBox.Text?.Trim() ?? "";
    }
}
