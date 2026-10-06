using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Ordex.Core.Common;

/// <summary>
/// Tokens for the customer's order link (/t/{token}).
/// 128 random bits from the OS crypto generator, URL-safe Base64 (22 characters):
/// impossible to guess, so the link itself is the key – no login or OTP needed.
/// </summary>
public static partial class TrackingToken
{
    public static string New()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Cheap shape check before touching the database.</summary>
    public static bool LooksValid(string? token) => token is not null && Shape().IsMatch(token);

    [GeneratedRegex("^[A-Za-z0-9_-]{20,40}$")]
    private static partial Regex Shape();
}
