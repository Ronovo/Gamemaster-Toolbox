using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Services
{
    public static class FlowListQuery
    {
        public static IQueryable<Flow> ApplyFilters(
            IQueryable<Flow> flows,
            string? name,
            int? gameSystemId)
        {
            var term = name?.Trim();
            if (!string.IsNullOrEmpty(term))
            {
                var needle = term.ToLower();
                flows = flows.Where(f => f.Name.ToLower().Contains(needle));
            }

            if (gameSystemId is > 0)
            {
                flows = flows.Where(f => f.GameSystemID == gameSystemId.Value);
            }

            return flows;
        }

        public static async Task<List<FlowListItem>> ToListItemsAsync(IQueryable<Flow> flows)
        {
            var rows = await ProjectRows(flows.OrderBy(f => f.Name)).ToListAsync();
            return Map(rows);
        }

        public static async Task<PagedList<FlowListItem>> ToPagedListAsync(
            IQueryable<Flow> flows,
            int page,
            int pageSize = PagedList<FlowListItem>.DefaultPageSize)
        {
            var totalCount = await flows.CountAsync();
            page = PagedList<FlowListItem>.ClampPage(page, totalCount, pageSize);

            var rows = await ProjectRows(
                    flows
                        .OrderBy(f => f.Name)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize))
                .ToListAsync();

            return new PagedList<FlowListItem>
            {
                Items = Map(rows),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public static Task<List<int>> MatchingIdsAsync(IQueryable<Flow> flows)
        {
            return flows.OrderBy(f => f.Name).Select(f => f.ID).ToListAsync();
        }

        private static IQueryable<FlowListRow> ProjectRows(IQueryable<Flow> flows)
        {
            return flows.Select(f => new FlowListRow
            {
                ID = f.ID,
                Name = f.Name,
                Description = f.Description,
                GameSystemName = f.GameSystem!.Name,
                StepCount = f.Steps.Count,
                BranchCount = f.Steps.Sum(s => s.Branches.Count),

                Steps = f.Steps.Select(s => new FlowListStepRow
                {
                    Kind = s.Kind,
                    HasQuestion = s.Question != null && s.Question != "",
                    OptionCount = s.Options.Count,
                    SourceStepSortOrder = s.SourceStepSortOrder,
                    Subtype = (OracleSubtype?)s.Oracle!.OracleSubtype,
                    EntryCount = s.Oracle == null ? 0 : s.Oracle.OracleEntries.Count,
                    Unbanded = s.Oracle != null && s.Oracle.OracleEntries.Any(e => e.RangeStart == null)
                })
            });
        }

        private static List<FlowListItem> Map(List<FlowListRow> rows)
        {
            return rows.Select(f => new FlowListItem
            {
                ID = f.ID,
                Name = f.Name,
                Description = f.Description,
                GameSystemName = f.GameSystemName,
                StepCount = f.StepCount,
                BranchCount = f.BranchCount,
                HasUnfinishedOracle = f.Steps.Any(s => s.Kind switch
                {
                    FlowStepKind.Roll => s.Subtype == null
                        || s.EntryCount == 0
                        || (s.Subtype.Value.Category() == OracleCategory.Dice && s.Unbanded),
                    FlowStepKind.Choice => !s.HasQuestion || s.OptionCount == 0,
                    FlowStepKind.Text or FlowStepKind.Number => !s.HasQuestion,
                    FlowStepKind.RollChosen => s.SourceStepSortOrder == null,
                    _ => false
                })
            }).ToList();
        }

        private sealed class FlowListRow
        {
            public int ID { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string GameSystemName { get; set; } = string.Empty;
            public int StepCount { get; set; }
            public int BranchCount { get; set; }
            public IEnumerable<FlowListStepRow> Steps { get; set; } = Array.Empty<FlowListStepRow>();
        }

        private sealed class FlowListStepRow
        {
            public FlowStepKind Kind { get; set; }
            public bool HasQuestion { get; set; }
            public int OptionCount { get; set; }
            public int? SourceStepSortOrder { get; set; }
            public OracleSubtype? Subtype { get; set; }
            public int EntryCount { get; set; }
            public bool Unbanded { get; set; }
        }
    }
}
