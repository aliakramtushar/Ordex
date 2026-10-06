using Dapper;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Models;
using Ordex.Infrastructure.Data;

namespace Ordex.Infrastructure.Repositories;

public sealed class CompanyRepository(DbSession session) : RepositoryBase(session), ICompanyRepository
{
    private const string Columns = """
        Id, CompanyCode, CompanyName, Phone, Email, Address, DefaultExchangeRate,
        PurchaseCurrency, SalesCurrency, IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
        """;

    public Task<IReadOnlyList<Company>> GetAllAsync(bool activeOnly = false) =>
        QueryAsync<Company>($"""
            SELECT {Columns}
            FROM dbo.Company
            WHERE (@ActiveOnly = 0 OR IsActive = 1)
            ORDER BY CompanyName;
            """, new { ActiveOnly = activeOnly });

    public Task<Company?> GetByIdAsync(int id) =>
        QuerySingleOrDefaultAsync<Company>($"SELECT {Columns} FROM dbo.Company WHERE Id = @Id;", new { Id = id });

    public async Task<bool> CodeExistsAsync(string code, int excludeId = 0) =>
        await ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.Company WHERE CompanyCode = @Code AND Id <> @ExcludeId;",
            new { Code = code, ExcludeId = excludeId }) > 0;

    public async Task<int> InsertAsync(Company c) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.Company (CompanyCode, CompanyName, Phone, Email, Address, DefaultExchangeRate,
                                     PurchaseCurrency, SalesCurrency, IsActive, CreatedBy, CreatedAt)
            VALUES (@CompanyCode, @CompanyName, @Phone, @Email, @Address, @DefaultExchangeRate,
                    @PurchaseCurrency, @SalesCurrency, @IsActive, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, c);

    public Task UpdateAsync(Company c) =>
        ExecuteAsync("""
            UPDATE dbo.Company
               SET CompanyCode = @CompanyCode, CompanyName = @CompanyName, Phone = @Phone, Email = @Email,
                   Address = @Address, DefaultExchangeRate = @DefaultExchangeRate,
                   PurchaseCurrency = @PurchaseCurrency, SalesCurrency = @SalesCurrency, IsActive = @IsActive,
                   UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id;
            """, c);

    public Task SetActiveAsync(int id, bool isActive, int updatedBy, DateTime updatedAt) =>
        ExecuteAsync("UPDATE dbo.Company SET IsActive = @IsActive, UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt WHERE Id = @Id;",
            new { Id = id, IsActive = isActive, UpdatedBy = updatedBy, UpdatedAt = updatedAt });

    public async Task<bool> HasDataAsync(int id) =>
        await ExecuteScalarAsync<int>("""
            SELECT CASE WHEN
                   EXISTS (SELECT 1 FROM dbo.AppUser         WHERE CompanyId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.Customer        WHERE CompanyId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.PurchaseBatch   WHERE CompanyId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.PreOrder        WHERE CompanyId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.StockItem       WHERE CompanyId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.Expense         WHERE CompanyId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.ExpenseCategory WHERE CompanyId = @Id)
            THEN 1 ELSE 0 END;
            """, new { Id = id }) == 1;

    public Task DeleteAsync(int id) =>
        ExecuteAsync("""
            DELETE FROM dbo.DocumentSequence WHERE CompanyId = @Id;
            DELETE FROM dbo.BusinessUnit     WHERE CompanyId = @Id;
            DELETE FROM dbo.Company          WHERE Id = @Id;
            """, new { Id = id });
}

public sealed class BusinessUnitRepository(DbSession session) : RepositoryBase(session), IBusinessUnitRepository
{
    public Task<IReadOnlyList<BusinessUnitListItem>> GetListAsync(int companyId, bool activeOnly = false) =>
        QueryAsync<BusinessUnitListItem>("""
            SELECT bu.Id, bu.CompanyId, c.CompanyName, bu.UnitCode, bu.UnitName, bu.IsActive
            FROM dbo.BusinessUnit bu
            INNER JOIN dbo.Company c ON c.Id = bu.CompanyId
            WHERE (@CompanyId = 0 OR bu.CompanyId = @CompanyId)
              AND (@ActiveOnly = 0 OR bu.IsActive = 1)
            ORDER BY c.CompanyName, bu.UnitName;
            """, new { CompanyId = companyId, ActiveOnly = activeOnly });

