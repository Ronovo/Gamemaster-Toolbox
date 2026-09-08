using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Turns loaded Flow entities into the portable file shape.
    ///
    /// Pure -- no DbContext and no database access, like OracleExporter. The caller loads the
    /// flows with their steps, branches, options and each referenced oracle (with its entries,
    /// which the branches are keyed on) and hands them over.
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

                foreach (var step in flow.Steps.OrderBy(s => s.SortOrder))
                {
                    dto.Steps.Add(BuildStep(step));
                }

                built.Add(dto);
            }

            return built;
        }

        private static FlowStepDto BuildStep(FlowStep step)
        {
            var dto = new FlowStepDto
            {
                SortOrder = step.SortOrder,
                Kind = step.Kind.ToString(),
                Label = step.Label
            };

            switch (step.Kind)
            {
                case FlowStepKind.Roll:
                    dto.Oracle = Reference(step.Oracle);
                    dto.Branches = BuildBranches(step);
                    break;

                case FlowStepKind.Choice:
                    dto.Question = step.Question;
                    dto.Options = step.Options
                        .OrderBy(o => o.SortOrder)
                        .Select(o => new FlowChoiceOptionDto
                        {
                            SortOrder = o.SortOrder,
                            Label = o.Label,
                            Oracle = Reference(o.Oracle)
                        })
                        .ToList();
                    break;

                case FlowStepKind.Text:
                case FlowStepKind.Number:
                    dto.Question = step.Question;
                    break;

                case FlowStepKind.RollChosen:
                    dto.PickedAtStep = step.SourceStepSortOrder;
                    break;
            }

            return dto;
        }

        private static List<FlowBranchDto> BuildBranches(FlowStep step)
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
                    GoToStep = branch.TargetStepSortOrder
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
            var fromSteps = flows.SelectMany(f => f.Steps).Select(s => s.Oracle);
            var fromOptions = flows.SelectMany(f => f.Steps).SelectMany(s => s.Options).Select(o => o.Oracle);

            return fromSteps
                .Concat(fromOptions)
                .Where(o => o != null)
                .Select(o => o!)
                .DistinctBy(o => o.ID)
                .ToList();
        }
    }
}
