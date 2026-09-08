using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Everything that touches a flow's step order, in one place.
    /// </summary>
    public class FlowConfigurator
    {
        private readonly ApplicationDbContext _context;

        public FlowConfigurator(ApplicationDbContext context)
        {
            _context = context;
        }

        public string? AddStep(Flow flow, FlowStepKind kind, int oracleId, string? label,
            string? question, int? sourceStepSortOrder)
        {
            if (!Enum.IsDefined(kind))
            {
                return "That is not a kind of step.";
            }

            if (label != null && label.Length > 100)
            {
                return "A step label cannot be longer than 100 characters.";
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
                    if (oracleId <= 0)
                    {
                        return "Pick an oracle to roll.";
                    }

                    if (!_context.Oracles.Any(o => o.ID == oracleId))
                    {
                        return "That oracle no longer exists.";
                    }

                    step.OracleID = oracleId;
                    break;

                case FlowStepKind.Choice:
                case FlowStepKind.Text:
                case FlowStepKind.Number:
                    if (string.IsNullOrWhiteSpace(question))
                    {
                        return "Type the question this step asks.";
                    }

                    if (question.Length > 200)
                    {
                        return "A question cannot be longer than 200 characters.";
                    }

                    step.Question = question.Trim();
                    break;

                case FlowStepKind.RollChosen:
                    var error = ValidateSource(flow, sourceStepSortOrder);
                    if (error != null)
                    {
                        return error;
                    }

                    step.SourceStepSortOrder = sourceStepSortOrder;
                    break;
            }

            flow.Steps.Add(step);
            return null;
        }

        public string? EditStep(Flow flow, FlowStep step, string? label, string? question,
            int? oracleId, int? sourceStepSortOrder)
        {
            if (label != null && label.Length > 100)
            {
                return "A step label cannot be longer than 100 characters.";
            }

            step.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

            switch (step.Kind)
            {
                case FlowStepKind.Roll:
                    if (oracleId == null || oracleId <= 0)
                    {
                        return "Pick an oracle to roll.";
                    }

                    if (!_context.Oracles.Any(o => o.ID == oracleId))
                    {
                        return "That oracle no longer exists.";
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
                case FlowStepKind.Number:
                    if (string.IsNullOrWhiteSpace(question))
                    {
                        return "Type the question this step asks.";
                    }

                    if (question.Length > 200)
                    {
                        return "A question cannot be longer than 200 characters.";
                    }

                    step.Question = question.Trim();
                    break;

                case FlowStepKind.RollChosen:
                    var error = ValidateSource(flow, sourceStepSortOrder);
                    if (error != null)
                    {
                        return error;
                    }

                    step.SourceStepSortOrder = sourceStepSortOrder;
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

            var survivors = flow.Steps
                .Where(s => s.ID != stepId)
                .OrderBy(s => s.SortOrder)
                .ToList();

            _context.FlowSteps.Remove(step);
            flow.Steps.Remove(step);

            Renumber(survivors, out orphaned);
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
                // Already at the end it was asked to move towards. Not an error worth a message.
                return null;
            }

            (ordered[index], ordered[target]) = (ordered[target], ordered[index]);

            Renumber(ordered, out _);
            return null;
        }

        public string? SetBranches(Flow flow, FlowStep step, List<BranchRow> rows)
        {
            if (!step.Kind.CanBranch())
            {
                return "This kind of step has no fixed outcomes to branch on.";
            }

            var entriesById = (step.Oracle?.OracleEntries ?? new List<OracleEntry>())
                .ToDictionary(e => e.ID);
            var validTargets = flow.Steps.Select(s => s.SortOrder).ToHashSet();
            var existing = step.Branches.ToDictionary(b => b.OracleEntryID);

            foreach (var row in rows)
            {
                if (!entriesById.ContainsKey(row.EntryID))
                {
                    return "One of these outcomes no longer exists. Reload the page and try again.";
                }

                var error = ParseTarget(row.Target, validTargets, out bool hasBranch, out int? sortOrder);
                if (error != null)
                {
                    return error;
                }

                existing.TryGetValue(row.EntryID, out var branch);

                if (!hasBranch)
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

                branch.TargetStepSortOrder = sortOrder;
            }

            return null;
        }

        public string? SetOptions(FlowStep step, List<OptionRow> rows)
        {
            if (step.Kind != FlowStepKind.Choice)
            {
                return "Only a dropdown question has options.";
            }

            var existing = step.Options.ToDictionary(o => o.ID);

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

                if (row.Label.Length > 100)
                {
                    return "An option label cannot be longer than 100 characters.";
                }

                var error = ResolveOracle(row.OracleID, out var oracleId);
                if (error != null)
                {
                    return error;
                }

                option.Label = row.Label.Trim();
                option.OracleID = oracleId;
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

            if (label.Length > 100)
            {
                return "An option label cannot be longer than 100 characters.";
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

            RenumberOptions(step.Options.OrderBy(o => o.SortOrder).ToList());
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

            RenumberOptions(ordered);
            return null;
        }

        // ------------------------------------------------------------------ helpers

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

        private static void RenumberOptions(List<FlowChoiceOption> ordered)
        {
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].SortOrder = i + 1;
            }
        }

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

        private static string? ValidateSource(Flow flow, int? sourceStepSortOrder)
        {
            if (sourceStepSortOrder == null)
            {
                return "Say which dropdown question this step takes its oracle from.";
            }

            var source = flow.Steps.FirstOrDefault(s => s.SortOrder == sourceStepSortOrder.Value);

            if (source == null)
            {
                return "That step is not in this flow.";
            }

            if (source.Kind != FlowStepKind.Choice)
            {
                return "Only a dropdown question picks an oracle, so only one of those can be the source.";
            }

            return null;
        }

        private static string? ParseTarget(string? target, HashSet<int> validTargets,
            out bool hasBranch, out int? sortOrder)
        {
            hasBranch = false;
            sortOrder = null;

            if (string.IsNullOrWhiteSpace(target))
            {
                return null;
            }

            hasBranch = true;

            if (target == BranchRow.EndTarget)
            {
                return null;
            }

            if (!int.TryParse(target, out var parsed) || !validTargets.Contains(parsed))
            {
                return "One of these branches points at a step that is not in this flow.";
            }

            sortOrder = parsed;
            return null;
        }

        private static void Renumber(List<FlowStep> ordered, out int orphaned)
        {
            orphaned = 0;

            var map = new Dictionary<int, int>();
            for (int i = 0; i < ordered.Count; i++)
            {
                map[ordered[i].SortOrder] = i + 1;
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].SortOrder = i + 1;
            }

            foreach (var branch in ordered.SelectMany(s => s.Branches))
            {
                if (branch.TargetStepSortOrder == null)
                {
                    continue;
                }

                if (map.TryGetValue(branch.TargetStepSortOrder.Value, out var moved))
                {
                    branch.TargetStepSortOrder = moved;
                }
                else
                {
                    branch.TargetStepSortOrder = null;
                    orphaned++;
                }
            }

            foreach (var step in ordered)
            {
                if (step.SourceStepSortOrder == null)
                {
                    continue;
                }

                if (map.TryGetValue(step.SourceStepSortOrder.Value, out var moved))
                {
                    step.SourceStepSortOrder = moved;
                }
                else
                {
                    step.SourceStepSortOrder = null;
                    orphaned++;
                }
            }
        }
    }
}