    public Task<BusinessUnit?> GetByIdAsync(int id) =>
        QuerySingleOrDefaultAsync<BusinessUnit>("""
            SELECT Id, CompanyId, UnitCode, UnitName, IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
            FROM dbo.BusinessUnit WHERE Id = @Id;
            """, new { Id = id });

    public async Task<bool> CodeExistsAsync(int companyId, string code, int excludeId = 0) =>
        await ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.BusinessUnit WHERE CompanyId = @CompanyId AND UnitCode = @Code AND Id <> @ExcludeId;",
            new { CompanyId = companyId, Code = code, ExcludeId = excludeId }) > 0;

    public async Task<int> InsertAsync(BusinessUnit u) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.BusinessUnit (CompanyId, UnitCode, UnitName, IsActive, CreatedBy, CreatedAt)
            VALUES (@CompanyId, @UnitCode, @UnitName, @IsActive, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, u);

    public Task UpdateAsync(BusinessUnit u) =>
        ExecuteAsync("""
            UPDATE dbo.BusinessUnit
               SET UnitCode = @UnitCode, UnitName = @UnitName, IsActive = @IsActive,
                   UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id;
            """, u);

    public Task SetActiveAsync(int id, bool isActive, int updatedBy, DateTime updatedAt) =>
        ExecuteAsync("UPDATE dbo.BusinessUnit SET IsActive = @IsActive, UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt WHERE Id = @Id;",
            new { Id = id, IsActive = isActive, UpdatedBy = updatedBy, UpdatedAt = updatedAt });

    public async Task<bool> HasDataAsync(int id) =>
        await ExecuteScalarAsync<int>("""
            SELECT CASE WHEN
                   EXISTS (SELECT 1 FROM dbo.AppUser         WHERE BusinessUnitId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.Customer        WHERE BusinessUnitId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.PurchaseBatch   WHERE BusinessUnitId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.PreOrder        WHERE BusinessUnitId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.StockItem       WHERE BusinessUnitId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.Expense         WHERE BusinessUnitId = @Id)
                OR EXISTS (SELECT 1 FROM dbo.ExpenseCategory WHERE BusinessUnitId = @Id)
            THEN 1 ELSE 0 END;
            """, new { Id = id }) == 1;

    public Task DeleteAsync(int id) =>
        ExecuteAsync("DELETE FROM dbo.BusinessUnit WHERE Id = @Id;", new { Id = id });
}

public sealed class UserRepository(DbSession session) : RepositoryBase(session), IUserRepository
{
    private const string Columns = """
        Id, CompanyId, BusinessUnitId, FullName, UserName, Email, Phone, PasswordHash, Role,
        AccessFailedCount, LockoutEnd, LastLoginAt, IsActive, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt
        """;

    public Task<AppUser?> GetByUserNameAsync(string userName) =>
        QuerySingleOrDefaultAsync<AppUser>($"SELECT {Columns} FROM dbo.AppUser WHERE UserName = @UserName;",
            new { UserName = userName });

    public Task<AppUser?> GetByIdAsync(int id) =>
        QuerySingleOrDefaultAsync<AppUser>($"SELECT {Columns} FROM dbo.AppUser WHERE Id = @Id;", new { Id = id });

