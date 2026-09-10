namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// The Options page: the dropdown entries of one Choice step.
    /// </summary>
    public class FlowOptionsViewModel
    {
        public int FlowID { get; set; }
        public int StepID { get; set; }

        public string FlowName { get; set; } = string.Empty;
        public string StepLabel { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string PositionPath { get; set; } = string.Empty;

        public bool HasRollChosenStep { get; set; }

        public List<OptionRow> Rows { get; set; } = new List<OptionRow>();

        public string? AddLabel { get; set; }
        public int? AddOracleID { get; set; }

        public List<int> AddOracleIds { get; set; } = new List<int>();

        public List<OracleListItem> Available { get; set; } = new List<OracleListItem>();

        /// <summary>Every step in the flow, for the "goes to" picker.</summary>
        public List<BranchTarget> Targets { get; set; } = new List<BranchTarget>();

        /// <summary>What happens to an option that routes nowhere, spelled out for the picker.</summary>
        public string FallThroughLabel { get; set; } = string.Empty;
    }

    public class OptionRow
    {
        public int OptionID { get; set; }
        public int SortOrder { get; set; }
        public string? Label { get; set; }

        public int? OracleID { get; set; }

        /// <summary>
        /// Where picking this option sends the run, in the same three-state shape the branch
        /// editor uses: blank carries on, "end" stops, a number is a step's ID.
        /// </summary>
        public string? Target { get; set; }
    }
}
