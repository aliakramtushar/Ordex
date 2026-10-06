using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;

namespace Ordex.Core.Services;

public sealed class ScopeService(
    ICurrentUser user,
    ICompanyRepository companies,
    IBusinessUnitRepository businessUnits) : IScopeService
{
    public bool NeedsCompany => user.CompanyId == 0;
    public bool NeedsBusinessUnit => user.BusinessUnitId == 0;

    public async Task<ServiceResult<TenantScope>> ResolveForCreateAsync(int? companyId, int? businessUnitId)
    {
        // A user bound to a company / unit always writes into it – form values are ignored.
        var resolvedCompanyId = user.CompanyId != 0 ? user.CompanyId : companyId.GetValueOrDefault();
        var resolvedUnitId = user.BusinessUnitId != 0 ? user.BusinessUnitId : businessUnitId.GetValueOrDefault();

        if (resolvedCompanyId == 0)
            return ServiceResult<TenantScope>.Fail(Messages.SelectCompany);

        if (resolvedUnitId == 0)
            return ServiceResult<TenantScope>.Fail(Messages.SelectBusinessUnit);

        if (NeedsCompany)
        {
            var company = await companies.GetByIdAsync(resolvedCompanyId);
            if (company is null || !company.IsActive)
                return ServiceResult<TenantScope>.Fail(Messages.SelectCompany);
        }

        if (NeedsBusinessUnit)
        {
            var unit = await businessUnits.GetByIdAsync(resolvedUnitId);
            if (unit is null || !unit.IsActive || unit.CompanyId != resolvedCompanyId)
                return ServiceResult<TenantScope>.Fail(Messages.InvalidBusinessUnit);
        }

        return ServiceResult<TenantScope>.Ok(new TenantScope(resolvedCompanyId, resolvedUnitId));
    }
}
