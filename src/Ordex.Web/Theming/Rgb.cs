using System.Globalization;

namespace Ordex.Web.Theming;

/// <summary>
/// A small sRGB colour type with the maths the theme engine needs:
/// mixing, WCAG contrast and "nudge until readable".
/// </summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static readonly Rgb White = new(255, 255, 255);
    public static readonly Rgb Black = new(0, 0, 0);

    public static Rgb Parse(string hex)
    {
        var h = hex.Trim().TrimStart('#');
        if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"'{hex}' is not a #rrggbb colour.");
        return new Rgb((byte)(v >> 16), (byte)(v >> 8 & 0xFF), (byte)(v & 0xFF));
    }

    /// <summary>Mixes <paramref name="amount"/> (0–1) of <paramref name="other"/> into this colour.</summary>
    public Rgb Mix(Rgb other, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte M(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
        return new Rgb(M(R, other.R), M(G, other.G), M(B, other.B));
    }

    public Rgb Lighten(double amount) => Mix(White, amount);
    public Rgb Darken(double amount) => Mix(Black, amount);

    /// <summary>WCAG relative luminance (0 = black, 1 = white).</summary>
    public double Luminance
    {
        get
        {
            static double Ch(byte c)
            {
                var s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Ch(R) + 0.7152 * Ch(G) + 0.0722 * Ch(B);
        }
    }

    public bool IsLight => Luminance > 0.4;

    /// <summary>WCAG contrast ratio, 1–21.</summary>
    public double Contrast(Rgb other)
    {
        var (a, b) = (Luminance, other.Luminance);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>
    /// Text colour for a filled surface (button, badge). White is preferred while it stays clearly
    /// readable (≥ 3.8:1, buttons use semibold text); otherwise near-black.
    /// </summary>
    public Rgb ReadableText(Rgb? dark = null, Rgb? light = null)
    {
        var d = dark ?? new Rgb(10, 14, 20);
        var l = light ?? White;
        if (Contrast(l) >= 3.8) return l;
        return Contrast(d) >= Contrast(l) ? d : l;
    }

    /// <summary>
    /// Keeps the hue and saturation but lowers (on light backgrounds) or raises (on dark ones)
    /// the HSL lightness in small steps until the colour reaches <paramref name="minRatio"/>
    /// against <paramref name="background"/>. Working in HSL keeps colours vivid instead of muddy.
    /// </summary>
    public Rgb EnsureContrast(Rgb background, double minRatio)
    {
        if (Contrast(background) >= minRatio) return this;

        var (h, s, l) = ToHsl();
        var step = background.IsLight ? -0.015 : 0.015;
        var c = this;
        for (var i = 0; i < 70 && c.Contrast(background) < minRatio; i++)
        {
            l = Math.Clamp(l + step, 0, 1);
            c = FromHsl(h, s, l);
        }
        return c;
    }

    public (double H, double S, double L) ToHsl()
    {
        double r = R / 255.0, g = G / 255.0, b = B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double h = 0, s = 0, l = (max + min) / 2;
        var d = max - min;
        if (d > 1e-9)
        {
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0)
              : max == g ? (b - r) / d + 2
              : (r - g) / d + 4;
            h /= 6;
        }
        return (h, s, l);
    }

    public static Rgb FromHsl(double h, double s, double l)
    {
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        double r, g, b;
        if (s < 1e-9) r = g = b = l;
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = Hue(p, q, h + 1.0 / 3);
            g = Hue(p, q, h);
            b = Hue(p, q, h - 1.0 / 3);
        }
        static byte B8(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
        return new Rgb(B8(r), B8(g), B8(b));
    }

    public string Hex => $"#{R:x2}{G:x2}{B:x2}";
    public string Channels => $"{R}, {G}, {B}";
    public string Alpha(double a) => $"rgba({R}, {G}, {B}, {a.ToString("0.##", CultureInfo.InvariantCulture)})";
    public override string ToString() => Hex;
}
