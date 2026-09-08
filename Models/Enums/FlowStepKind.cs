using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.Enums
{
    public enum FlowStepKind
    {
        [Display(Name = "Roll an oracle")]
        Roll = 0,

        [Display(Name = "Ask a question (dropdown)")]
        Choice = 1,

        [Display(Name = "Ask a question (text)")]
        Text = 2,

        [Display(Name = "Ask a question (number)")]
        Number = 3,

        [Display(Name = "Roll the oracle picked earlier")]
        RollChosen = 4
    }
}
