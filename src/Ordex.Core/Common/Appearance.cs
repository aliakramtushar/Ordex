namespace Ordex.Core.Common;

/// <summary>
/// Per-user look: a colour theme and a light/dark mode.
/// The keys are stored on the user (AppUser.Theme / AppUser.ColorMode); the colours
/// themselves live in the web layer (Ordex.Web/Theming/ThemeCatalog.cs).
/// </summary>
public static class Appearance
{
    public const string DefaultTheme = "brattle";
    public const string DefaultMode = ColorModes.Light;

    /// <summary>Every theme key the app knows. Keep in sync with ThemeCatalog.</summary>
    public static readonly IReadOnlyList<string> ThemeKeys = ["brattle", "blush", "mend", "navy"];

    public static class ColorModes
    {
        public const string Light = "light";
        public const string Dark = "dark";
        public const string System = "system";
        public static readonly IReadOnlyList<string> All = [Light, Dark, System];
    }

    public static string NormalizeTheme(string? key) =>
        key is not null && ThemeKeys.Contains(key.Trim().ToLowerInvariant()) ? key.Trim().ToLowerInvariant() : DefaultTheme;

    public static string NormalizeMode(string? mode) =>
        mode is not null && ColorModes.All.Contains(mode.Trim().ToLowerInvariant()) ? mode.Trim().ToLowerInvariant() : DefaultMode;

    public static bool IsValidTheme(string? key) => key is not null && ThemeKeys.Contains(key);
    public static bool IsValidMode(string? mode) => mode is not null && ColorModes.All.Contains(mode);
}
