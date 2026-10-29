using GM_Toolbox.Models;
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
            flows = flows.Containing(f => f.Name, name);

            if (gameSystemId is > 0)
            {
                flows = flows.Where(f => f.GameSystemID == gameSystemId.Value);
            }

            return flows;
        }

        /// <summary>
        /// A flow with every step, option and branch, and the oracles they roll with their entries and game
        /// systems: enough to run, edit or export it. Split, because the collections down each path would
        /// otherwise multiply into one very wide result set.
        /// </summary>
        public static IQueryable<Flow> WithStepsAndOracles(this IQueryable<Flow> flows)
        {
            return flows
                .Include(f => f.GameSystem)
                .Include(f => f.Steps).ThenInclude(s => s.Oracle).ThenInclude(o => o!.OracleEntries)
                .Include(f => f.Steps).ThenInclude(s => s.Oracle).ThenInclude(o => o!.GameSystem)
                .Include(f => f.Steps).ThenInclude(s => s.Branches).ThenInclude(b => b.Oracle).ThenInclude(o => o!.OracleEntries)
                .Include(f => f.Steps).ThenInclude(s => s.Branches).ThenInclude(b => b.Oracle).ThenInclude(o => o!.GameSystem)
                .Include(f => f.Steps).ThenInclude(s => s.Options).ThenInclude(o => o.Oracle).ThenInclude(o => o!.OracleEntries)
                .Include(f => f.Steps).ThenInclude(s => s.Options).ThenInclude(o => o.Oracle).ThenInclude(o => o!.GameSystem)
                .AsSplitQuery();
        }

        /// <summary>One page of flows, each loaded whole so its "needs setup" badge uses the same rules as a run.</summary>
        public static async Task<PagedList<FlowListItem>> ToPagedListAsync(
            IQueryable<Flow> flows,
            int page)
        {
            var loaded = await PagedList<Flow>.CreateAsync(flows.OrderBy(f => f.Name), WithStepsAndOracles, page);
            return loaded.WithItems(loaded.Items.Select(f => new FlowListItem
            {
                ID = f.ID,
                Name = f.Name,
                Description = f.Description,
                GameSystemName = f.GameSystem?.Name ?? string.Empty,
                StepCount = f.Steps.Count,
                BranchCount = f.Steps.Sum(s => s.Branches.Count),
                NeedsSetup = f.RunBlockedReason() != null
            }).ToList());
        }

        public static Task<List<int>> MatchingIdsAsync(IQueryable<Flow> flows)
        {
            return flows.OrderBy(f => f.Name).Select(f => f.ID).ToListAsync();
        }
    }
}
