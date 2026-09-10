using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// The Steps page: the flow's steps in order, plus the fields for adding one more.
    /// </summary>
    public class FlowStepsViewModel
    {
        public int FlowID { get; set; }
        public string FlowName { get; set; } = string.Empty;

        /// <summary>Every step, in run order.</summary>
        public List<FlowStepRow> Rows { get; set; } = new List<FlowStepRow>();

        /// <summary>Steps a RollChosen step may take its oracle from (Choice or Roll).</summary>
        public List<BranchTarget> SourceSteps { get; set; } = new List<BranchTarget>();

        public FlowStepKind AddKind { get; set; } = FlowStepKind.Roll;
        public int AddOracleID { get; set; }
        public string? AddLabel { get; set; }
        public string? AddQuestion { get; set; }
        public int? AddSourceStepID { get; set; }
    }

    public class FlowStepRow
    {
        public int StepID { get; set; }

        /// <summary>"Step 2" -- position in the flow.</summary>
        public string PositionPath { get; set; } = string.Empty;

        public FlowStepKind Kind { get; set; }
        public string Label { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;
        public string OracleTypeLabel { get; set; } = string.Empty;

        public string? BlockedReason { get; set; }
        public string? Warning { get; set; }

        public List<string> BranchSummaries { get; set; } = new List<string>();
        public List<string> OptionSummaries { get; set; } = new List<string>();

        public string FallThroughLabel { get; set; } = string.Empty;

        public bool IsFirst { get; set; }
        public bool IsLast { get; set; }

        public bool CanBranch => Kind.CanBranch();
        public bool IsChoice => Kind == FlowStepKind.Choice;
    }
}
