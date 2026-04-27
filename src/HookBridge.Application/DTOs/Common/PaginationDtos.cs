namespace HookBridge.Application.DTOs.Common;

public class PagedRequestDto
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; } = "desc";

    public int NormalizedPageNumber => PageNumber < 1 ? 1 : PageNumber;

    public int NormalizedPageSize => Math.Clamp(PageSize, 1, 500);

    public string NormalizedSortDirection =>
        string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";

    public int Skip => (NormalizedPageNumber - 1) * NormalizedPageSize;
}

public sealed class PagedResponseDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public long TotalCount { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage { get; set; }

    public bool HasNextPage { get; set; }

    public static PagedResponseDto<T> Create(IReadOnlyList<T> items, int pageNumber, int pageSize, long totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        return new PagedResponseDto<T>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            HasPreviousPage = pageNumber > 1,
            HasNextPage = pageNumber < totalPages,
        };
    }
}
