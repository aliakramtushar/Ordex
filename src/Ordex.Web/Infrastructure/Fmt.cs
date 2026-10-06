using System.Globalization;
using Ordex.Core.Enums;

namespace Ordex.Web.Infrastructure;

/// <summary>Display formatting used by every view – one place, consistent everywhere.</summary>
public static class Fmt
{
    // Bangladeshi grouping: 12,34,567
    private static readonly NumberFormatInfo LakhFormat = new()
    {
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = ".",
        NumberGroupSizes = [3, 2],
        NegativeSign = "-"
    };

    private static readonly NumberFormatInfo WesternFormat = new()
    {
        NumberGroupSeparator = ",",
        NumberDecimalSeparator = ".",
        NumberGroupSizes = [3],
        NegativeSign = "-"
    };

    /// <summary>
    /// ৳ 12,500 – shown in whole taka (exact paisa stay in the database),
    /// negative as "-৳ 8,215".
    /// </summary>
    public static string Bdt(decimal value)
    {
        var rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
        var text = Math.Abs(rounded).ToString("#,0", LakhFormat);
        return rounded < 0 ? $"-৳ {text}" : $"৳ {text}";
    }

    public static string Bdt(decimal? value) => value is null ? "—" : Bdt(value.Value);

    /// <summary>S$ 120.50</summary>
    public static string Sgd(decimal value) => $"S$ {value.ToString("#,0.00", WesternFormat)}";

    public static string Number(decimal value) => value.ToString("#,0.##", LakhFormat);

    public static string Rate(decimal value) => value.ToString("0.00##", CultureInfo.InvariantCulture);

    public static string Date(DateTime value) => value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    public static string Date(DateTime? value) => value is null ? "—" : Date(value.Value);

    public static string DateTime(DateTime value) => value.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime? value) => value is null ? "—" : DateTime(value.Value);

    public static string Month(DateTime value) => value.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Value for &lt;input type="date"&gt;.</summary>
    public static string InputDate(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>CSS modifier per status: used for badges and the coloured order cards.</summary>
    public static string StatusClass(OrderStatus status) => status switch
    {
        OrderStatus.PreOrder => "pre",
        OrderStatus.OutForDelivery => "out",
        OrderStatus.Delivered => "done",
        OrderStatus.Returned => "ret",
        _ => "pre"
    };

    public static string StatusIcon(OrderStatus status) => status switch
    {
        OrderStatus.PreOrder => "bi-hourglass-split",
        OrderStatus.OutForDelivery => "bi-truck",
        OrderStatus.Delivered => "bi-check2-circle",
        OrderStatus.Returned => "bi-arrow-counterclockwise",
        _ => "bi-circle"
    };

    /// <summary>"tel:" link value.</summary>
    public static string Tel(string mobile) => $"tel:{mobile}";

    /// <summary>Only http(s) links are rendered as clickable – blocks javascript: URLs.</summary>
    public static string? SafeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = "https://" + trimmed;

        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.ToString()
            : null;
    }

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }
}
