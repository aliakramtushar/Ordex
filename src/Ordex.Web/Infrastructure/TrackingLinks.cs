using System.Text;

namespace Ordex.Web.Infrastructure;

/// <summary>
/// Builds the customer's order link (https://your-site/t/{token}) and the WhatsApp message that carries it.
/// Set <c>App:PublicBaseUrl</c> (e.g. https://ordex.example.com) when the site sits behind a proxy
/// that hides the real host; otherwise the current request's address is used.
/// </summary>
public sealed class TrackingLinks(IConfiguration configuration, IHttpContextAccessor accessor)
{
    public string Url(string? token)
    {
        if (string.IsNullOrEmpty(token)) return string.Empty;

        var configured = configuration["App:PublicBaseUrl"];
        var baseUrl = !string.IsNullOrWhiteSpace(configured)
            ? configured.TrimEnd('/')
            : accessor.HttpContext is { } ctx ? $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}" : string.Empty;

        return $"{baseUrl}/t/{Uri.EscapeDataString(token)}";
    }

    /// <summary>Ready-to-send message for one order.</summary>
    public string Message(string customerName, string shopName, string orderNo, string productName, string? token)
    {
        var first = FirstName(customerName);
        return $"Hi {first}, thank you for ordering from {shopName}! 🛍️\n" +
               $"You can check your order {orderNo} ({productName}) anytime here:\n{Url(token)}";
    }

    /// <summary>One message listing several orders (same customer).</summary>
    public string Message(string customerName, string shopName, IEnumerable<(string OrderNo, string ProductName, string? Token)> orders)
    {
        var sb = new StringBuilder();
        sb.Append($"Hi {FirstName(customerName)}, thank you for ordering from {shopName}! 🛍️\n");
        sb.Append("You can check your orders anytime here:\n");
        foreach (var (orderNo, product, token) in orders)
            sb.Append($"\n• {product} ({orderNo})\n{Url(token)}\n");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// wa.me link with the message filled in. Bangladeshi numbers (01XXXXXXXXX) get the 880 prefix;
    /// numbers already written with a country code are used as they are.
    /// </summary>
    public static string WhatsApp(string? mobile, string message)
    {
        var digits = new string((mobile ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith('0')) digits = "88" + digits;
        var target = digits.Length >= 8 ? digits : string.Empty;
        return $"https://wa.me/{target}?text={Uri.EscapeDataString(message)}";
    }

    private static string FirstName(string name)
    {
        var first = (name ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "there" : first;
    }
}
