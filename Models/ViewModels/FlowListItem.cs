namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// One row of the flow list. 
    /// </summary>
    public class FlowListItem
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public string GameSystemName { get; set; } = string.Empty;

        public int StepCount { get; set; }
        public int BranchCount { get; set; }

        /// <summary>
        /// True when a step points at an oracle that has no outcomes, or a dice oracle whose
        /// bands were never set. Either way the flow would stall partway through a run.
        /// </summary>
        public bool HasUnfinishedOracle { get; set; }

        public bool NeedsSetup => StepCount == 0 || HasUnfinishedOracle;
    }
}
