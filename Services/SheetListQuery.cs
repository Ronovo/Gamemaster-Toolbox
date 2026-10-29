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
            sheets = sheets.Containing(s => s.FileName, name);

            if (gameSystemId is > 0)
            {
                sheets = sheets.Where(s => s.GameSystemID == gameSystemId.Value);
            }

            return sheets;
        }

        public static Task<PagedList<SheetListItem>> ToPagedListAsync(
            IQueryable<Sheet> sheets,
            int page,
            int pageSize = PagedList<SheetListItem>.DefaultPageSize)
        {
            return PagedList<SheetListItem>.CreateAsync(sheets.OrderBy(s => s.FileName), Project, page, pageSize);
        }

        private static IQueryable<SheetListItem> Project(IQueryable<Sheet> sheets)
        {
            return sheets.Select(s => new SheetListItem
            {
                ID = s.ID,
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
