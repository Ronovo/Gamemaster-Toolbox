using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Services
{
    public static class FlowListQuery
    {
        public static async Task<List<FlowListItem>> ToListItemsAsync(IQueryable<Flow> flows)
        {
            var rows = await flows
                .OrderBy(f => f.Name)
                .Select(f => new
                {
                    f.ID,
                    f.Name,
                    f.Description,
                    GameSystemName = f.GameSystem!.Name,
                    StepCount = f.Steps.Count,
                    BranchCount = f.Steps.Sum(s => s.Branches.Count),

                    // Everything the finished-or-not verdict needs, as flat as SQL can give it.
                    Steps = f.Steps.Select(s => new
                    {
                        s.Kind,
                        HasQuestion = s.Question != null && s.Question != "",
                        OptionCount = s.Options.Count,
                        s.SourceStepSortOrder,
                        // Nullable, and every read below is guarded: only a Roll step has an
                        // oracle at all, and a non-nullable enum cannot come back from the left
                        // join a question step produces.
                        Subtype = (OracleSubtype?)s.Oracle!.OracleSubtype,
                        EntryCount = s.Oracle == null ? 0 : s.Oracle.OracleEntries.Count,
                        Unbanded = s.Oracle != null && s.Oracle.OracleEntries.Any(e => e.RangeStart == null)
                    })
                })
                .ToListAsync();

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
    }
}
