using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// One row of the flow list. 
    /// </summary>
    public class FlowListItem : IOwned
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? OwnerId { get; set; }
        public Visibility Visibility { get; set; }
        public string? Description { get; set; }

        public string GameSystemName { get; set; } = string.Empty;

        public int StepCount { get; set; }
        public int BranchCount { get; set; }

        /// <summary>
        /// True when a step points at an oracle with no outcomes or unset dice bands, a question
        /// has no text, or a "roll the picked oracle" step has no source. The flow would stall.
        /// </summary>
        public bool HasUnfinishedOracle { get; set; }

        public bool NeedsSetup => StepCount == 0 || HasUnfinishedOracle;
    }
}
