namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// One page of a filtered list. Page size is fixed at 10 everywhere that lists
    /// oracles, flows, or game systems.
    /// </summary>
    public class PagedList<T>
    {
        public const int DefaultPageSize = 10;

        public List<T> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = DefaultPageSize;
        public int TotalCount { get; set; }

        public int TotalPages => TotalCount <= 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
        public int To => Math.Min(Page * PageSize, TotalCount);

        public bool HasPrevious => Page > 1;
        public bool HasNext => TotalPages > 0 && Page < TotalPages;

        public static int ClampPage(int page, int totalCount, int pageSize = DefaultPageSize)
        {
            if (page < 1)
            {
                page = 1;
            }

            var totalPages = totalCount <= 0
                ? 1
                : (int)Math.Ceiling(totalCount / (double)pageSize);

            return page > totalPages ? totalPages : page;
        }
    }
}
