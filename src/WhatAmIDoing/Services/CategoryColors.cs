using System.Linq;
using System.Text.RegularExpressions;
using WhatAmIDoing.Data;

namespace WhatAmIDoing.Services;

/// <summary>
/// Color per category label for charts and HTML. Uses optional DB overrides (see
/// <see cref="Bind"/>) then falls back to a deterministic palette hash so the dashboard and export match.
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

    private static AppDatabase? _db;

    /// <summary>Call once at startup so <see cref="Pick"/> can resolve per-category hex overrides.</summary>
    public static void Bind(AppDatabase db) => _db = db;

    /// <summary>Returns <c>#RRGGBB</c> uppercase, or false if the string is not a valid web color.</summary>
    public static bool TryNormalizeHex(string? input, out string canonicalHex)
    {
        canonicalHex = "";
        if (string.IsNullOrWhiteSpace(input))
            return false;
        var s = input.Trim();
        if (!s.StartsWith("#", StringComparison.Ordinal))
            s = "#" + s;

        // #RGB or #RRGGBB
        if (!HexStrict.IsMatch(s))
            return false;

        var body = s[1..];
        if (body.Length == 3)
        {
            canonicalHex = "#" + string.Concat(body.Select(ch => $"{ch}{ch}")).ToUpperInvariant();
            return true;
        }

        canonicalHex = "#" + body.ToUpperInvariant();
        return true;
    }

    private static readonly Regex HexStrict = new(@"^#(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        if (_db is not null && _db.TryGetCategoryColor(category, out var raw) && TryNormalizeHex(raw, out var custom))
            return custom;

        var hash = unchecked((uint)0x811C9DC5);
        foreach (var ch in category)
        {
            hash ^= ch;
            hash *= 0x01000193;
        }

        return Palette[hash % (uint)Palette.Length];
    }
}
