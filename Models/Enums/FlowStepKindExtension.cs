using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GM_Toolbox.Models.Enums
{
    public static class FlowStepKindExtension
    {
        public static bool IsQuestion(this FlowStepKind kind)
        {
            switch (kind)
            {
                case FlowStepKind.Choice:
                case FlowStepKind.Text:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Only a Roll step has fixed outcomes to branch on.</summary>
        public static bool CanBranch(this FlowStepKind kind)
        {
            return kind == FlowStepKind.Roll;
        }

        public static string DisplayName(this FlowStepKind kind)
        {
            var display = typeof(FlowStepKind)
                .GetField(kind.ToString())
                ?.GetCustomAttribute<DisplayAttribute>();

            return display?.Name ?? kind.ToString();
        }
    }
}
