namespace GM_Toolbox.Models.ViewModels
{
    public enum ExportScope
    {
        Everything = 0,
        AllOracles = 1,
        SelectedOracles = 2,
        SelectedFlows = 3
    }

    /// <summary>
    /// The Export page: what to export, and the list to pick from.
    /// </summary>
    public class ExportViewModel
    {
        public ExportScope Scope { get; set; } = ExportScope.Everything;


        public List<int> SelectedIds { get; set; } = new List<int>();

        public List<int> SelectedFlowIds { get; set; } = new List<int>();

        public string? ReturnTo { get; set; }

        public List<OracleListItem> Available { get; set; } = new List<OracleListItem>();

        public List<FlowListItem> AvailableFlows { get; set; } = new List<FlowListItem>();

        public int GameSystemCount { get; set; }
    }
}
