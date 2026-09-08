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
    }
}
