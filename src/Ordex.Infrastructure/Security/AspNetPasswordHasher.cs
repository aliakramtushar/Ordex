using Microsoft.AspNetCore.Identity;
using Ordex.Core.Abstractions;
using Ordex.Core.Entities;

namespace Ordex.Infrastructure.Security;

/// <summary>
/// Uses ASP.NET Core Identity's hasher: PBKDF2 with HMAC-SHA512, random salt,
/// 100,000 iterations. Old hashes are flagged for automatic upgrade on login.
/// </summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<AppUser> _hasher = new();
    private static readonly AppUser NoUser = new();

    public string Hash(string password) => _hasher.HashPassword(NoUser, password);

    public PasswordCheck Verify(string hash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(NoUser, hash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
                _ => PasswordCheck.Failed
            };
        }
        catch (FormatException)
        {
            // A corrupted hash must never crash the login page.
            return PasswordCheck.Failed;
        }
    }
}

public sealed class AppClockOptions
{
    public const string SectionName = "App";

    /// <summary>IANA or Windows time zone id. Default: Bangladesh.</summary>
    public string TimeZone { get; set; } = "Asia/Dhaka";
}

/// <summary>Business clock – all dates are stored in the shop's local time.</summary>
public sealed class SystemClock : IClock
{
    private readonly TimeZoneInfo _zone;

    public SystemClock(Microsoft.Extensions.Options.IOptions<AppClockOptions> options)
    {
        _zone = Resolve(options.Value.TimeZone);
    }

    public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);

    private static TimeZoneInfo Resolve(string id)
    {
        foreach (var candidate in new[] { id, "Asia/Dhaka", "Bangladesh Standard Time" })
        {
            if (TimeZoneInfo.TryFindSystemTimeZoneById(candidate, out var zone))
                return zone;
        }

        return TimeZoneInfo.CreateCustomTimeZone("BST+6", TimeSpan.FromHours(6), "Bangladesh", "Bangladesh");
    }
}
