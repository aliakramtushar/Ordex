using System.Data;
using Dapper;
using Ordex.Core.Common;

namespace Ordex.Infrastructure.Data;

/// <summary>
/// Thin helpers over Dapper. Every call automatically uses the request's
/// connection and the current transaction (if any), so repositories never
/// have to think about it.
/// </summary>
public abstract class RepositoryBase(DbSession session)
{
    /// <summary>
    /// Standard tenant filter. Needs @CompanyId and @BusinessUnitId parameters
    /// (see <see cref="ScopeParameters"/>). 0 = all.
    /// </summary>
    protected static string ScopeFilter(string alias) =>
        $"(@CompanyId = 0 OR {alias}.CompanyId = @CompanyId) AND (@BusinessUnitId = 0 OR {alias}.BusinessUnitId = @BusinessUnitId)";

    protected static DynamicParameters ScopeParameters(TenantScope scope)
    {
        var p = new DynamicParameters();
        p.Add("CompanyId", scope.CompanyId);
        p.Add("BusinessUnitId", scope.BusinessUnitId);
        return p;
    }

    protected async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param = null, CommandType commandType = CommandType.Text)
    {
        var connection = await session.GetConnectionAsync();
        var rows = await connection.QueryAsync<T>(Command(sql, param, commandType));
        return rows.AsList();
    }

    protected async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null)
    {
        var connection = await session.GetConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<T>(Command(sql, param));
    }

    protected async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        var connection = await session.GetConnectionAsync();
        return await connection.ExecuteAsync(Command(sql, param));
    }

    protected async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        var connection = await session.GetConnectionAsync();
        return await connection.ExecuteScalarAsync<T>(Command(sql, param));
    }

    /// <summary>Runs a query (or stored procedure) that returns several result sets.</summary>
    protected async Task<TResult> QueryMultipleAsync<TResult>(
        string sql, object? param, Func<SqlMapper.GridReader, Task<TResult>> read, CommandType commandType = CommandType.Text)
    {
        var connection = await session.GetConnectionAsync();
        using var grid = await connection.QueryMultipleAsync(Command(sql, param, commandType));
        return await read(grid);
    }

    /// <summary>
    /// Server-side paging in one round trip: total count + one page of rows (OFFSET / FETCH).
    /// <paramref name="orderBy"/> must be a constant from code – never user input; it should end
    /// with a unique column (e.g. Id) so rows never repeat or go missing between pages.
    /// <para>
    /// OPTION (RECOMPILE): the tenant filter "(@CompanyId = 0 OR CompanyId = @CompanyId)" and optional
    /// search filters are "catch-all" predicates. Recompiling lets SQL Server plan for the actual values,
    /// so a staff user's query seeks its unit's index instead of reusing a plan built for "all companies".
    /// The compile cost (~ms) is tiny next to scanning a large table.
    /// </para>
    /// </summary>
    protected async Task<PagedResult<T>> QueryPagedAsync<T>(
        string select, string fromWhere, string orderBy, DynamicParameters param, PagedQuery query)
    {
        param.Add("Offset", query.Offset);
        param.Add("PageSize", query.PageSize);

        var sql = $"""
            SELECT COUNT(1) {fromWhere}
            OPTION (RECOMPILE);

            SELECT {select}
            {fromWhere}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            OPTION (RECOMPILE);
            """;

        var result = await QueryMultipleAsync(sql, param, async grid =>
        {
            var total = await grid.ReadSingleAsync<int>();
            var items = (await grid.ReadAsync<T>()).AsList();
            return new PagedResult<T>(items, total, query.Page, query.PageSize);
        });

        // A page past the end (old bookmark, rows deleted meanwhile): show the last real page instead of an empty list.
        if (result.Items.Count == 0 && result.TotalCount > 0 && query.Page > result.TotalPages)
        {
            query.Page = result.TotalPages;
            return await QueryPagedAsync<T>(select, fromWhere, orderBy, param, query);
        }

        return result;
    }

    private CommandDefinition Command(string sql, object? param, CommandType commandType = CommandType.Text) =>
        new(sql, param, session.Transaction, session.CommandTimeout, commandType);
}
