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
        public int StepSortOrder { get; set; }

        public bool HasRollChosenStep { get; set; }

        public List<OptionRow> Rows { get; set; } = new List<OptionRow>();

        public string? AddLabel { get; set; }
        public int? AddOracleID { get; set; }

        public List<int> AddOracleIds { get; set; } = new List<int>();

        public List<OracleListItem> Available { get; set; } = new List<OracleListItem>();
    }

    public class OptionRow
    {
        public int OptionID { get; set; }
        public int SortOrder { get; set; }
        public string? Label { get; set; }

        public int? OracleID { get; set; }
    }
}
