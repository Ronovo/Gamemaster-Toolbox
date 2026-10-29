namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// Route values plus page numbers for the shared pager partial.
    /// </summary>
    public class PagerModel
    {
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public int From { get; set; }
        public int To { get; set; }

        public string Action { get; set; } = "Index";
        public string? Controller { get; set; }
        public string PageParam { get; set; } = "page";
        public Dictionary<string, string?> Route { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static PagerModel Create<T>(
            PagedList<T> list,
            string action,
            Dictionary<string, string?> route,
            string pageParam = "page",
            string? controller = null)
        {
            return new PagerModel
            {
                Page = list.Page,
                TotalPages = list.TotalPages,
                TotalCount = list.TotalCount,
                From = list.From,
                To = list.To,
                Action = action,
                Controller = controller,
                PageParam = pageParam,
                Route = new Dictionary<string, string?>(route, StringComparer.OrdinalIgnoreCase)
            };
        }

        public Dictionary<string, string?> RouteWithPage(int page)
        {
            var copy = new Dictionary<string, string?>(Route, StringComparer.OrdinalIgnoreCase);
            if (page <= 1)
            {
                copy.Remove(PageParam);
            }
            else
            {
                copy[PageParam] = page.ToString();
            }

            foreach (var key in copy.Keys.ToList())
            {
                if (string.IsNullOrWhiteSpace(copy[key]))
                {
                    copy.Remove(key);
                }
            }

            return copy;
        }
    }
}
