using Ordex.Core.Abstractions;
using Ordex.Core.Abstractions.Repositories;
using Ordex.Core.Abstractions.Services;
using Ordex.Core.Common;
using Ordex.Core.Entities;
using Ordex.Core.Enums;
using Ordex.Core.Models;

namespace Ordex.Core.Services;

/// <summary>
/// Stock = extra products bought to balance the luggage weight + returned items.
/// Returned items are created automatically by <see cref="OrderService"/>;
/// here we add extras, sell items and remove mistakes.
/// </summary>
public sealed class StockService(
    IStockRepository stock,
    IBatchRepository batches,
    IScopeService scopeService,
    IFileStorage storage,
    ICurrentUser user,
    IClock clock) : IStockService
{
    public Task<PagedResult<StockListItem>> GetPagedAsync(StockFilter filter) =>
        stock.GetPagedAsync(user.Scope, filter);

    public Task<StockTabCounts> GetTabCountsAsync() => stock.GetTabCountsAsync(user.Scope);

    public Task<StockInput> NewAsync() => Task.FromResult(new StockInput { EntryDate = clock.Today });

    public async Task<StockInput?> GetForEditAsync(int id)
    {
        var s = await stock.GetByIdAsync(id, user.Scope);
        if (s is null || s.SourceType != StockSource.Extra)
            return null;

        return new StockInput
        {
            Id = s.Id,
            CompanyId = s.CompanyId,
            BusinessUnitId = s.BusinessUnitId,
            EntryDate = s.EntryDate,
            BatchId = s.BatchId,
            ProductName = s.ProductName,
            ProductImage = s.ProductImage,
            ProductLink = s.ProductLink,
            ProductSize = s.ProductSize,
            PriceSgd = s.PriceSgd,
            PriceBdt = s.PriceBdt,
            Notes = s.Notes
        };
    }

    public async Task<ServiceResult<int>> SaveExtraAsync(StockInput input, UploadedFile? image)
    {
        StockItem item;
        TenantScope scope;

        if (input.IsNew)
        {
            var resolved = await scopeService.ResolveForCreateAsync(input.CompanyId, input.BusinessUnitId);
            if (!resolved.Succeeded)
                return ServiceResult<int>.Fail(resolved.Error!);

            scope = resolved.Value;
            item = new StockItem
            {
                CompanyId = scope.CompanyId,
                BusinessUnitId = scope.BusinessUnitId,
                SourceType = StockSource.Extra,
                Status = StockStatus.InStock,
                CreatedBy = user.UserId,
                CreatedAt = clock.Now
            };
        }
        else
        {
            var existing = await stock.GetByIdAsync(input.Id, user.Scope);
            if (existing is null)
                return ServiceResult<int>.Fail(Messages.NotFound);
            if (existing.SourceType != StockSource.Extra)
                return ServiceResult<int>.Fail(Messages.StockReturnLocked);

            item = existing;
            scope = new TenantScope(item.CompanyId, item.BusinessUnitId);
            item.UpdatedBy = user.UserId;
            item.UpdatedAt = clock.Now;
        }

        if (input.BatchId is { } batchId && await batches.GetByIdAsync(batchId, scope) is null)
            return ServiceResult<int>.Fail(Messages.InvalidBatch);

        var imageResult = await ImageChange.PrepareAsync(
            storage, item.ProductImage, input.RemoveImage, image, $"{scope.CompanyId}/stock");
        if (!imageResult.Succeeded)
            return ServiceResult<int>.Fail(imageResult.Error!);

        var imageChange = imageResult.Value!;

        item.EntryDate = input.EntryDate.Date;
        item.BatchId = input.BatchId;
        item.ProductName = input.ProductName.Trim();
        item.ProductImage = imageChange.FinalPath;
        item.ProductLink = TextHelper.Clean(input.ProductLink);
        item.ProductSize = TextHelper.Clean(input.ProductSize);
        item.PriceSgd = input.PriceSgd;
        item.PriceBdt = input.PriceBdt;
        item.Notes = TextHelper.Clean(input.Notes);

        try
        {
            if (item.Id == 0)
                item.Id = await stock.InsertAsync(item);
            else
                await stock.UpdateAsync(item);
        }
        catch
        {
            imageChange.Rollback();
            throw;
        }

        imageChange.Commit();
        return ServiceResult<int>.Ok(item.Id);
    }

    public async Task<ServiceResult> SellAsync(StockSaleInput input)
    {
        var item = await stock.GetByIdAsync(input.Id, user.Scope);
        if (item is null)
            return ServiceResult.Fail(Messages.NotFound);
        if (item.Status == StockStatus.Sold)
            return ServiceResult.Fail(Messages.StockAlreadySold);

        item.Status = StockStatus.Sold;
        item.SoldPrice = input.SoldPrice;
        item.SoldDate = input.SoldDate.Date;
        item.SoldTo = TextHelper.Clean(input.SoldTo);
        item.UpdatedBy = user.UserId;
        item.UpdatedAt = clock.Now;

        return await stock.UpdateSaleAsync(item, StockStatus.InStock)
            ? ServiceResult.Ok()
            : ServiceResult.Fail(Messages.Conflict);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var item = await stock.GetByIdAsync(id, user.Scope);
        if (item is null)
            return ServiceResult.Fail(Messages.NotFound);
        if (item.SourceType != StockSource.Extra || item.Status != StockStatus.InStock)
            return ServiceResult.Fail("Only unsold extra items can be deleted.");

        await stock.SoftDeleteAsync(id, user.UserId, clock.Now);
        return ServiceResult.Ok();
    }
}
