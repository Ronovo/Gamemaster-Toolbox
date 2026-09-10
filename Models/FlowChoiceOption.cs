using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// One entry in a Choice step's dropdown.
    /// </summary>
    public class FlowChoiceOption
    {
        public int ID { get; set; }

        public int FlowStepID { get; set; }
        public FlowStep? FlowStep { get; set; }

        public int SortOrder { get; set; }

        [Required(ErrorMessage = "An option needs something to call it.")]
        [MaxLength(100)]
        public string Label { get; set; } = string.Empty;

        [Display(Name = "Rolls on")]
        public int? OracleID { get; set; }
        public Oracle? Oracle { get; set; }

        /// <summary>
        /// "Picking this option sends the run to this step" (by ID). Null keeps the old
        /// behaviour, where an option only names an oracle and the run carries on.
        /// </summary>
        [Display(Name = "Goes to step")]
        public int? TargetStepID { get; set; }
        public FlowStep? TargetStep { get; set; }

        /// <summary>
        /// "Picking this option stops the run." A branch says this with a row whose target is
        /// null, but an option row always exists, so it needs saying outright.
        /// </summary>
        public bool EndsFlow { get; set; }

        /// <summary>Whether picking this option sends the run somewhere other than straight on.</summary>
        public bool Routes => TargetStepID != null || EndsFlow;
    }
}
