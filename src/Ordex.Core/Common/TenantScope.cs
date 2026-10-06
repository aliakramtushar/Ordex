namespace Ordex.Core.Common;

/// <summary>
/// The data window a user is allowed to see.
/// 0 means "all": SuperAdmin = (0, 0), company admin = (CompanyId, 0).
/// Every repository query is filtered by this value.
/// </summary>
public readonly record struct TenantScope(int CompanyId, int BusinessUnitId)
{
    public static TenantScope All => new(0, 0);

    public bool IsAllCompanies => CompanyId == 0;
    public bool IsAllUnits => BusinessUnitId == 0;

    /// <summary>True when this scope is allowed to see a record of the given company/unit.</summary>
    public bool Covers(int companyId, int businessUnitId) =>
        (IsAllCompanies || CompanyId == companyId) &&
        (IsAllUnits || BusinessUnitId == businessUnitId);
}
