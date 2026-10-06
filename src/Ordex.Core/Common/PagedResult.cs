namespace Ordex.Core.Common;

/// <summary>Base for every list filter that needs paging.</summary>
public abstract class PagedQuery
{
    private int _page = 1;
    private int _pageSize = 20;

    public string? Search { get; set; }

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value is < 1 or > 500 ? 20 : value;
    }

    public int Offset => (Page - 1) * PageSize;

    /// <summary>Search text prepared for a SQL LIKE (null when empty).</summary>
    public string? SearchPattern => string.IsNullOrWhiteSpace(Search) ? null : $"%{Search.Trim()}%";
}

public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    public IReadOnlyList<T> Items { get; }
    public int TotalCount { get; }
    public int Page { get; }
    public int PageSize { get; }

    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
    public int FirstItemNumber => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItemNumber => Math.Min(Page * PageSize, TotalCount);
}
