using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
    public class OracleIndexViewModel : FilteredIndexViewModel<OracleListItem>
    {
        [Display(Name = "Oracle Type")]
        public string? OracleType { get; set; }

        public IEnumerable<SelectListItem> Subtypes { get; set; } = Array.Empty<SelectListItem>();

        public override bool HasFilters => base.HasFilters || !string.IsNullOrWhiteSpace(OracleType);

        public override Dictionary<string, string?> FilterRoute
        {
            get
            {
                var route = base.FilterRoute;
                route["OracleType"] = OracleType;
                return route;
            }
        }
    }
}
