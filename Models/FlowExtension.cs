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

            return step.Oracle?.Name ?? $"Step {step.SortOrder}";
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
            }

            return null;
        }

        public static string ControlLabel(this FlowStep step)
        {
            switch (step.Kind)
            {
                case FlowStepKind.Choice:
                case FlowStepKind.Text:
                    return "Continue";
                default:
                    return $"Roll: {step.DisplayLabel()}";
            }
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
