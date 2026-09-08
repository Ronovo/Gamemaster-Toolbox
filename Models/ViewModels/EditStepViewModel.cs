using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// The Edit Step page: renaming a step, fixing its question, or repointing it.
    /// </summary>
    public class EditStepViewModel
    {
        public int FlowID { get; set; }
        public int StepID { get; set; }

        public string FlowName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public FlowStepKind Kind { get; set; }

        public string? Label { get; set; }
        public string? Question { get; set; }
        public int? OracleID { get; set; }
        public int? SourceStepSortOrder { get; set; }

        public bool IsQuestion => Kind.IsQuestion();
        public bool IsRoll => Kind == FlowStepKind.Roll;
        public bool IsRollChosen => Kind == FlowStepKind.RollChosen;
        public string KindName => Kind.DisplayName();
    }
}