    public Task<IReadOnlyList<UserListItem>> GetListAsync(TenantScope scope, string? search)
    {
        var p = ScopeParameters(scope);
        p.Add("Search", string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%");

        return QueryAsync<UserListItem>($"""
            SELECT u.Id, u.FullName, u.UserName, u.Email, u.Phone, u.Role, u.CompanyId, u.BusinessUnitId,
                   c.CompanyName, bu.UnitName AS BusinessUnitName, u.IsActive, u.LastLoginAt
            FROM dbo.AppUser u
            LEFT JOIN dbo.Company c       ON c.Id  = u.CompanyId
            LEFT JOIN dbo.BusinessUnit bu ON bu.Id = u.BusinessUnitId
            WHERE {ScopeFilter("u")}
              AND (@Search IS NULL OR u.FullName LIKE @Search OR u.UserName LIKE @Search OR u.Phone LIKE @Search)
            ORDER BY u.Role, u.FullName;
            """, p);
    }

    public async Task<bool> UserNameExistsAsync(string userName, int excludeId = 0) =>
        await ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.AppUser WHERE UserName = @UserName AND Id <> @ExcludeId;",
            new { UserName = userName, ExcludeId = excludeId }) > 0;

    public async Task<bool> AnySuperAdminAsync() =>
        await ExecuteScalarAsync<int>("SELECT COUNT(1) FROM dbo.AppUser WHERE Role = 1;") > 0;

    public async Task<int> InsertAsync(AppUser u) =>
        await ExecuteScalarAsync<int>("""
            INSERT INTO dbo.AppUser (CompanyId, BusinessUnitId, FullName, UserName, Email, Phone, PasswordHash, Role, IsActive, CreatedBy, CreatedAt)
            VALUES (@CompanyId, @BusinessUnitId, @FullName, @UserName, @Email, @Phone, @PasswordHash, @Role, @IsActive, @CreatedBy, @CreatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """, u);

    public Task UpdateAsync(AppUser u) =>
        ExecuteAsync("""
            UPDATE dbo.AppUser
               SET CompanyId = @CompanyId, BusinessUnitId = @BusinessUnitId, FullName = @FullName, UserName = @UserName,
                   Email = @Email, Phone = @Phone, Role = @Role, IsActive = @IsActive,
                   UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id;
            """, u);

    public Task UpdatePasswordAsync(int userId, string passwordHash, int updatedBy, DateTime updatedAt) =>
        ExecuteAsync("""
            UPDATE dbo.AppUser
               SET PasswordHash = @PasswordHash, AccessFailedCount = 0, LockoutEnd = NULL,
                   UpdatedBy = @UpdatedBy, UpdatedAt = @UpdatedAt
             WHERE Id = @Id;
            """, new { Id = userId, PasswordHash = passwordHash, UpdatedBy = updatedBy, UpdatedAt = updatedAt });

    public Task RecordLoginSuccessAsync(int userId, DateTime loginAt) =>
        ExecuteAsync("UPDATE dbo.AppUser SET AccessFailedCount = 0, LockoutEnd = NULL, LastLoginAt = @LoginAt WHERE Id = @Id;",
            new { Id = userId, LoginAt = loginAt });

    public Task RecordLoginFailureAsync(int userId, int failedCount, DateTime? lockoutEnd) =>
        ExecuteAsync("UPDATE dbo.AppUser SET AccessFailedCount = @FailedCount, LockoutEnd = @LockoutEnd WHERE Id = @Id;",
            new { Id = userId, FailedCount = failedCount, LockoutEnd = lockoutEnd });
}

public sealed class DocumentNumberRepository(DbSession session) : RepositoryBase(session), IDocumentNumberRepository
{
    /// <summary>
    /// UPDLOCK + HOLDLOCK serialises callers for the same key, so two users saving
    /// at the same second can never get the same number. Must run inside a transaction.
    /// </summary>
    public async Task<string> NextAsync(int companyId, string prefix, DateTime date)
    {
        var period = date.ToString("yyMM");

        var next = await ExecuteScalarAsync<int>("""
            DECLARE @Next INT;

            UPDATE dbo.DocumentSequence WITH (UPDLOCK, HOLDLOCK)
               SET @Next = LastNumber = LastNumber + 1
             WHERE CompanyId = @CompanyId AND Prefix = @Prefix AND Period = @Period;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.DocumentSequence (CompanyId, Prefix, Period, LastNumber)
                VALUES (@CompanyId, @Prefix, @Period, 1);
                SET @Next = 1;
            END

            SELECT @Next;
            """, new { CompanyId = companyId, Prefix = prefix, Period = period });

        return $"{prefix}-{period}-{next:D4}";
    }
}
