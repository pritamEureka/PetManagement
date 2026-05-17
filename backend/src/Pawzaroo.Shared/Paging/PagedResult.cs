namespace Pawzaroo.Shared.Paging;

public record PageRequest(int Page = 1, int PageSize = 20)
{
    public int Skip => (Math.Max(1, Page) - 1) * Math.Clamp(PageSize, 1, 100);
    public int Take => Math.Clamp(PageSize, 1, 100);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total)
{
    public int TotalPages => (int)Math.Ceiling((double)Total / Math.Max(1, PageSize));
}
