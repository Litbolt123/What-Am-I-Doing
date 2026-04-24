namespace WhatAmIDoing.Services;

/// <summary>
/// Deterministic color per category label so the dashboard and the exported HTML report
/// always agree on which color means which category.
/// </summary>
public static class CategoryColors
{
    private static readonly string[] Palette =
    {
        "#4F8EF7", // blue
        "#34C38F", // green
        "#F1B44C", // amber
        "#F46A6A", // red
        "#7E7DF5", // purple
        "#50A5F1", // light blue
        "#74788D", // grey
        "#E07A5F", // coral
        "#5DBB63", // emerald
        "#D17DC9", // pink
        "#3DB1D2", // teal
        "#A37F4E", // brown
    };

    private const string IdleColor = "#C9CDD4";
    private const string IgnoredColor = "#9AA0A6";
    private const string UncategorizedColor = "#B7BDC6";

    public static string Pick(string category)
    {
        if (string.IsNullOrEmpty(category))
            return UncategorizedColor;

        if (category == CategoryClassifier.IdleCategory)
            return IdleColor;
        if (category.Equals("Ignored", StringComparison.OrdinalIgnoreCase))
            return IgnoredColor;
        if (category == CategoryClassifier.Uncategorized)
            return UncategorizedColor;

        var hash = unchecked((uint)0x811C9DC5);
        foreach (var ch in category)
        {
            hash ^= ch;
            hash *= 0x01000193;
        }

        return Palette[hash % (uint)Palette.Length];
    }
}
