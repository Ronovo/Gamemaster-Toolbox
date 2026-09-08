using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
    public class OracleIndexViewModel
    {
        public string? Name { get; set; }

        [Display(Name = "Game System")]
        public int? GameSystemID { get; set; }

        [Display(Name = "Oracle Type")]
        public string? OracleType { get; set; }

        public bool CatalogEmpty { get; set; }
        public PagedList<OracleListItem> Results { get; set; } = new();
        public IEnumerable<SelectListItem> GameSystems { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> Subtypes { get; set; } = Array.Empty<SelectListItem>();

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(Name)
            || GameSystemID.HasValue
            || !string.IsNullOrWhiteSpace(OracleType);

        public Dictionary<string, string?> FilterRoute => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Name,
            ["GameSystemID"] = GameSystemID?.ToString(),
            ["OracleType"] = OracleType
        };
    }
}
