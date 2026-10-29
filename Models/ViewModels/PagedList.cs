using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// One page of a filtered list. Page size is fixed at 10 everywhere.
    /// </summary>
    public class PagedList<T>
    {
        public const int DefaultPageSize = 10;

        public List<T> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int PageSize => DefaultPageSize;
        public int TotalCount { get; set; }

        public int TotalPages => TotalCount <= 0
            ? 0
            : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public int From => TotalCount == 0 ? 0 : (Page - 1) * PageSize + 1;
        public int To => Math.Min(Page * PageSize, TotalCount);

        public bool HasPrevious => Page > 1;
        public bool HasNext => TotalPages > 0 && Page < TotalPages;

        /// <summary>
        /// One page of an ordered query, run through project so only that page's rows are read.
        /// </summary>
        public static async Task<PagedList<T>> CreateAsync<TSource>(IOrderedQueryable<TSource> ordered,
            Func<IQueryable<TSource>, IQueryable<T>> project, int page)
        {
            var totalCount = await ordered.CountAsync();
            page = ClampPage(page, totalCount);
            var items = await project(ordered.Skip((page - 1) * DefaultPageSize).Take(DefaultPageSize)).ToListAsync();
            return new PagedList<T> { Items = items, Page = page, TotalCount = totalCount };
        }

        public PagedList<TOut> WithItems<TOut>(List<TOut> items) =>
            new() { Items = items, Page = Page, TotalCount = TotalCount };

        private static int ClampPage(int page, int totalCount)
        {
            if (page < 1)
            {
                page = 1;
            }

            var totalPages = totalCount <= 0
                ? 1
                : (int)Math.Ceiling(totalCount / (double)DefaultPageSize);

            return page > totalPages ? totalPages : page;
        }
    }
}
