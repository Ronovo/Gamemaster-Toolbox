namespace GM_Toolbox.Models.ViewModels
{
    public class GameSystemIndexViewModel
    {
        public string? Name { get; set; }
        public string? Genre { get; set; }

        public bool CatalogEmpty { get; set; }
        public PagedList<GameSystem> Results { get; set; } = new();

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(Name)
            || !string.IsNullOrWhiteSpace(Genre);

        public Dictionary<string, string?> FilterRoute => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Name,
            ["Genre"] = Genre
        };
    }
}
