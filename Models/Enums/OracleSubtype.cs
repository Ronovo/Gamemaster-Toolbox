using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.Enums
{
    // Persisted as a string via HasConversion<string>(), so the numeric values never reach
    // the database. Dice = 0 is what an unset OracleSubtype takes.
    public enum OracleSubtype
    {
        Dice = 0,
        [Display(Name = "Playing Cards")]
        PlayingCards = 1,
        [Display(Name = "Tarot Cards")]
        TarotCards = 2,
    }
}
