namespace Ordex.Core.Common;

/// <summary>Columns every table shares: key, soft-delete flag and audit trail.</summary>
public abstract class AuditableEntity
{
    public int Id { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// A record that belongs to one company and one business unit.
/// Every master/transaction table derives from this.
/// </summary>
public abstract class TenantEntity : AuditableEntity
{
    public int CompanyId { get; set; }
    public int BusinessUnitId { get; set; }
}
