using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Everything that touches a flow's shape -- the order of steps and the options and
    /// branches hanging off them -- in one place.
    ///
    /// A flow is a flat list. A step's SortOrder is its position in the flow; every reference
    /// between steps is by database ID and survives a reorder untouched.
    /// </summary>
    public class FlowConfigurator
    {
        private readonly ApplicationDbContext _context;

        public FlowConfigurator(ApplicationDbContext context)
        {
            _context = context;
        }

        public string? AddStep(Flow flow, FlowStepKind kind, int oracleId, string? label,
            string? question, int? sourceStepId)
        {
            if (!Enum.IsDefined(kind))
            {
                return "That is not a kind of step.";
            }

            if (FlowText.LabelProblem(label, "A step label") is string labelProblem)
            {
                return labelProblem;
            }

            var step = new FlowStep
            {
                FlowID = flow.ID,
                Kind = kind,
                SortOrder = flow.Steps.Count + 1,
                Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim()
            };

            switch (kind)
            {
                case FlowStepKind.Roll:
                    var oracleError = RequireOracle(oracleId);
                    if (oracleError != null)
                    {
                        return oracleError;
                    }
                    step.OracleID = oracleId;
                    break;

                case FlowStepKind.Choice:
                case FlowStepKind.Text:
                    if (FlowText.QuestionProblem(question) is string questionProblem)
                    {
                        return questionProblem;
                    }
                    step.Question = question!.Trim();
                    break;

                case FlowStepKind.RollChosen:
                    var sourceError = ValidateSource(flow, sourceStepId);
                    if (sourceError != null)
                    {
                        return sourceError;
                    }
                    step.SourceStepID = sourceStepId;
                    break;
            }

            flow.Steps.Add(step);
            return null;
        }

        public string? EditStep(Flow flow, FlowStep step, string? label, string? question,
            int? oracleId, int? sourceStepId)
        {
            if (FlowText.LabelProblem(label, "A step label") is string labelProblem)
            {
                return labelProblem;
            }

            step.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

            switch (step.Kind)
            {
                case FlowStepKind.Roll:
                    var oracleError = RequireOracle(oracleId);
                    if (oracleError != null)
                    {
                        return oracleError;
                    }
                    if (step.OracleID != oracleId && step.Branches.Count > 0)
                    {
                        _context.FlowBranches.RemoveRange(step.Branches.ToList());
                        step.Branches.Clear();
                    }
                    step.OracleID = oracleId;
                    break;

                case FlowStepKind.Choice:
                case FlowStepKind.Text:
                    if (FlowText.QuestionProblem(question) is string questionProblem)
                    {
                        return questionProblem;
                    }
                    step.Question = question!.Trim();
                    break;

                case FlowStepKind.RollChosen:
                    var sourceError = ValidateSource(flow, sourceStepId, step);
                    if (sourceError != null)
                    {
                        return sourceError;
                    }
                    step.SourceStepID = sourceStepId;
                    break;
            }

            return null;
        }

        public string? RemoveStep(Flow flow, int stepId, out int orphaned)
        {
            orphaned = 0;

            var step = flow.Steps.FirstOrDefault(s => s.ID == stepId);
            if (step == null)
            {
                return "That step is no longer part of this flow.";
            }

            // References are by ID, so they only break when the step they name is actually
            // gone. Clear those and count them for the notice the caller shows, before the
            // step is removed: removing it has EF Core clear them without counting.
            foreach (var survivor in flow.Steps.Where(s => s != step))
            {
                if (survivor.SourceStepID == stepId)
                {
                    survivor.SourceStepID = null;
                    orphaned++;
                }

                foreach (var branch in survivor.Branches.Where(b => b.TargetStepID == stepId))
                {
                    branch.TargetStepID = null;
                    orphaned++;
                }

                foreach (var option in survivor.Options.Where(o => o.TargetStepID == stepId))
                {
                    option.TargetStepID = null;
                    orphaned++;
                }
            }

            _context.FlowSteps.Remove(step);
            flow.Steps.Remove(step);

            flow.Steps.OrderBy(s => s.SortOrder).ToList().Renumber();
            return null;
        }

        public string? MoveStep(Flow flow, int stepId, int delta)
        {
            if (delta != -1 && delta != 1)
            {
                return "A step can only move one place at a time.";
            }

            var ordered = flow.Steps.OrderBy(s => s.SortOrder).ToList();
            var index = ordered.FindIndex(s => s.ID == stepId);
            if (index < 0)
            {
                return "That step is no longer part of this flow.";
            }

            var target = index + delta;
            if (target < 0 || target >= ordered.Count)
            {
                return null;
            }

            // Steps only swap with a neighbour, so the order breaks only when the two are a step
            // and the source it rolls from.
            if (ordered[index].SourceStepID == ordered[target].ID || ordered[target].SourceStepID == ordered[index].ID)
            {
                return SourceComesFirst;
            }

            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
            ordered.Renumber();

            return null;
        }

        public string? SetBranches(Flow flow, FlowStep step, List<BranchRow> rows)
        {
            if (!step.AllowsBranches())
            {
                return "This kind of step has no fixed outcomes to branch on.";
            }

            var entriesById = (step.Oracle?.OracleEntries ?? new List<OracleEntry>())
                .ToDictionary(e => e.ID);
            var validSteps = flow.Steps.Select(s => s.ID).ToHashSet();
            var existing = step.Branches.ToDictionary(b => b.OracleEntryID);

            foreach (var row in rows)
            {
                if (!entriesById.ContainsKey(row.EntryID))
                {
                    return "One of these outcomes no longer exists. Reload the page and try again.";
                }

                var oracleError = ResolveOracle(row.OracleID, out var oracleId);
                if (oracleError != null)
                {
                    return oracleError;
                }

                var targetError = ParseTarget(row.Target, validSteps, out bool routes,
                    out int? targetStepId, out bool endsFlow);
                if (targetError != null)
                {
                    return targetError;
                }

                var wanted = oracleId != null || routes;
                existing.TryGetValue(row.EntryID, out var branch);

                if (!wanted)
                {
                    if (branch != null)
                    {
                        _context.FlowBranches.Remove(branch);
                        step.Branches.Remove(branch);
                    }
                    continue;
                }

                if (branch == null)
                {
                    branch = new FlowBranch { FlowStepID = step.ID, OracleEntryID = row.EntryID };
                    _context.FlowBranches.Add(branch);
                    step.Branches.Add(branch);
                }

                branch.OracleID = oracleId;
                branch.TargetStepID = targetStepId;
                branch.EndsFlow = endsFlow;
            }

            return null;
        }

        public string? SetOptions(Flow flow, FlowStep step, List<OptionRow> rows)
        {
            if (step.Kind != FlowStepKind.Choice)
            {
                return "Only a dropdown question has options.";
            }

            var existing = step.Options.ToDictionary(o => o.ID);
            var validSteps = flow.Steps.Select(s => s.ID).ToHashSet();

            foreach (var row in rows)
            {
                if (!existing.TryGetValue(row.OptionID, out var option))
                {
                    return "One of these options no longer exists. Reload the page and try again.";
                }

                if (string.IsNullOrWhiteSpace(row.Label))
                {
                    return "Every option needs something to call it.";
                }
                if (FlowText.LabelProblem(row.Label, "An option label") is string rowProblem)
                {
                    return rowProblem;
                }

                var oracleError = ResolveOracle(row.OracleID, out var oracleId);
                if (oracleError != null)
                {
                    return oracleError;
                }

                var targetError = ParseTarget(row.Target, validSteps, out bool routes,
                    out int? targetStepId, out bool endsFlow);
                if (targetError != null)
                {
                    return targetError;
                }

                option.Label = row.Label.Trim();
                option.OracleID = oracleId;
                option.TargetStepID = routes && !endsFlow ? targetStepId : null;
                option.EndsFlow = routes && endsFlow;
            }

            return null;
        }

        public string? AddOption(FlowStep step, string? label, int? oracleId)
        {
            if (step.Kind != FlowStepKind.Choice)
            {
                return "Only a dropdown question has options.";
            }
            if (string.IsNullOrWhiteSpace(label))
            {
                return "Give the option something to call it.";
            }
            if (FlowText.LabelProblem(label, "An option label") is string labelProblem)
            {
                return labelProblem;
            }

            var error = ResolveOracle(oracleId, out var resolved);
            if (error != null)
            {
                return error;
            }

            Append(step, label.Trim(), resolved);
            return null;
        }

        public string? AddOptionsFromOracles(FlowStep step, List<int> oracleIds, out int added)
        {
            added = 0;

            if (step.Kind != FlowStepKind.Choice)
            {
                return "Only a dropdown question has options.";
            }
            if (oracleIds == null || oracleIds.Count == 0)
            {
                return "Tick at least one oracle to add.";
            }

            var oracles = _context.Oracles
                .Where(o => oracleIds.Contains(o.ID))
                .OrderBy(o => o.Name)
                .ToList();

            if (oracles.Count == 0)
            {
                return "None of those oracles still exist.";
            }

            var alreadyThere = step.Options
                .Where(o => o.OracleID != null)
                .Select(o => o.OracleID!.Value)
                .ToHashSet();

            foreach (var oracle in oracles)
            {
                if (alreadyThere.Contains(oracle.ID))
                {
                    continue;
                }

                var label = oracle.Name.Length > 100 ? oracle.Name.Substring(0, 100) : oracle.Name;
                Append(step, label, oracle.ID);
                added++;
            }

            if (added == 0)
            {
                return "Every oracle you ticked is already an option on this question.";
            }

            return null;
        }

        public string? RemoveOption(FlowStep step, int optionId)
        {
            var option = step.Options.FirstOrDefault(o => o.ID == optionId);
            if (option == null)
            {
                return "That option is no longer part of this question.";
            }

            _context.FlowChoiceOptions.Remove(option);
            step.Options.Remove(option);

            step.Options.OrderBy(o => o.SortOrder).ToList().Renumber();
            return null;
        }

        public string? MoveOption(FlowStep step, int optionId, int delta)
        {
            if (delta != -1 && delta != 1)
            {
                return "An option can only move one place at a time.";
            }

            var ordered = step.Options.OrderBy(o => o.SortOrder).ToList();
            var index = ordered.FindIndex(o => o.ID == optionId);

            if (index < 0)
            {
                return "That option is no longer part of this question.";
            }

            var target = index + delta;
            if (target < 0 || target >= ordered.Count)
            {
                return null;
            }

            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
            ordered.Renumber();
            return null;
        }

        private static void Append(FlowStep step, string label, int? oracleId)
        {
            var option = new FlowChoiceOption
            {
                FlowStepID = step.ID,
                SortOrder = step.Options.Count == 0 ? 1 : step.Options.Max(o => o.SortOrder) + 1,
                Label = label,
                OracleID = oracleId
            };

            step.Options.Add(option);
        }

        private string? RequireOracle(int? oracleId) =>
            oracleId is null or <= 0 ? "Pick an oracle to roll." : ResolveOracle(oracleId, out _);

        private string? ResolveOracle(int? oracleId, out int? resolved)
        {
            resolved = null;

            if (oracleId == null || oracleId <= 0)
            {
                return null;
            }

            if (!_context.Oracles.Any(o => o.ID == oracleId))
            {
                return "That oracle no longer exists.";
            }

            resolved = oracleId;
            return null;
        }

        private const string SourceComesFirst = "A step can only take its oracle from a step that comes before it.";

        /// <summary>Checks the source of a step that rolls what another picks; a null step is a new last step.</summary>
        private static string? ValidateSource(Flow flow, int? sourceStepId, FlowStep? step = null)
        {
            if (sourceStepId == null)
            {
                return "Say which step this one takes its oracle from.";
            }

            var source = flow.Steps.FirstOrDefault(s => s.ID == sourceStepId.Value);
            if (source == null)
            {
                return "That step is not in this flow.";
            }
            if (source.Kind != FlowStepKind.Choice && source.Kind != FlowStepKind.Roll)
            {
                return "This step takes its oracle from a dropdown question or a roll.";
            }
            if (step != null && source.SortOrder > step.SortOrder)
            {
                return SourceComesFirst;
            }

            return null;
        }

        /// <summary>
        /// Reads a routing picker: blank means no route, "end" ends the run, a number is the
        /// target step's ID.
        /// </summary>
        private static string? ParseTarget(string? target, HashSet<int> validSteps, out bool routes,
            out int? stepId, out bool endsFlow)
        {
            routes = false;
            stepId = null;
            endsFlow = false;

            if (string.IsNullOrWhiteSpace(target))
            {
                return null;
            }

            routes = true;

            if (target == BranchRow.EndTarget)
            {
                endsFlow = true;
                return null;
            }

            if (!int.TryParse(target, out var parsed) || !validSteps.Contains(parsed))
            {
                return "One of these routes points at a step that is not in this flow.";
            }

            stepId = parsed;
            return null;
        }
    }
}
