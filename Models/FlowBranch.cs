namespace GM_Toolbox.Models
{
    /// <summary>
    /// "If this step rolled that outcome, go here instead of to the next step."
    /// </summary>
    public class FlowBranch
    {
        public int ID { get; set; }
        public int FlowStepID { get; set; }
        public FlowStep? FlowStep { get; set; }
        public int OracleEntryID { get; set; }
        public OracleEntry? OracleEntry { get; set; }
        public int? TargetStepSortOrder { get; set; }
    }
}
