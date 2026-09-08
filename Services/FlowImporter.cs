using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Validates the flows in a parsed export file and turns them into Flow entities ready to add.
    ///
    /// Pure -- no DbContext, like OracleImporter, and all-or-nothing for the same reason: one
    /// bad flow means nothing at all is imported, which is what keeps export, Clear All,
    /// re-import a safe round trip.
    /// </summary>
    public static class FlowImporter
    {
        public static string? TryBuild(
            List<FlowDto>? dtos,
            Dictionary<string, GameSystem> systemsByName,
            List<Oracle> importedOracles,
            IEnumerable<Oracle> existingOracles,
            out List<Flow> flows)
        {
            flows = new List<Flow>();
            var built = new List<Flow>();

            if (dtos == null || dtos.Count == 0)
            {
                return null;
            }

            var lookup = new OracleResolver(importedOracles, existingOracles);

            for (int i = 0; i < dtos.Count; i++)
            {
                var dto = dtos[i];
                var label = $"Flow {i + 1}";

                if (string.IsNullOrWhiteSpace(dto.Name))
                    return $"{label}: name is missing.";

                if (dto.Name.Length > 100)
                    return $"{label} (\"{dto.Name}\"): name is longer than 100 characters.";

                if (dto.Description != null && dto.Description.Length > 500)
                    return $"{label} (\"{dto.Name}\"): description is longer than 500 characters.";

                if (string.IsNullOrWhiteSpace(dto.GameSystem))
                    return $"{label} (\"{dto.Name}\"): game system is missing.";

                var system = OracleImporter.EnsureSystem(systemsByName, dto.GameSystem);

                var steps = dto.Steps ?? new List<FlowStepDto>();

                var flow = new Flow
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    GameSystem = system
                };

                var error = BuildSteps(label, dto.Name, steps, lookup, flow);
                if (error != null)
                    return error;

                built.Add(flow);
            }

            flows = built;
            return null;
        }

        private static string? BuildSteps(string label, string name, List<FlowStepDto> steps,
            OracleResolver lookup, Flow flow)
        {
            for (int n = 0; n < steps.Count; n++)
            {
                var stepDto = steps[n];
                var where = $"{label} (\"{name}\"): step {n + 1}";

                if (stepDto.SortOrder != n + 1)
                    return $"{label} (\"{name}\"): steps must be numbered 1 to {steps.Count} in order; step {n + 1} says {stepDto.SortOrder}.";

                if (stepDto.Label != null && stepDto.Label.Length > 100)
                    return $"{where} has a label longer than 100 characters.";

                // Absent in a version 3 file, where every step rolled a fixed oracle.
                var kindName = string.IsNullOrWhiteSpace(stepDto.Kind)
                    ? nameof(FlowStepKind.Roll)
                    : stepDto.Kind;

                if (!Enum.TryParse<FlowStepKind>(kindName, out var kind) || !Enum.IsDefined(kind))
                    return $"{where}: \"{stepDto.Kind}\" is not a known kind of step.";

                var step = new FlowStep
                {
                    SortOrder = stepDto.SortOrder,
                    Kind = kind,
                    Label = string.IsNullOrWhiteSpace(stepDto.Label) ? null : stepDto.Label
                };

                var error = FillStep(where, kind, stepDto, steps.Count, lookup, step);
                if (error != null)
                    return error;

                flow.Steps.Add(step);
            }

            foreach (var step in flow.Steps.Where(s => s.Kind == FlowStepKind.RollChosen))
            {
                var source = flow.Steps.FirstOrDefault(s => s.SortOrder == step.SourceStepSortOrder);

                if (source == null)
                    return $"{label} (\"{name}\"): step {step.SortOrder} is picked at step {step.SourceStepSortOrder}, which is not in this flow.";

                if (source.Kind != FlowStepKind.Choice)
                    return $"{label} (\"{name}\"): step {step.SortOrder} is picked at step {source.SortOrder}, which is not a dropdown question.";
            }

            return null;
        }

        private static string? FillStep(string where, FlowStepKind kind, FlowStepDto dto,
            int stepCount, OracleResolver lookup, FlowStep step)
        {
            switch (kind)
            {
                case FlowStepKind.Roll:
                    var oracleError = lookup.Resolve(where, dto.Oracle, required: true, out var oracle);
                    if (oracleError != null)
                        return oracleError;

                    step.Oracle = oracle;
                    return BuildBranches(where, dto.Branches, oracle!, stepCount, step);

                case FlowStepKind.Choice:
                    var questionError = Question(where, dto.Question, step);
                    if (questionError != null)
                        return questionError;

                    return BuildOptions(where, dto.Options, lookup, step);

                case FlowStepKind.Text:
                case FlowStepKind.Number:
                    return Question(where, dto.Question, step);

                case FlowStepKind.RollChosen:
                    if (dto.PickedAtStep == null)
                        return $"{where} rolls what a question picks, but does not say which one.";

                    if (dto.PickedAtStep < 1 || dto.PickedAtStep > stepCount)
                        return $"{where} is picked at step {dto.PickedAtStep}, which is not in this flow.";

                    step.SourceStepSortOrder = dto.PickedAtStep;
                    return null;
            }

            return null;
        }

        private static string? Question(string where, string? question, FlowStep step)
        {
            if (string.IsNullOrWhiteSpace(question))
                return $"{where} is a question step but has no question.";

            if (question.Length > 200)
                return $"{where} has a question longer than 200 characters.";

            step.Question = question;
            return null;
        }

        private static string? BuildOptions(string where, List<FlowChoiceOptionDto>? options,
            OracleResolver lookup, FlowStep step)
        {
            if (options == null || options.Count == 0)
                return $"{where} is a dropdown question with no options.";

            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];

                if (string.IsNullOrWhiteSpace(option.Label))
                    return $"{where} has an option with no label.";

                if (option.Label.Length > 100)
                    return $"{where} has an option label longer than 100 characters.";

                // An option may name no oracle at all -- it can exist only to be recorded.
                var error = lookup.Resolve($"{where}, option \"{option.Label}\",",
                    option.Oracle, required: false, out var oracle);
                if (error != null)
                    return error;

                step.Options.Add(new FlowChoiceOption
                {
                    SortOrder = i + 1,
                    Label = option.Label,
                    Oracle = oracle
                });
            }

            return null;
        }

        private static string? BuildBranches(string where, List<FlowBranchDto>? branches,
            Oracle oracle, int stepCount, FlowStep step)
        {
            if (branches == null)
            {
                return null;
            }

            var entriesBySortOrder = oracle.OracleEntries.ToDictionary(e => e.SortOrder);
            var seen = new HashSet<int>();

            foreach (var branchDto in branches)
            {
                if (!entriesBySortOrder.TryGetValue(branchDto.WhenOutcome, out var entry))
                    return $"{where} branches on outcome {branchDto.WhenOutcome}, which \"{oracle.Name}\" does not have.";

                if (!seen.Add(branchDto.WhenOutcome))
                    return $"{where} has more than one branch on outcome {branchDto.WhenOutcome}.";

                if (branchDto.GoToStep != null
                    && (branchDto.GoToStep < 1 || branchDto.GoToStep > stepCount))
                {
                    return $"{where} branches to step {branchDto.GoToStep}, which is not in this flow.";
                }

                step.Branches.Add(new FlowBranch
                {
                    OracleEntry = entry,
                    TargetStepSortOrder = branchDto.GoToStep
                });
            }

            return null;
        }

        private sealed class OracleResolver
        {
            private readonly Dictionary<string, Oracle> _fromFile;
            private readonly HashSet<string> _ambiguousInFile;
            private readonly Dictionary<string, Oracle> _fromDatabase;
            private readonly HashSet<string> _ambiguousInDatabase;

            public OracleResolver(IEnumerable<Oracle> fromFile, IEnumerable<Oracle> fromDatabase)
            {
                _fromFile = Index(fromFile, out _ambiguousInFile);
                _fromDatabase = Index(fromDatabase, out _ambiguousInDatabase);
            }

            public string? Resolve(string where, OracleRefDto? reference, bool required, out Oracle? oracle)
            {
                oracle = null;

                if (reference == null
                    || string.IsNullOrWhiteSpace(reference.Name)
                    || string.IsNullOrWhiteSpace(reference.GameSystem))
                {
                    return required
                        ? $"{where} does not name an oracle and its game system."
                        : null;
                }

                var key = Key(reference.Name, reference.GameSystem);
                var described = $"\"{reference.Name}\" on \"{reference.GameSystem}\"";

                if (_ambiguousInFile.Contains(key))
                    return $"{where} names {described}, and this file carries more than one oracle by that name.";

                if (_fromFile.TryGetValue(key, out oracle))
                    return null;

                if (_ambiguousInDatabase.Contains(key))
                    return $"{where} names {described}, and you already have more than one oracle by that name. Rename one, or import a file that carries its own copy.";

                if (_fromDatabase.TryGetValue(key, out oracle))
                    return null;

                return $"{where} names {described}, which is neither in this file nor in your toolbox.";
            }

            private static Dictionary<string, Oracle> Index(IEnumerable<Oracle> oracles, out HashSet<string> ambiguous)
            {
                var lookup = new Dictionary<string, Oracle>();
                ambiguous = new HashSet<string>();

                foreach (var oracle in oracles)
                {
                    var systemName = oracle.GameSystem?.Name;
                    if (systemName == null)
                    {
                        continue;
                    }

                    var key = Key(oracle.Name, systemName);
                    if (!lookup.TryAdd(key, oracle))
                    {
                        ambiguous.Add(key);
                    }
                }

                return lookup;
            }

            private static string Key(string name, string gameSystem)
            {
                return $"{name.Trim().ToLowerInvariant()}\u001f{gameSystem.Trim().ToLowerInvariant()}";
            }
        }
    }
}
