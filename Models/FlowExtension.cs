using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models
{
    public static class FlowExtension
    {
        /// <summary>
        /// Why this flow cannot be run, or null when it can.
        /// </summary>
        public static string? RunBlockedReason(this Flow flow)
        {
            if (flow.Steps.Count == 0)
            {
                return "This flow has no steps yet. Use Steps to build the chain.";
            }

            foreach (var step in flow.InRunOrder())
            {
                var reason = step.BlockedReason(flow);
                if (reason != null)
                {
                    return $"{step.PositionPath()} ({step.DisplayLabel()}): {LowerFirst(reason)}";
                }
            }

            return null;
        }

        private static string LowerFirst(string text)
        {
            return text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>Every step, in run order. The run starts at the first.</summary>
        public static IEnumerable<FlowStep> InRunOrder(this Flow flow)
        {
            return flow.Steps.OrderBy(s => s.SortOrder);
        }
    }

    public static class FlowStepExtension
    {
        public static string DisplayLabel(this FlowStep step)
        {
            if (!string.IsNullOrWhiteSpace(step.Label))
            {
                return step.Label;
            }

            if (step.Kind.IsQuestion() && !string.IsNullOrWhiteSpace(step.Question))
            {
                return step.Question;
            }

            if (step.Kind == FlowStepKind.Loop)
            {
                return $"Loop Step {step.SortOrder + 1}";
            }

            return step.Oracle?.Name ?? $"Step {step.SortOrder}";
        }

        /// <summary>What the run asks at this step: the question text, or a Loop's count prompt.</summary>
        public static string Prompt(this FlowStep step, Flow flow)
        {
            if (step.Kind != FlowStepKind.Loop)
            {
                return step.Question ?? string.Empty;
            }

            var next = step.NextInOrder(flow);
            return next == null
                ? "Loop how many times?"
                : $"Loop {next.PositionPath()} ({next.DisplayLabel()}): how many times?";
        }

        public static FlowStep? NextInOrder(this FlowStep step, Flow flow)
        {
            var ordered = flow.InRunOrder().ToList();
            var index = ordered.FindIndex(s => s.ID == step.ID);
            return index >= 0 && index + 1 < ordered.Count ? ordered[index + 1] : null;
        }

        /// <summary>A readable position, "Step 2".</summary>
        public static string PositionPath(this FlowStep step)
        {
            return $"Step {step.SortOrder}";
        }

        public static string? BlockedReason(this FlowStep step, Flow flow)
        {
            switch (step.Kind)
            {
                case FlowStepKind.Roll:
                    if (step.Oracle == null)
                    {
                        return "This step points at an oracle that no longer exists.";
                    }
                    return step.Oracle.RollBlockedReason();

                case FlowStepKind.Choice:
                    if (string.IsNullOrWhiteSpace(step.Question))
                    {
                        return "This question has no text yet. Use Edit to write it.";
                    }
                    if (step.Options.Count == 0)
                    {
                        return "This question has no options yet. Use Options to add some.";
                    }
                    return null;

                case FlowStepKind.Text:
                    return string.IsNullOrWhiteSpace(step.Question)
                        ? "This question has no text yet. Use Edit to write it."
                        : null;

                case FlowStepKind.RollChosen:
                    var source = step.SourceStep(flow);
                    if (source == null)
                    {
                        return "This step rolls what an earlier step picks, but no source is set. Use Edit to pick one.";
                    }
                    if (source.Kind == FlowStepKind.Choice)
                    {
                        if (!source.Options.Any(o => o.OracleID != null))
                        {
                            return $"{source.PositionPath()} has no option that names an oracle, so there would be nothing to roll.";
                        }
                        return null;
                    }
                    if (source.Kind == FlowStepKind.Roll)
                    {
                        if (!source.Branches.Any(b => b.OracleID != null))
                        {
                            return $"{source.PositionPath()} has no outcome that picks a table, so there would be nothing to roll.";
                        }
                        return null;
                    }
                    return $"This step takes its oracle from {source.PositionPath()}, which neither asks a question nor rolls.";

                case FlowStepKind.Loop:
                    var next = step.NextInOrder(flow);
                    if (next == null)
                    {
                        return "Nothing comes after this loop. Add a roll step after it.";
                    }
                    if (next.Kind != FlowStepKind.Roll && next.Kind != FlowStepKind.RollChosen)
                    {
                        return $"A loop repeats a roll, but {next.PositionPath()} does not roll. Move a roll step right after it.";
                    }
                    return null;
            }

            return null;
        }

        public static string ControlLabel(this FlowStep step)
        {
            return step.Kind.IsQuestion() ? "Continue" : $"Roll: {step.DisplayLabel()}";
        }

        public static bool AllowsBranches(this FlowStep step)
        {
            return step.Kind.CanBranch();
        }

        public static FlowStep? SourceStep(this FlowStep step, Flow flow)
        {
            return step.SourceStepID == null
                ? null
                : flow.Steps.FirstOrDefault(s => s.ID == step.SourceStepID.Value);
        }
    }
}
