using GM_Toolbox.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// A list page filtered by name and game system: oracles, flows and sheets.
    /// </summary>
    public class FilteredIndexViewModel<T>
    {
        public string? Name { get; set; }

        [Display(Name = "Game System")]
        public int? GameSystemID { get; set; }

        public bool CatalogEmpty { get; set; }
        public PagedList<T> Results { get; set; } = new();
        public IEnumerable<SelectListItem> GameSystems { get; set; } = Array.Empty<SelectListItem>();

        public virtual bool HasFilters =>
            !string.IsNullOrWhiteSpace(Name)
            || GameSystemID.HasValue;

        public virtual Dictionary<string, string?> FilterRoute => new(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = Name,
            ["GameSystemID"] = GameSystemID?.ToString()
        };

        /// <summary>Sets the filters, the game system picker and whether there is anything to list at all.</summary>
        public async Task FillAsync<TEntity>(string? name, int? gameSystemId, IQueryable<GameSystem> systems,
            IQueryable<TEntity> everything)
        {
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            GameSystemID = gameSystemId is > 0 ? gameSystemId : null;
            GameSystems = FilterSelectLists.GameSystems(await systems.OrderBy(g => g.Name).ToListAsync());
            CatalogEmpty = !await everything.AnyAsync();
        }
    }
}
