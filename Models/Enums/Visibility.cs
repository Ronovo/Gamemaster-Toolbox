using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.Enums
{
    public enum Visibility
    {
        [Display(Name = "Only me")]
        OnlyMe = 0,
        [Display(Name = "Everyone")]
        Everyone = 1
    }
}
