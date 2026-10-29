using GM_Toolbox.Models;
using GM_Toolbox.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Services
{
    public static class SheetListQuery
    {
        public static IQueryable<Sheet> ApplyFilters(
            IQueryable<Sheet> sheets,
            string? name,
            int? gameSystemId)
        {
            var term = name?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                var needle = term.ToLower();
                sheets = sheets.Where(s => s.FileName.ToLower().Contains(needle));
            }

            if (gameSystemId is > 0)
            {
                sheets = sheets.Where(s => s.GameSystemID == gameSystemId.Value);
            }

            return sheets;
        }

        public static async Task<PagedList<SheetListItem>> ToPagedListAsync(
            IQueryable<Sheet> sheets,
            int page,
            int pageSize = PagedList<SheetListItem>.DefaultPageSize)
        {
            var totalCount = await sheets.CountAsync();
            page = PagedList<SheetListItem>.ClampPage(page, totalCount, pageSize);

            var items = await Project(
                    sheets
                        .OrderBy(s => s.FileName)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize))
                .ToListAsync();

            return new PagedList<SheetListItem>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        private static IQueryable<SheetListItem> Project(IQueryable<Sheet> sheets)
        {
            return sheets.Select(s => new SheetListItem
            {
                ID = s.ID,
                OwnerId = s.OwnerId,
                FileName = s.FileName,
                GameSystemName = s.GameSystem!.Name,
                DateUploaded = s.DateUploaded,
                DateUpdated = s.DateUpdated,
                CurrentVersion = s.CurrentVersion,
                CurrentChecksum = s.Versions.First(v => v.Version == s.CurrentVersion).Checksum
            });
        }
    }
}
