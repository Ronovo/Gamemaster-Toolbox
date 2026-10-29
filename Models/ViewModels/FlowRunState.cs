namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// A run of one flow, held in the server's session store (memory, so a restart ends it).
    ///
    /// Never saved to the database and never exported.
    /// </summary>
    public class FlowRunState
    {
        public int FlowID { get; set; }

        public List<FlowRunStep> Transcript { get; set; } = new List<FlowRunStep>();

        public int? CurrentStepID { get; set; }

        public int StepsRolled { get; set; }

        public Dictionary<int, int> ChosenOracles { get; set; } = new Dictionary<int, int>();

        /// <summary>How many times the next roll repeats, set by a Loop step.</summary>
        public int? PendingRepeat { get; set; }

        public bool HitStepLimit { get; set; }

        public bool Finished => CurrentStepID == null;
    }

    public class FlowRunStep
    {
        public int StepID { get; set; }
        public string Label { get; set; } = string.Empty;
        public string OracleName { get; set; } = string.Empty;
        public RollOutcome Outcome { get; set; } = new RollOutcome();
        public List<RollOutcome> Outcomes { get; set; } = new List<RollOutcome>();
        public bool IsQuestion { get; set; }
    }
}
