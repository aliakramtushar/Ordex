using Ordex.Core.Common;
using Ordex.Core.Enums;

namespace Ordex.Core.Abstractions;

/// <summary>The logged-in user, read from the auth cookie.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    string UserName { get; }
    string FullName { get; }
    UserRole Role { get; }
    int CompanyId { get; }
    int BusinessUnitId { get; }

    bool IsSuperAdmin => Role == UserRole.SuperAdmin;
    bool IsAdmin => Role is UserRole.SuperAdmin or UserRole.Admin;

    /// <summary>What this user is allowed to see (0 = all). Used for permission checks and new records.</summary>
    TenantScope HomeScope => new(CompanyId, BusinessUnitId);

    /// <summary>
    /// What this user is looking at right now: the home scope, optionally narrowed by the
    /// company / business-unit switcher in the top bar. Never wider than <see cref="HomeScope"/>.
    /// Every list, report and get-by-id query is filtered by this.
    /// </summary>
    TenantScope Scope => HomeScope;
}

/// <summary>Business clock (Bangladesh time by default). Never use DateTime.Now directly.</summary>
public interface IClock
{
    DateTime Now { get; }
    DateTime Today => Now.Date;
}

public interface IPasswordHasher
{
    string Hash(string password);
    PasswordCheck Verify(string hash, string password);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded
}

/// <summary>An uploaded file, independent of ASP.NET (so services stay framework-free).</summary>
public sealed record UploadedFile(string FileName, long Length, Func<Stream> OpenReadStream);

/// <summary>
/// Stores uploaded images and returns a web path such as "/uploads/1/orders/abc.jpg".
/// Implementations must validate size, extension AND file signature (magic bytes).
/// </summary>
public interface IFileStorage
{
    Task<ServiceResult<string>> SaveImageAsync(UploadedFile file, string folder, CancellationToken ct = default);
    void Delete(string? webPath);
}

/// <summary>
/// Runs a block of database work inside ONE transaction (all-or-nothing).
/// Nested calls join the outer transaction.
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<Task> work, CancellationToken ct = default);
    Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default);
}
