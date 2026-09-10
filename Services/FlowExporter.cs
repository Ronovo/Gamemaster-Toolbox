using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Turns loaded Flow entities into the portable file shape (format version 10).
    ///
    /// Pure -- no DbContext and no database access, like OracleExporter. The caller loads the
    /// flows with their steps, branches, options and each referenced oracle with its entries,
    /// which the branches are keyed on.
    ///
    /// A flow is a flat list of steps, each with a per-file <c>id</c> (its position) that
    /// <c>pickedAtStep</c> and <c>goToStep</c> point at.
    /// </summary>
    public class FlowExporter
    {
        public static List<FlowDto> Build(List<Flow> flows)
        {
            var built = new List<FlowDto>();

            foreach (var flow in flows)
            {
                var dto = new FlowDto
                {
                    Name = flow.Name,
                    GameSystem = flow.GameSystem!.Name,
                    Description = flow.Description
                };

                var steps = flow.InRunOrder().ToList();
                var ids = new Dictionary<int, int>();
                for (int i = 0; i < steps.Count; i++)
                {
                    ids[steps[i].ID] = i + 1;
                }

                foreach (var step in steps)
                {
                    dto.Steps.Add(BuildStep(step, ids));
                }

                built.Add(dto);
            }

            return built;
        }

        private static FlowStepDto BuildStep(FlowStep step, Dictionary<int, int> ids)
        {
            var dto = new FlowStepDto
            {
                Id = ids[step.ID],
                SortOrder = step.SortOrder,
                Kind = step.Kind.ToString(),
                Label = step.Label
            };

            switch (step.Kind)
            {
                case FlowStepKind.Roll:
                    dto.Oracle = Reference(step.Oracle);
                    dto.Branches = BuildBranches(step, ids);
                    break;

                case FlowStepKind.Choice:
                    dto.Question = step.Question;
                    dto.Options = step.Options
                        .OrderBy(o => o.SortOrder)
                        .Select(o => new FlowChoiceOptionDto
                        {
                            SortOrder = o.SortOrder,
                            Label = o.Label,
                            Oracle = Reference(o.Oracle),
                            GoToStep = o.TargetStepID != null && ids.TryGetValue(o.TargetStepID.Value, out var t)
                                ? t : null,
                            EndsFlow = o.EndsFlow ? true : null
                        })
                        .ToList();
                    break;

                case FlowStepKind.Text:
                    dto.Question = step.Question;
                    break;

                case FlowStepKind.RollChosen:
                    dto.PickedAtStep = step.SourceStepID != null && ids.TryGetValue(step.SourceStepID.Value, out var s)
                        ? s : null;
                    break;
            }

            return dto;
        }

        private static List<FlowBranchDto> BuildBranches(FlowStep step, Dictionary<int, int> ids)
        {
            var entriesById = (step.Oracle?.OracleEntries ?? new List<OracleEntry>())
                .ToDictionary(e => e.ID);

            var built = new List<FlowBranchDto>();

            foreach (var branch in step.Branches)
            {
                if (!entriesById.TryGetValue(branch.OracleEntryID, out var entry))
                {
                    continue;
                }

                built.Add(new FlowBranchDto
                {
                    WhenOutcome = entry.SortOrder,
                    PicksOracle = Reference(branch.Oracle),
                    GoToStep = branch.TargetStepID != null && ids.TryGetValue(branch.TargetStepID.Value, out var t)
                        ? t : null,
                    EndsFlow = branch.EndsFlow ? true : null
                });
            }

            return built.OrderBy(b => b.WhenOutcome).ToList();
        }

        private static OracleRefDto? Reference(Oracle? oracle)
        {
            return oracle == null
                ? null
                : new OracleRefDto { Name = oracle.Name, GameSystem = oracle.GameSystem!.Name };
        }

        public static List<Oracle> ReferencedOracles(List<Flow> flows)
        {
            var steps = flows.SelectMany(f => f.Steps).ToList();

            return steps.Select(s => s.Oracle)
                .Concat(steps.SelectMany(s => s.Options).Select(o => o.Oracle))
                .Concat(steps.SelectMany(s => s.Branches).Select(b => b.Oracle))
                .Where(o => o != null)
                .Select(o => o!)
                .DistinctBy(o => o.ID)
                .ToList();
        }
    }
}
