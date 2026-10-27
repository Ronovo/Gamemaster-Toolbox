using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
    public class SheetIndexViewModel
    {
        public string? Name { get; set; }

        [Display(Name = "Game System")]
        public int? GameSystemID { get; set; }

        public bool CatalogEmpty { get; set; }
        public PagedList<SheetListItem> Results { get; set; } = new();
        public IEnumerable<SelectListItem> GameSystems { get; set; } = Array.Empty<SelectListItem>();

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(Name)
            || GameSystemID.HasValue;

        public Dictionary<string, string?> FilterRoute => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Name,
            ["GameSystemID"] = GameSystemID?.ToString()
        };
    }
}
