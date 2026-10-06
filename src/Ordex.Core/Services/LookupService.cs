using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Models;

namespace Ordex.Core.Services;

/// <summary>Dropdown data. Every list is limited to what the current user may see.</summary>
public sealed class LookupService(
    ICurrentUser user,
    ICompanyRepository companies,
    IBusinessUnitRepository businessUnits,
    IBatchRepository batches,
    IExpenseCategoryRepository categories,
    ICustomerRepository customers) : ILookupService
{
    public async Task<IReadOnlyList<LookupItem>> CompaniesAsync()
    {
        var list = await companies.GetAllAsync(activeOnly: true);
        return list
            .Where(c => user.CompanyId == 0 || c.Id == user.CompanyId)
            .Select(c => new LookupItem { Id = c.Id, Text = c.CompanyName })
            .ToList();
    }

    public async Task<IReadOnlyList<LookupItem>> BusinessUnitsAsync(int? companyId)
    {
        var effectiveCompanyId = EffectiveCompany(companyId);
        if (effectiveCompanyId == 0)
            return [];

        var list = await businessUnits.GetListAsync(effectiveCompanyId, activeOnly: true);
        return list
            .Where(u => user.BusinessUnitId == 0 || u.Id == user.BusinessUnitId)
            .Select(u => new LookupItem { Id = u.Id, Text = u.UnitName })
            .ToList();
    }

    public Task<IReadOnlyList<BatchLookupItem>> BatchesAsync(int? companyId, int? businessUnitId)
    {
        var effectiveCompany = EffectiveCompany(companyId);
        var requestedUnit = businessUnitId.GetValueOrDefault();
        if (requestedUnit == 0 && effectiveCompany == user.Scope.CompanyId)
            requestedUnit = user.Scope.BusinessUnitId; // default to the unit picked in the top bar

        var scope = new TenantScope(
            effectiveCompany,
            user.BusinessUnitId != 0 ? user.BusinessUnitId : requestedUnit);

        // SuperAdmin without a company selected: nothing meaningful to show yet.
        if (scope.IsAllCompanies)
            return Task.FromResult<IReadOnlyList<BatchLookupItem>>([]);

        return batches.GetLookupAsync(scope.CompanyId, scope.BusinessUnitId);
    }

    public async Task<IReadOnlyList<LookupItem>> ExpenseCategoriesAsync(int? companyId)
    {
        var list = await categories.GetListAsync(EffectiveCompany(companyId));
        return list.Select(c => new LookupItem { Id = c.Id, Text = c.CategoryName }).ToList();
    }

    public Task<IReadOnlyList<CustomerListItem>> SearchCustomersAsync(string term, int? companyId = null, int? businessUnitId = null)
    {
        term = (term ?? string.Empty).Trim();
        if (term.Length < 2)
            return Task.FromResult<IReadOnlyList<CustomerListItem>>([]);
        if (term.Length > 60)
            term = term[..60];

        // Narrow only: a value outside the user's own window is ignored.
        var scope = user.Scope;
        var company = scope.CompanyId != 0 ? scope.CompanyId : companyId.GetValueOrDefault();
        var unit = scope.BusinessUnitId != 0 ? scope.BusinessUnitId : businessUnitId.GetValueOrDefault();
        if (company == 0) unit = 0;

        return customers.SearchAsync(new TenantScope(company, unit), term);
    }

    public async Task<decimal> DefaultExchangeRateAsync(int? companyId)
    {
        var effectiveCompanyId = EffectiveCompany(companyId);
        if (effectiveCompanyId == 0)
            return 0;

        var company = await companies.GetByIdAsync(effectiveCompanyId);
        return company?.DefaultExchangeRate ?? 0;
    }

    public async Task<CurrencySettings> CurrencySettingsAsync()
    {
        var companyId = user.Scope.CompanyId;
        var list = new List<Company>();
        if (companyId != 0)
        {
            if (await companies.GetByIdAsync(companyId) is { } one)
                list.Add(one);
        }
        else
        {
            list.AddRange(await companies.GetAllAsync(activeOnly: true));
        }

        var pairs = list
            .Select(c => (Purchase: (c.PurchaseCurrency ?? "").Trim().ToUpperInvariant(), Sales: (c.SalesCurrency ?? "").Trim().ToUpperInvariant()))
            .Distinct()
            .ToList();

        if (pairs.Count == 0)
            return CurrencySettings.Default;

        var first = pairs[0];
        return new CurrencySettings(
            Currencies.Find(first.Purchase, Currencies.Sgd),
            Currencies.Find(first.Sales, Currencies.Bdt),
            IsMixed: pairs.Count > 1);
    }

    /// <summary>
    /// A company-bound user can only ever ask for his own company. A SuperAdmin gets the
    /// company he asked for, else the one picked in the top bar (0 = none picked).
    /// </summary>
    private int EffectiveCompany(int? requested) =>
        user.CompanyId != 0 ? user.CompanyId
        : requested is > 0 ? requested.Value
        : user.Scope.CompanyId;
}
