using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Enums;
using Ordex.Core.Models;

namespace Ordex.Core.Services;

/// <summary>
/// Companies.
///  • SuperAdmin: create, edit, switch on/off and delete (only while the company has no data).
///  • Admin: edits his own company's details (name, contact, currencies, default rate) – no code, no on/off, no delete.
/// </summary>
public sealed class CompanyService(
    ICompanyRepository companies,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    IClock clock) : ICompanyService
{
    public Task<IReadOnlyList<Company>> GetAllAsync() => companies.GetAllAsync();

    public async Task<CompanyInput?> GetForEditAsync(int id)
    {
        if (!user.IsSuperAdmin)
            return null;

        var c = await companies.GetByIdAsync(id);
        return c is null ? null : new CompanyInput
        {
            Id = c.Id,
            CompanyCode = c.CompanyCode,
            CompanyName = c.CompanyName,
            Phone = c.Phone,
            Email = c.Email,
            Address = c.Address,
            DefaultExchangeRate = c.DefaultExchangeRate,
            PurchaseCurrency = c.PurchaseCurrency,
            SalesCurrency = c.SalesCurrency,
            IsActive = c.IsActive
        };
    }

    public async Task<ServiceResult<int>> SaveAsync(CompanyInput input)
    {
        if (!user.IsSuperAdmin)
            return ServiceResult<int>.Fail(Messages.AccessDenied);

        if (!Currencies.IsSupported(input.PurchaseCurrency) || !Currencies.IsSupported(input.SalesCurrency))
            return ServiceResult<int>.Fail(Messages.InvalidCurrency);

        var code = input.CompanyCode.Trim().ToUpperInvariant();
        if (await companies.CodeExistsAsync(code, input.Id))
            return ServiceResult<int>.Fail(Messages.DuplicateCode);

        var company = input.Id == 0 ? new Company() : await companies.GetByIdAsync(input.Id);
        if (company is null)
            return ServiceResult<int>.Fail(Messages.NotFound);

        company.CompanyCode = code;
        ApplyDetails(company, input.CompanyName, input.Phone, input.Email, input.Address,
            input.DefaultExchangeRate, input.PurchaseCurrency, input.SalesCurrency);
        company.IsActive = input.IsActive;

        if (company.Id == 0)
        {
            company.CreatedBy = user.UserId;
            company.CreatedAt = clock.Now;
            return ServiceResult<int>.Ok(await companies.InsertAsync(company));
        }

        company.UpdatedBy = user.UserId;
        company.UpdatedAt = clock.Now;
        await companies.UpdateAsync(company);
        return ServiceResult<int>.Ok(company.Id);
    }

    public async Task<CompanyProfileInput?> GetProfileAsync()
    {
        if (!user.IsAdmin || user.CompanyId == 0)
            return null;

        var c = await companies.GetByIdAsync(user.CompanyId);
        return c is null ? null : new CompanyProfileInput
        {
            CompanyCode = c.CompanyCode,
            CompanyName = c.CompanyName,
            Phone = c.Phone,
            Email = c.Email,
            Address = c.Address,
            DefaultExchangeRate = c.DefaultExchangeRate,
            PurchaseCurrency = c.PurchaseCurrency,
            SalesCurrency = c.SalesCurrency
        };
    }

    public async Task<ServiceResult> SaveProfileAsync(CompanyProfileInput input)
    {
        // The company always comes from the signed-in user, never from the form.
        if (!user.IsAdmin || user.CompanyId == 0)
            return ServiceResult.Fail(Messages.AccessDenied);

        if (!Currencies.IsSupported(input.PurchaseCurrency) || !Currencies.IsSupported(input.SalesCurrency))
            return ServiceResult.Fail(Messages.InvalidCurrency);

        var company = await companies.GetByIdAsync(user.CompanyId);
        if (company is null)
            return ServiceResult.Fail(Messages.NotFound);

        ApplyDetails(company, input.CompanyName, input.Phone, input.Email, input.Address,
            input.DefaultExchangeRate, input.PurchaseCurrency, input.SalesCurrency);
        company.UpdatedBy = user.UserId;
        company.UpdatedAt = clock.Now;
        await companies.UpdateAsync(company); // code and IsActive are kept as loaded
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<string>> SetActiveAsync(int id, bool isActive)
    {
        if (!user.IsSuperAdmin)
            return ServiceResult<string>.Fail(Messages.AccessDenied);

        var company = await companies.GetByIdAsync(id);
        if (company is null)
            return ServiceResult<string>.Fail(Messages.NotFound);

        await companies.SetActiveAsync(id, isActive, user.UserId, clock.Now);
        return ServiceResult<string>.Ok(company.CompanyName);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        if (!user.IsSuperAdmin)
            return ServiceResult.Fail(Messages.AccessDenied);

        if (await companies.GetByIdAsync(id) is null)
            return ServiceResult.Fail(Messages.NotFound);

        return await unitOfWork.ExecuteAsync(async () =>
        {
            if (await companies.HasDataAsync(id))
                return ServiceResult.Fail(Messages.CompanyHasData);

            await companies.DeleteAsync(id);
            return ServiceResult.Ok();
        });
    }

    private static void ApplyDetails(Company company, string name, string? phone, string? email, string? address,
        decimal rate, string purchaseCurrency, string salesCurrency)
    {
        company.CompanyName = name.Trim();
        company.Phone = TextHelper.Clean(phone);
        company.Email = TextHelper.Clean(email);
        company.Address = TextHelper.Clean(address);
        company.DefaultExchangeRate = rate;
        company.PurchaseCurrency = purchaseCurrency.Trim().ToUpperInvariant();
        company.SalesCurrency = salesCurrency.Trim().ToUpperInvariant();
    }
}

/// <summary>
/// Business units.
///  • SuperAdmin: create, edit, switch on/off and delete (only while the unit has no data) in any company.
///  • Admin: edits the units of his own company (or only his own unit when bound to one) – no add, delete or on/off.
/// </summary>
public sealed class BusinessUnitService(
    IBusinessUnitRepository units,
    ICompanyRepository companies,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    IClock clock) : IBusinessUnitService
{
    public async Task<IReadOnlyList<BusinessUnitListItem>> GetListAsync()
    {
        if (!user.IsAdmin)
            return [];

        var list = await units.GetListAsync(user.IsSuperAdmin ? user.Scope.CompanyId : user.CompanyId);
        return user.BusinessUnitId == 0 ? list : list.Where(u => u.Id == user.BusinessUnitId).ToList();
    }

    public async Task<BusinessUnitInput?> GetForEditAsync(int id)
    {
        var unit = await units.GetByIdAsync(id);
        if (unit is null || !CanManage(unit))
            return null;

        return new BusinessUnitInput
        {
            Id = unit.Id,
            CompanyId = unit.CompanyId,
            UnitCode = unit.UnitCode,
            UnitName = unit.UnitName,
            IsActive = unit.IsActive
        };
    }

    public async Task<ServiceResult<int>> SaveAsync(BusinessUnitInput input)
    {
        if (!user.IsAdmin)
            return ServiceResult<int>.Fail(Messages.AccessDenied);

        var isNew = input.Id == 0;
        if (isNew && !user.IsSuperAdmin)
            return ServiceResult<int>.Fail(Messages.AccessDenied); // only SuperAdmin adds units

        var unit = isNew ? new BusinessUnit() : await units.GetByIdAsync(input.Id);
        if (unit is null || (!isNew && !CanManage(unit)))
            return ServiceResult<int>.Fail(Messages.NotFound);

        // Company can't move after creation; on create the SuperAdmin picks it.
        var companyId = isNew ? input.CompanyId.GetValueOrDefault() : unit.CompanyId;
        if (isNew && await companies.GetByIdAsync(companyId) is null)
            return ServiceResult<int>.Fail(Messages.SelectCompany);

        var code = input.UnitCode.Trim().ToUpperInvariant();
        if (await units.CodeExistsAsync(companyId, code, input.Id))
            return ServiceResult<int>.Fail(Messages.DuplicateCode);

        unit.UnitCode = code;
        unit.UnitName = input.UnitName.Trim();
        if (user.IsSuperAdmin)
            unit.IsActive = input.IsActive; // Admins can't switch units on/off

        if (isNew)
        {
            unit.CompanyId = companyId;
            unit.CreatedBy = user.UserId;
            unit.CreatedAt = clock.Now;
            return ServiceResult<int>.Ok(await units.InsertAsync(unit));
        }

        unit.UpdatedBy = user.UserId;
        unit.UpdatedAt = clock.Now;
        await units.UpdateAsync(unit);
        return ServiceResult<int>.Ok(unit.Id);
    }

    public async Task<ServiceResult<string>> SetActiveAsync(int id, bool isActive)
    {
        if (!user.IsSuperAdmin)
            return ServiceResult<string>.Fail(Messages.AccessDenied);

        var unit = await units.GetByIdAsync(id);
        if (unit is null)
            return ServiceResult<string>.Fail(Messages.NotFound);

        await units.SetActiveAsync(id, isActive, user.UserId, clock.Now);
        return ServiceResult<string>.Ok(unit.UnitName);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        if (!user.IsSuperAdmin)
            return ServiceResult.Fail(Messages.AccessDenied);

        if (await units.GetByIdAsync(id) is null)
            return ServiceResult.Fail(Messages.NotFound);

        return await unitOfWork.ExecuteAsync(async () =>
        {
            if (await units.HasDataAsync(id))
                return ServiceResult.Fail(Messages.UnitHasData);

            await units.DeleteAsync(id);
            return ServiceResult.Ok();
        });
    }

    private bool CanManage(BusinessUnit unit) =>
        user.IsSuperAdmin ||
        (user.IsAdmin && unit.CompanyId == user.CompanyId && (user.BusinessUnitId == 0 || unit.Id == user.BusinessUnitId));
}

/// <summary>
/// User management rules:
///  • SuperAdmin manages everyone.
///  • Admin manages Admin/Staff users of his own company only.
///  • SuperAdmin accounts always have CompanyId = 0 and BusinessUnitId = 0.
///  • Staff must belong to a business unit.
///  • Nobody can deactivate or demote himself.
/// </summary>
public sealed class UserService(
    IUserRepository users,
    IBusinessUnitRepository units,
    ICompanyRepository companies,
    IPasswordHasher hasher,
    ICurrentUser user,
    IClock clock) : IUserService
{
    public Task<PagedResult<UserListItem>> GetPagedAsync(UserFilter filter) =>
        users.GetPagedAsync(new TenantScope(user.Scope.CompanyId, 0), filter);

    public async Task<UserInput?> GetForEditAsync(int id)
    {
        var u = await users.GetByIdAsync(id);
        if (u is null || !CanManage(u))
            return null;

        return new UserInput
        {
            Id = u.Id,
            FullName = u.FullName,
            UserName = u.UserName,
            Email = u.Email,
            Phone = u.Phone,
            Role = u.Role,
            CompanyId = u.CompanyId,
            BusinessUnitId = u.BusinessUnitId,
            IsActive = u.IsActive
        };
    }

    public async Task<ServiceResult<int>> SaveAsync(UserInput input)
    {
        if (!user.IsAdmin)
            return ServiceResult<int>.Fail(Messages.AccessDenied);

        var isNew = input.Id == 0;
        var entity = isNew ? new AppUser() : await users.GetByIdAsync(input.Id);
        if (entity is null || (!isNew && !CanManage(entity)))
            return ServiceResult<int>.Fail(Messages.NotFound);

        var rules = await ValidateAsync(input, entity, isNew);
        if (!rules.Succeeded)
            return ServiceResult<int>.Fail(rules.Error!);

        entity.FullName = input.FullName.Trim();
        entity.UserName = input.UserName.Trim();
        entity.Email = input.Email?.Trim();
        entity.Phone = input.Phone?.Trim();
        entity.Role = input.Role;
        entity.IsActive = input.IsActive;
        (entity.CompanyId, entity.BusinessUnitId) = ResolveScope(input);

        if (isNew)
        {
            entity.PasswordHash = hasher.Hash(input.Password!);
            entity.CreatedBy = user.UserId;
            entity.CreatedAt = clock.Now;
            return ServiceResult<int>.Ok(await users.InsertAsync(entity));
        }

        entity.UpdatedBy = user.UserId;
        entity.UpdatedAt = clock.Now;
        await users.UpdateAsync(entity);

        if (!string.IsNullOrWhiteSpace(input.Password))
            await users.UpdatePasswordAsync(entity.Id, hasher.Hash(input.Password), user.UserId, clock.Now);

        return ServiceResult<int>.Ok(entity.Id);
    }

    private async Task<ServiceResult> ValidateAsync(UserInput input, AppUser existing, bool isNew)
    {
        if (isNew && string.IsNullOrWhiteSpace(input.Password))
            return ServiceResult.Fail("Password is required for a new user.");

        if (input.Role == UserRole.SuperAdmin && !user.IsSuperAdmin)
            return ServiceResult.Fail(Messages.AccessDenied);

        if (!isNew && existing.Id == user.UserId && (!input.IsActive || input.Role != existing.Role))
            return ServiceResult.Fail("You can't deactivate yourself or change your own role.");

        if (await users.UserNameExistsAsync(input.UserName.Trim(), input.Id))
            return ServiceResult.Fail(Messages.UserNameTaken);

        if (input.Role == UserRole.SuperAdmin)
            return ServiceResult.Ok();

        var (companyId, unitId) = ResolveScope(input);

        if (companyId == 0 || await companies.GetByIdAsync(companyId) is null)
            return ServiceResult.Fail(Messages.SelectCompany);

        if (input.Role == UserRole.Staff && unitId == 0)
            return ServiceResult.Fail("Staff users must belong to a business unit.");

        if (unitId != 0)
        {
            var unit = await units.GetByIdAsync(unitId);
            if (unit is null || unit.CompanyId != companyId)
                return ServiceResult.Fail(Messages.InvalidBusinessUnit);
        }

        return ServiceResult.Ok();
    }

    private (int CompanyId, int BusinessUnitId) ResolveScope(UserInput input)
    {
        if (input.Role == UserRole.SuperAdmin)
            return (0, 0);

        var companyId = user.CompanyId != 0 ? user.CompanyId : input.CompanyId;
        var unitId = user.BusinessUnitId != 0 ? user.BusinessUnitId : input.BusinessUnitId;
        return (companyId, unitId);
    }

    private bool CanManage(AppUser target)
    {
        if (user.IsSuperAdmin) return true;
        if (target.Role == UserRole.SuperAdmin) return false;
        return target.CompanyId == user.CompanyId &&
               (user.BusinessUnitId == 0 || target.BusinessUnitId == user.BusinessUnitId);
    }
}
