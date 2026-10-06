using System.Data;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Common;
using Ordex.Core.Models;
using Ordex.Infrastructure.Data;

namespace Ordex.Infrastructure.Repositories;

/// <summary>Reporting reads go through stored procedures (see /database/003_StoredProcedures.sql).</summary>
public sealed class ReportRepository(DbSession session) : RepositoryBase(session), IReportRepository
{
    public Task<DashboardData> GetDashboardAsync(TenantScope scope, DateTime fromDate, DateTime toDate) =>
        QueryMultipleAsync("dbo.usp_Dashboard_Summary",
            new { scope.CompanyId, scope.BusinessUnitId, FromDate = fromDate.Date, ToDate = toDate.Date },
            async grid => new DashboardData
            {
                Kpi = await grid.ReadSingleOrDefaultAsync<DashboardKpi>() ?? new DashboardKpi(),
                Trend = (await grid.ReadAsync<MonthlyTrendItem>()).ToList(),
                RecentOrders = (await grid.ReadAsync<RecentOrderItem>()).ToList()
            },
            CommandType.StoredProcedure);

    public Task<ProfitLossReport> GetProfitLossAsync(TenantScope scope, DateTime fromDate, DateTime toDate) =>
        QueryMultipleAsync("dbo.usp_Report_ProfitLoss",
            new { scope.CompanyId, scope.BusinessUnitId, FromDate = fromDate.Date, ToDate = toDate.Date },
            async grid => new ProfitLossReport
            {
                Summary = await grid.ReadSingleOrDefaultAsync<FinanceSummary>() ?? new FinanceSummary(),
                ExpenseByCategory = (await grid.ReadAsync<ExpenseByCategory>()).ToList(),
                Batches = (await grid.ReadAsync<BatchListItem>()).ToList()
            },
            CommandType.StoredProcedure);

    public Task<PayableReport> GetMonthlyPayableAsync(TenantScope scope, int year) =>
        QueryMultipleAsync("dbo.usp_Report_MonthlyPayable",
            new { scope.CompanyId, scope.BusinessUnitId, Year = year },
            async grid => new PayableReport
            {
                Year = year,
                Months = (await grid.ReadAsync<PayableMonth>()).ToList(),
                Batches = (await grid.ReadAsync<BatchListItem>()).ToList(),
                Unassigned = await grid.ReadSingleOrDefaultAsync<UnassignedOrders>() ?? new UnassignedOrders()
            },
            CommandType.StoredProcedure);
}
