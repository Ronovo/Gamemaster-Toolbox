namespace GM_Toolbox.Models
{
    public interface ISortable
    {
        int SortOrder { get; set; }
    }

    public static class SortableExtensions
    {
        /// <summary>Numbers the items 1, 2, 3... in the order given.</summary>
        public static void Renumber<T>(this IEnumerable<T> ordered) where T : ISortable
        {
            var n = 1;
            foreach (var item in ordered)
            {
                item.SortOrder = n++;
            }
        }
    }
}
