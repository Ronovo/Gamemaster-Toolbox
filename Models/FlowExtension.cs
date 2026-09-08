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

            foreach (var step in flow.Steps.OrderBy(s => s.SortOrder))
            {
                var reason = step.BlockedReason(flow);
                if (reason != null)
                {
                    return $"Step {step.SortOrder} ({step.DisplayLabel()}): {LowerFirst(reason)}";
                }
            }

            return null;
        }

        private static string LowerFirst(string text)
        {
            return text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text.Substring(1);
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
                case FlowStepKind.Number:
                    return string.IsNullOrWhiteSpace(step.Question)
                        ? "This question has no text yet. Use Edit to write it."
                        : null;

                case FlowStepKind.RollChosen:
                    var source = step.SourceStep(flow);
                    if (source == null)
                    {
                        return "This step rolls what an earlier question picks, but no question is set. Use Edit to pick one.";
                    }
                    if (source.Kind != FlowStepKind.Choice)
                    {
                        return $"This step takes its oracle from step {source.SortOrder}, which is not a dropdown question.";
                    }
                    if (!source.Options.Any(o => o.OracleID != null))
                    {
                        return $"Step {source.SortOrder} has no option that names an oracle, so there would be nothing to roll.";
                    }
                    return null;
            }

            return null;
        }

        public static string ControlLabel(this FlowStep step)
        {
            switch (step.Kind)
            {
                case FlowStepKind.Choice:
                case FlowStepKind.Text:
                case FlowStepKind.Number:
                    return "Continue";
                default:
                    return $"Roll: {step.DisplayLabel()}";
            }
        }

        public static FlowStep? SourceStep(this FlowStep step, Flow flow)
        {
            return step.SourceStepSortOrder == null
                ? null
                : flow.Steps.FirstOrDefault(s => s.SortOrder == step.SourceStepSortOrder.Value);
        }
    }
}
