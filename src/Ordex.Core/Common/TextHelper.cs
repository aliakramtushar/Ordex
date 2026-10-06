using System.Text;

namespace Ordex.Core.Common;

public static class TextHelper
{
    /// <summary>"+880 1711-223344" -> "+8801711223344". Keeps digits and a leading plus.</summary>
    public static string NormalizeMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile))
            return string.Empty;

        var trimmed = mobile.Trim();
        var sb = new StringBuilder(trimmed.Length);
        if (trimmed.StartsWith('+'))
            sb.Append('+');

        foreach (var ch in trimmed)
        {
            if (char.IsAsciiDigit(ch))
                sb.Append(ch);
        }

        return sb.ToString();
    }

    /// <summary>Trims and turns empty strings into null.</summary>
    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
