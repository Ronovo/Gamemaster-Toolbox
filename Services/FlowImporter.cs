using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Validates the flows in a parsed export file and turns them into Flow entities ready to add.
    ///
    /// Pure -- no DbContext, like OracleImporter, and all-or-nothing: one bad flow means nothing
    /// at all is imported.
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

                if (dto == null)
                    return $"{label}: is empty.";
                if (string.IsNullOrWhiteSpace(dto.Name))
                    return $"{label}: name is missing.";
                if (dto.Name.Length > 100)
                    return $"{label} (\"{dto.Name}\"): name is longer than 100 characters.";
                if (dto.Description != null && dto.Description.Length > 500)
                    return $"{label} (\"{dto.Name}\"): description is longer than 500 characters.";
                if (string.IsNullOrWhiteSpace(dto.GameSystem))
                    return $"{label} (\"{dto.Name}\"): game system is missing.";

                var systemError = OracleImporter.EnsureSystem(systemsByName, dto.GameSystem, $"{label} (\"{dto.Name}\")", out var system);
                if (systemError != null)
                    return systemError;

                var flow = new Flow
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    GameSystem = system
                };

                var builder = new FlowBuilder($"{label} (\"{dto.Name}\")", lookup, flow);
                var error = builder.Build(dto.Steps ?? new List<FlowStepDto>());
                if (error != null)
                    return error;

                error = builder.ResolveReferences();
                if (error != null)
                    return error;

                built.Add(flow);
            }

            flows = built;
            return null;
        }

        /// <summary>Turns one flow's step DTOs into a wired-up list of FlowStep entities.</summary>
        private sealed class FlowBuilder
        {
            private readonly string _where;
            private readonly OracleResolver _lookup;
            private readonly Flow _flow;

            // the file's step Id -> the step built from it
            private readonly Dictionary<int, FlowStep> _byKey = new();

            // deferred step-to-step references, resolved once every step exists
            private readonly List<(string what, int key, Action<FlowStep?> apply)> _refs = new();

            private int _order = 0;

            public FlowBuilder(string where, OracleResolver lookup, Flow flow)
            {
                _where = where;
                _lookup = lookup;
                _flow = flow;
            }

            public string? Build(List<FlowStepDto> steps)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var dto = steps[i];
                    if (dto == null)
                        return $"{_where}: step #{i + 1} is empty.";
                    if (dto.Id is not int key)
                        return $"{_where}: step #{i + 1} has no id.";
                    var where = $"{_where}: step {key}";

                    var kindError = ResolveKind(dto.Kind, where, out var kind);
                    if (kindError != null) return kindError;

                    var step = new FlowStep
                    {
                        Kind = kind,
                        Label = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label.Trim(),
                        SortOrder = ++_order
                    };

                    if (!_byKey.TryAdd(key, step))
                        return $"{where}: two steps share the id {key}.";

                    var fillError = Fill(where, kind, dto, step);
                    if (fillError != null) return fillError;

                    _flow.Steps.Add(step);
                }

                return null;
            }

            public string? ResolveReferences()
            {
                foreach (var (what, key, apply) in _refs)
                {
                    if (!_byKey.TryGetValue(key, out var target))
                        return $"{what} points at step {key}, which is not in this flow.";
                    apply(target);
                }

                foreach (var step in _flow.Steps.Where(s => s.Kind == FlowStepKind.RollChosen))
                {
                    if (step.SourceStep == null)
                        return $"{_where}: a \"roll on another oracle\" step has no source.";
                    if (step.SourceStep.Kind != FlowStepKind.Choice && step.SourceStep.Kind != FlowStepKind.Roll)
                        return $"{_where}: a \"roll on another oracle\" step takes its oracle from a step that is neither a dropdown question nor a roll.";
                }

                _flow.Steps.OrderBy(s => s.SortOrder).ToList().Renumber();

                return null;
            }

            private string? Fill(string where, FlowStepKind kind, FlowStepDto dto, FlowStep step)
            {
                switch (kind)
                {
                    case FlowStepKind.Roll:
                        var oracleError = _lookup.Resolve(where, dto.Oracle, required: true, out var oracle);
                        if (oracleError != null) return oracleError;
                        step.Oracle = oracle;
                        return BuildBranches(where, dto.Branches, oracle!, step);

                    case FlowStepKind.Choice:
                        var qError = Question(where, dto.Question, step);
                        if (qError != null) return qError;
                        return BuildOptions(where, dto.Options, step);

                    case FlowStepKind.Text:
                        return Question(where, dto.Question, step);

                    case FlowStepKind.RollChosen:
                        if (dto.PickedAtStep == null)
                            return $"{where} rolls what an earlier step picks, but does not say which one.";
                        Defer($"{where} (picked at)", dto.PickedAtStep.Value, s => step.SourceStep = s);
                        return null;
                }

                return null;
            }

            private string? BuildOptions(string where, List<FlowChoiceOptionDto>? options, FlowStep step)
            {
                if (options == null || options.Count == 0)
                    return $"{where} is a dropdown question with no options.";

                for (int i = 0; i < options.Count; i++)
                {
                    var option = options[i];

                    if (option == null)
                        return $"{where} has an empty option.";
                    if (string.IsNullOrWhiteSpace(option.Label))
                        return $"{where} has an option with no label.";
                    if (FlowText.LabelProblem(option.Label, "An option label") is string labelProblem)
                        return $"{where}: {labelProblem}";

                    var error = _lookup.Resolve($"{where}, option \"{option.Label}\",",
                        option.Oracle, required: false, out var oracle);
                    if (error != null) return error;

                    var built = new FlowChoiceOption
                    {
                        SortOrder = i + 1,
                        Label = option.Label.Trim(),
                        Oracle = oracle,
                        EndsFlow = option.EndsFlow ?? false
                    };
                    step.Options.Add(built);

                    if (option.GoToStep != null)
                    {
                        Defer($"{where}, option \"{option.Label}\"", option.GoToStep.Value,
                            s => built.TargetStep = s);
                    }
                }

                return null;
            }

            private string? BuildBranches(string where, List<FlowBranchDto>? branches, Oracle oracle,
                FlowStep step)
            {
                if (branches == null) return null;

                var entriesBySortOrder = oracle.OracleEntries.ToDictionary(e => e.SortOrder);
                var seen = new HashSet<int>();

                foreach (var branchDto in branches)
                {
                    if (branchDto == null)
                        return $"{where} has an empty branch.";
                    if (!entriesBySortOrder.TryGetValue(branchDto.WhenOutcome, out var entry))
                        return $"{where} branches on outcome {branchDto.WhenOutcome}, which \"{oracle.Name}\" does not have.";
                    if (!seen.Add(branchDto.WhenOutcome))
                        return $"{where} has more than one branch on outcome {branchDto.WhenOutcome}.";

                    var picksError = _lookup.Resolve($"{where}, outcome {branchDto.WhenOutcome},",
                        branchDto.PicksOracle, required: false, out var picks);
                    if (picksError != null) return picksError;

                    var built = new FlowBranch
                    {
                        OracleEntry = entry,
                        Oracle = picks,
                        EndsFlow = branchDto.EndsFlow ?? false
                    };
                    step.Branches.Add(built);

                    if (branchDto.GoToStep != null)
                    {
                        Defer($"{where}, outcome {branchDto.WhenOutcome}", branchDto.GoToStep.Value,
                            s => built.TargetStep = s);
                    }
                }

                return null;
            }

            private void Defer(string what, int key, Action<FlowStep?> apply)
            {
                _refs.Add((what, key, apply));
            }

            private static string? ResolveKind(string? kindName, string where, out FlowStepKind kind)
            {
                kind = FlowStepKind.Roll;

                var name = string.IsNullOrWhiteSpace(kindName) ? nameof(FlowStepKind.Roll) : kindName;

                if (!Enum.TryParse(name, out kind) || !Enum.IsDefined(kind))
                    return $"{where}: \"{kindName}\" is not a known kind of step.";

                return null;
            }

            private static string? Question(string where, string? question, FlowStep step)
            {
                if (FlowText.QuestionProblem(question) is string problem)
                    return $"{where}: {problem}";
                step.Question = question!.Trim();
                return null;
            }
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
                    if (systemName == null) continue;

                    var key = Key(oracle.Name, systemName);
                    if (!lookup.TryAdd(key, oracle))
                        ambiguous.Add(key);
                }

                return lookup;
            }

            private static string Key(string name, string gameSystem)
            {
                return $"{name.Trim().ToLowerInvariant()}\n{gameSystem.Trim().ToLowerInvariant()}";
            }
        }
    }
}
