using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// What one of a Roll step's outcomes does: name the table a later RollChosen step
    /// rolls, and/or (advanced) send the run to another step.
    /// </summary>
    public class FlowBranch
    {
        public int ID { get; set; }
        public int FlowStepID { get; set; }
        public FlowStep? FlowStep { get; set; }
        public int OracleEntryID { get; set; }
        public OracleEntry? OracleEntry { get; set; }

        /// <summary>
        /// The table this outcome picks. A later RollChosen step pointed at this Roll rolls
        /// it. Null means the outcome names no table.
        /// </summary>
        [Display(Name = "Picks table")]
        public int? OracleID { get; set; }
        public Oracle? Oracle { get; set; }

        /// <summary>
        /// Advanced: rolling this outcome sends the run to this step by ID. Null carries on
        /// to the next step.
        /// </summary>
        public int? TargetStepID { get; set; }
        public FlowStep? TargetStep { get; set; }

        /// <summary>Advanced: rolling this outcome stops the run.</summary>
        public bool EndsFlow { get; set; }

        /// <summary>Whether this outcome sends the run somewhere other than straight on.</summary>
        public bool Routes => TargetStepID != null || EndsFlow;
    }
}
