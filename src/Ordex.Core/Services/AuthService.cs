using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Enums;
using Ordex.Core.Models;

namespace Ordex.Core.Services;

public sealed class AuthService(
    IUserRepository users,
    ICompanyRepository companies,
    IBusinessUnitRepository businessUnits,
    IPasswordHasher hasher,
    ICurrentUser currentUser,
    IClock clock) : IAuthService
{
    /// <summary>Failed attempts allowed before the account is locked.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>How long a locked account stays locked.</summary>
    public const int LockoutMinutes = 15;

    // Used to spend the same time on "unknown user" as on "wrong password",
    // so an attacker can't discover valid usernames by measuring response time.
    private static string? _dummyHash;

    public async Task<ServiceResult<AuthenticatedUser>> LoginAsync(LoginInput input)
    {
        var user = await users.GetByUserNameAsync(input.UserName.Trim());

        if (user is null)
        {
            _dummyHash ??= hasher.Hash(Guid.NewGuid().ToString());
            hasher.Verify(_dummyHash, input.Password);
            return ServiceResult<AuthenticatedUser>.Fail(Messages.InvalidLogin);
        }

        var now = clock.Now;

        if (user.LockoutEnd is { } lockoutEnd && lockoutEnd > now)
        {
            var minutesLeft = Math.Max(1, (int)Math.Ceiling((lockoutEnd - now).TotalMinutes));
            return ServiceResult<AuthenticatedUser>.Fail(string.Format(Messages.AccountLocked, minutesLeft));
        }

        var check = hasher.Verify(user.PasswordHash, input.Password);
        if (check == PasswordCheck.Failed)
        {
            var failed = user.AccessFailedCount + 1;
            DateTime? lockUntil = failed >= MaxFailedAttempts ? now.AddMinutes(LockoutMinutes) : null;
            await users.RecordLoginFailureAsync(user.Id, lockUntil is null ? failed : 0, lockUntil);

            return lockUntil is null
                ? ServiceResult<AuthenticatedUser>.Fail(Messages.InvalidLogin)
                : ServiceResult<AuthenticatedUser>.Fail(string.Format(Messages.AccountLocked, LockoutMinutes));
        }

        if (!user.IsActive)
            return ServiceResult<AuthenticatedUser>.Fail(Messages.AccountInactive);

        var companyName = "All companies";
        var unitName = "All business units";

        if (user.CompanyId != 0)
        {
            var company = await companies.GetByIdAsync(user.CompanyId);
            if (company is null || !company.IsActive)
                return ServiceResult<AuthenticatedUser>.Fail(Messages.AccountInactive);
            companyName = company.CompanyName;
        }

        if (user.BusinessUnitId != 0)
        {
            var unit = await businessUnits.GetByIdAsync(user.BusinessUnitId);
            if (unit is null || !unit.IsActive)
                return ServiceResult<AuthenticatedUser>.Fail(Messages.AccountInactive);
            unitName = unit.UnitName;
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
            await users.UpdatePasswordAsync(user.Id, hasher.Hash(input.Password), user.Id, now);

        await users.RecordLoginSuccessAsync(user.Id, now);

        return ServiceResult<AuthenticatedUser>.Ok(new AuthenticatedUser
        {
            Id = user.Id,
            FullName = user.FullName,
            UserName = user.UserName,
            Role = user.Role,
            CompanyId = user.CompanyId,
            BusinessUnitId = user.BusinessUnitId,
            CompanyName = companyName,
            BusinessUnitName = unitName,
            Theme = Appearance.NormalizeTheme(user.Theme),
            ColorMode = Appearance.NormalizeMode(user.ColorMode)
        });
    }

    public async Task<ServiceResult> SaveAppearanceAsync(string theme, string colorMode)
    {
        if (!currentUser.IsAuthenticated)
            return ServiceResult.Fail(Messages.AccessDenied);

        if (!Appearance.IsValidTheme(theme) || !Appearance.IsValidMode(colorMode))
            return ServiceResult.Fail(Messages.InvalidAppearance);

        await users.UpdateAppearanceAsync(currentUser.UserId, theme, colorMode);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ChangePasswordAsync(ChangePasswordInput input)
    {
        var user = await users.GetByIdAsync(currentUser.UserId);
        if (user is null)
            return ServiceResult.Fail(Messages.NotFound);

        if (hasher.Verify(user.PasswordHash, input.CurrentPassword) == PasswordCheck.Failed)
            return ServiceResult.Fail(Messages.WrongCurrentPassword);

        await users.UpdatePasswordAsync(user.Id, hasher.Hash(input.NewPassword), user.Id, clock.Now);
        return ServiceResult.Ok();
    }

    public async Task<bool> IsSessionValidAsync(int userId, UserRole role, int companyId, int businessUnitId)
    {
        var user = await users.GetByIdAsync(userId);
        if (user is null
            || !user.IsActive
            || user.Role != role
            || user.CompanyId != companyId
            || user.BusinessUnitId != businessUnitId)
            return false;

        // A company or unit switched off by the SuperAdmin signs its users out at the next check.
        if (companyId != 0 && (await companies.GetByIdAsync(companyId)) is not { IsActive: true })
            return false;

        if (businessUnitId != 0 && (await businessUnits.GetByIdAsync(businessUnitId)) is not { IsActive: true })
            return false;

        return true;
    }

    /// <summary>Creates the first SuperAdmin on a fresh database. Does nothing afterwards.</summary>
    public async Task EnsureSuperAdminAsync(string userName, string password, string fullName)
    {
        if (await users.AnySuperAdminAsync())
            return;

        await users.InsertAsync(new AppUser
        {
            CompanyId = 0,
            BusinessUnitId = 0,
            FullName = fullName,
            UserName = userName,
            PasswordHash = hasher.Hash(password),
            Role = UserRole.SuperAdmin,
            IsActive = true,
            CreatedBy = 0,
            CreatedAt = clock.Now
        });
    }
}
