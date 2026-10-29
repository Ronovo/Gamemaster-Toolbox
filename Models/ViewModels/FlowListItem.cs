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

        /// <summary>True when the flow can't run yet; FlowExtension.RunBlockedReason says why.</summary>
        public bool NeedsSetup { get; set; }
    }
}
