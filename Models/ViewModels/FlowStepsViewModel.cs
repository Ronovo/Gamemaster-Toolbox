using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// The Steps page: the chain as it stands, plus the fields for adding one more link.
    /// </summary>
    public class FlowStepsViewModel
    {
        public int FlowID { get; set; }
        public string FlowName { get; set; } = string.Empty;

        public List<FlowStepRow> Rows { get; set; } = new List<FlowStepRow>();

        public List<BranchTarget> ChoiceSteps { get; set; } = new List<BranchTarget>();

        public FlowStepKind AddKind { get; set; } = FlowStepKind.Roll;
        public int AddOracleID { get; set; }
        public string? AddLabel { get; set; }
        public string? AddQuestion { get; set; }
        public int? AddSourceStepSortOrder { get; set; }
    }

    public class FlowStepRow
    {
        public int StepID { get; set; }
        public int SortOrder { get; set; }
        public FlowStepKind Kind { get; set; }
        public string Label { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public string OracleTypeLabel { get; set; } = string.Empty;

        public string? BlockedReason { get; set; }

        public string? Warning { get; set; }

        public List<string> BranchSummaries { get; set; } = new List<string>();

        public List<string> OptionSummaries { get; set; } = new List<string>();

        public string FallThroughLabel { get; set; } = string.Empty;

        public bool CanBranch => Kind.CanBranch();
        public bool IsChoice => Kind == FlowStepKind.Choice;
    }
}
