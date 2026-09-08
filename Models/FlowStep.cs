using GM_Toolbox.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// One link in a flow. What it does when the run reaches it is decided by Kind: it rolls an
    /// oracle, asks a question, or rolls whichever oracle an earlier question picked.
    /// </summary>
    public class FlowStep : IValidatableObject
    {
        public int ID { get; set; }

        public int FlowID { get; set; }
        public Flow? Flow { get; set; }

        public int SortOrder { get; set; }

        [Display(Name = "Step type")]
        public FlowStepKind Kind { get; set; } = FlowStepKind.Roll;

        [Display(Name = "Oracle")]
        public int? OracleID { get; set; }
        public Oracle? Oracle { get; set; }

        [MaxLength(100)]
        public string? Label { get; set; }

        [MaxLength(200)]
        public string? Question { get; set; }

        [Display(Name = "Picked at step")]
        public int? SourceStepSortOrder { get; set; }

        public ICollection<FlowBranch> Branches { get; set; } = new List<FlowBranch>();

        public ICollection<FlowChoiceOption> Options { get; set; } = new List<FlowChoiceOption>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            switch (Kind)
            {
                case FlowStepKind.Roll:
                    if (OracleID == null || OracleID <= 0)
                    {
                        yield return new ValidationResult(
                            "Pick an oracle for this step to roll.", new[] { nameof(OracleID) });
                    }
                    break;

                case FlowStepKind.Choice:
                case FlowStepKind.Text:
                case FlowStepKind.Number:
                    if (string.IsNullOrWhiteSpace(Question))
                    {
                        yield return new ValidationResult(
                            "Type the question this step asks.", new[] { nameof(Question) });
                    }
                    break;

                case FlowStepKind.RollChosen:
                    if (SourceStepSortOrder == null)
                    {
                        yield return new ValidationResult(
                            "Say which question step this one takes its oracle from.",
                            new[] { nameof(SourceStepSortOrder) });
                    }
                    break;
            }
        }
    }
}
