using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Models;

namespace Ordex.Core.Services;

public sealed class CustomerService(
    ICustomerRepository customers,
    ICurrentUser user,
    IClock clock) : ICustomerService
{
    public Task<PagedResult<CustomerListItem>> GetPagedAsync(CustomerFilter filter) =>
        customers.GetPagedAsync(user.Scope, filter);

    public Task<Customer?> GetAsync(int id) => customers.GetByIdAsync(id, user.Scope);

    public async Task<CustomerInput?> GetForEditAsync(int id)
    {
        var c = await customers.GetByIdAsync(id, user.Scope);
        return c is null ? null : new CustomerInput
        {
            Id = c.Id,
            CustomerName = c.CustomerName,
            Mobile = c.Mobile,
            SocialLink = c.SocialLink,
            Address = c.Address
        };
    }

    public async Task<ServiceResult> UpdateAsync(CustomerInput input)
    {
        var customer = await customers.GetByIdAsync(input.Id, user.Scope);
        if (customer is null)
            return ServiceResult.Fail(Messages.NotFound);

        var mobile = TextHelper.NormalizeMobile(input.Mobile);
        var sameMobile = await customers.FindByMobileAsync(customer.CompanyId, customer.BusinessUnitId, mobile);
        if (sameMobile is not null && sameMobile.Id != customer.Id)
            return ServiceResult.Fail($"Another customer ({sameMobile.CustomerName}) already uses this mobile number.");

        customer.CustomerName = input.CustomerName.Trim();
        customer.Mobile = mobile;
        customer.SocialLink = TextHelper.Clean(input.SocialLink);
        customer.Address = TextHelper.Clean(input.Address);
        customer.UpdatedBy = user.UserId;
        customer.UpdatedAt = clock.Now;

        await customers.UpdateAsync(customer);
        return ServiceResult.Ok();
    }
}

public sealed class BatchService(
    IBatchRepository batches,
    ICompanyRepository companies,
    IDocumentNumberRepository documentNumbers,
    IScopeService scopeService,
    IUnitOfWork unitOfWork,
    ICurrentUser user,
    IClock clock) : IBatchService
{
    private const string BatchPrefix = "BAT";

    public Task<PagedResult<BatchListItem>> GetPagedAsync(BatchFilter filter) =>
        batches.GetPagedAsync(user.Scope, filter);

    public Task<BatchListItem?> GetTotalsAsync(int id) => batches.GetTotalsAsync(id, user.Scope);

    public async Task<BatchInput> NewAsync()
    {
        var rate = 0m;
        if (user.CompanyId != 0)
            rate = (await companies.GetByIdAsync(user.CompanyId))?.DefaultExchangeRate ?? 0;

        return new BatchInput { BatchDate = clock.Today, ExchangeRate = rate };
    }

    public async Task<BatchInput?> GetForEditAsync(int id)
    {
        var b = await batches.GetByIdAsync(id, user.Scope);
        return b is null ? null : new BatchInput
        {
            Id = b.Id,
            CompanyId = b.CompanyId,
            BusinessUnitId = b.BusinessUnitId,
            BatchNo = b.BatchNo,
            BatchDate = b.BatchDate,
            ExchangeRate = b.ExchangeRate,
            Notes = b.Notes
        };
    }

    public async Task<ServiceResult<int>> SaveAsync(BatchInput input)
    {
        PurchaseBatch batch;

        if (input.IsNew)
        {
            var resolved = await scopeService.ResolveForCreateAsync(input.CompanyId, input.BusinessUnitId);
            if (!resolved.Succeeded)
                return ServiceResult<int>.Fail(resolved.Error!);

            batch = new PurchaseBatch
            {
                CompanyId = resolved.Value.CompanyId,
                BusinessUnitId = resolved.Value.BusinessUnitId,
                CreatedBy = user.UserId,
                CreatedAt = clock.Now
            };
        }
        else
        {
            var existing = await batches.GetByIdAsync(input.Id, user.Scope);
            if (existing is null)
                return ServiceResult<int>.Fail(Messages.NotFound);

            batch = existing;
            batch.UpdatedBy = user.UserId;
            batch.UpdatedAt = clock.Now;
        }

        var batchNo = TextHelper.Clean(input.BatchNo);
        if (batchNo is not null &&
            await batches.BatchNoExistsAsync(batch.CompanyId, batch.BusinessUnitId, batchNo, batch.Id))
            return ServiceResult<int>.Fail("This batch number already exists.");

        batch.BatchDate = input.BatchDate.Date;
        batch.ExchangeRate = input.ExchangeRate;
        batch.Notes = TextHelper.Clean(input.Notes);

        var id = await unitOfWork.ExecuteAsync(async () =>
        {
            batch.BatchNo = batchNo ?? (batch.Id == 0 || string.IsNullOrEmpty(batch.BatchNo)
                ? await documentNumbers.NextAsync(batch.CompanyId, BatchPrefix, batch.BatchDate)
                : batch.BatchNo);

            if (batch.Id == 0)
                return await batches.InsertAsync(batch);

            await batches.UpdateAsync(batch);
            return batch.Id;
        });

        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult> SetPaidAsync(int id, bool isPaid)
    {
        var batch = await batches.GetByIdAsync(id, user.Scope);
        if (batch is null)
            return ServiceResult.Fail(Messages.NotFound);

        await batches.SetPaidAsync(id, isPaid, isPaid ? clock.Today : null, user.UserId, clock.Now);
        return ServiceResult.Ok();
    }
}
