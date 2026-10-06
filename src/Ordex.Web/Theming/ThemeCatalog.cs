using Ordex.Core.Common;

namespace Ordex.Web.Theming;

/// <summary>
/// A theme is described by a handful of brand colours; every other token
/// (backgrounds, borders, hover states, readable text, dark mode…) is derived
/// from them by <see cref="ThemeCss"/>. Adding a theme = adding one entry here
/// (and its key to <see cref="Appearance.ThemeKeys"/>).
/// </summary>
public sealed record ThemeDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>Main brand colour: buttons, active tab, the ＋ button.</summary>
    public required string Brand { get; init; }

    /// <summary>Highlight: active menu marker, money owed, badges.</summary>
    public required string Accent { get; init; }

    /// <summary>Very dark colour for the sidebar, toasts and headline cards.</summary>
    public required string Ink { get; init; }

    /// <summary>Colour the page background is tinted with (kept very light so it is easy on the eyes).</summary>
    public required string Canvas { get; init; }

    /// <summary>How strongly the canvas colour tints the page (0–1).</summary>
    public double CanvasStrength { get; init; } = 0.12;

    /// <summary>The original palette, shown as swatches on the settings page.</summary>
    public required IReadOnlyList<string> Swatches { get; init; }
}

public static class ThemeCatalog
{
    public static readonly IReadOnlyList<ThemeDefinition> All =
    [
        new()
        {
            Key = "brattle",
            Name = "Brattle",
            Description = "Sunny yellow and neon blue on deep black – bold and lively.",
            Brand = "#5bccf6",
            Accent = "#fcde67",
            Ink = "#030e12",
            Canvas = "#fcde67",
            CanvasStrength = 0.14,
            Swatches = ["#5bccf6", "#fcde67", "#030e12"]
        },
        new()
        {
            Key = "blush",
            Name = "Blush",
            Description = "Champagne pink, middle purple and a touch of sunglow – warm and soft.",
            Brand = "#d58ebf",
            Accent = "#f9d326",
            Ink = "#2c1a28",
            Canvas = "#f0d9cb",
            CanvasStrength = 0.45,
            Swatches = ["#f0d9cb", "#f1dfd3", "#d58ebf", "#e2a8be", "#f9d326"]
        },
        new()
        {
            Key = "mend",
            Name = "Mend",
            Description = "Lavender and toolbox purple with tango pink and orange-yellow – calm and fresh.",
            Brand = "#7a77b9",
            Accent = "#f2c76e",
            Ink = "#1f1d3a",
            Canvas = "#ebe8e7",
            CanvasStrength = 0.6,
            Swatches = ["#ebe8e7", "#ea7186", "#f2c76e", "#7a77b9", "#bd9dea"]
        },
        new()
        {
            Key = "navy",
            Name = "Classic navy",
            Description = "The original Ordex look – ink navy with a signal-amber accent.",
            Brand = "#2457c5",
            Accent = "#e39a2d",
            Ink = "#0f1f3d",
            Canvas = "#e9edf3",
            CanvasStrength = 0.55,
            Swatches = ["#0f1f3d", "#2457c5", "#e39a2d", "#f3f5f8"]
        }
    ];

    public static ThemeDefinition Get(string? key) =>
        All.FirstOrDefault(t => t.Key == Appearance.NormalizeTheme(key)) ?? All[0];
}
