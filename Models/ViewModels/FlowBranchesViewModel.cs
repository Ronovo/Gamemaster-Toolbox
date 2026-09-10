namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// The Branches page: one row per outcome of one step's oracle, and the list of places a
    /// row is allowed to send the run.
    /// </summary>
    public class FlowBranchesViewModel
    {
        public int FlowID { get; set; }
        public int StepID { get; set; }

        public string FlowName { get; set; } = string.Empty;
        public string StepLabel { get; set; } = string.Empty;
        public string OracleName { get; set; } = string.Empty;
        public string PositionPath { get; set; } = string.Empty;
        public bool IsDice { get; set; }

        public bool CanBranch { get; set; }

        public string KindName { get; set; } = string.Empty;

        public string FallThroughLabel { get; set; } = string.Empty;

        public List<BranchRow> Rows { get; set; } = new List<BranchRow>();

        /// <summary>Steps a row's "go to" picker may name.</summary>
        public List<BranchTarget> Targets { get; set; } = new List<BranchTarget>();

        /// <summary>Oracles a row's "picks table" picker may name.</summary>
        public List<OracleListItem> Oracles { get; set; } = new List<OracleListItem>();
    }

    public class BranchRow
    {
        public const string EndTarget = "end";

        public int EntryID { get; set; }

        public string? OutcomeKey { get; set; }

        public string? Result { get; set; }

        /// <summary>The table this outcome picks, or null for none.</summary>
        public int? OracleID { get; set; }

        /// <summary>
        /// Advanced routing: blank carries on, "end" stops the run, a number is the target
        /// step's ID.
        /// </summary>
        public string? Target { get; set; }
    }

    public class BranchTarget
    {
        public int StepID { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
