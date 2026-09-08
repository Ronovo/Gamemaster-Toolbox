using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Services
{
    public static class OracleListQuery
    {
        public static IQueryable<Oracle> ApplyFilters(
            IQueryable<Oracle> oracles,
            string? name,
            int? gameSystemId,
            string? type)
        {
            var term = name?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                var needle = term.ToLower();
                oracles = oracles.Where(o => o.Name.ToLower().Contains(needle));
            }

            if (gameSystemId is > 0)
            {
                oracles = oracles.Where(o => o.GameSystemID == gameSystemId.Value);
            }

            var kind = type?.Trim();
            if (!string.IsNullOrEmpty(kind))
            {
                if (Enum.TryParse<OracleSubtype>(kind, ignoreCase: true, out var subtype)
                    && Enum.IsDefined(subtype))
                {
                    oracles = oracles.Where(o => o.OracleSubtype == subtype);
                }
                else
                {
                    var spec = DiceSpec.Parse(kind);
                    if (spec == null)
                    {
                        oracles = oracles.Where(o => false);
                    }
                    else
                    {
                        oracles = oracles.Where(o =>
                            o.OracleSubtype == OracleSubtype.Dice
                            && o.DiceCount == spec.Count
                            && o.DiceFaces == spec.Faces);
                    }
                }
            }

            return oracles;
        }

        public static async Task<List<(int Count, int Faces)>> DiceGeometriesAsync(IQueryable<Oracle> oracles)
        {
            var rows = await oracles
                .Where(o => o.OracleSubtype == OracleSubtype.Dice
                    && o.DiceCount != null
                    && o.DiceFaces != null)
                .Select(o => new { Count = o.DiceCount!.Value, Faces = o.DiceFaces!.Value })
                .Distinct()
                .ToListAsync();

            return rows.Select(r => (r.Count, r.Faces)).ToList();
        }

        public static Task<List<OracleListItem>> ToListItemsAsync(IQueryable<Oracle> oracles)
        {
            return Project(oracles.OrderBy(o => o.Name)).ToListAsync();
        }

        public static async Task<PagedList<OracleListItem>> ToPagedListAsync(
            IQueryable<Oracle> oracles,
            int page,
            int pageSize = PagedList<OracleListItem>.DefaultPageSize)
        {
            var totalCount = await oracles.CountAsync();
            page = PagedList<OracleListItem>.ClampPage(page, totalCount, pageSize);

            var items = await Project(
                    oracles
                        .OrderBy(o => o.Name)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize))
                .ToListAsync();

            return new PagedList<OracleListItem>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public static Task<List<int>> MatchingIdsAsync(IQueryable<Oracle> oracles)
        {
            return oracles.OrderBy(o => o.Name).Select(o => o.ID).ToListAsync();
        }

        private static IQueryable<OracleListItem> Project(IQueryable<Oracle> oracles)
        {
            return oracles.Select(o => new OracleListItem
            {
                ID = o.ID,
                Name = o.Name,
                OracleSubtype = o.OracleSubtype,
                DiceCount = o.DiceCount,
                DiceFaces = o.DiceFaces,
                GameSystemName = o.GameSystem!.Name,
                EntryCount = o.OracleEntries.Count,
                HasUnbandedEntries = o.OracleEntries.Any(e => e.RangeStart == null)
            });
        }
    }
}
