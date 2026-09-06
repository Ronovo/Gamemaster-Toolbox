using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.Enums
{
    // The numbering is out of order on purpose, and it is safe: the value is persisted as a
    // string via HasConversion<string>(), so these numbers never reach the database. Do not
    // tidy them anyway — 0 is what an unset OracleSubtype takes, and D6 is the right default
    // for that. Renumbering would silently change what a new oracle starts as.
    public enum OracleSubtype
    {
        D4 = 1,
        D6 = 0,
        D8 = 2, 
        D10 = 3, 
        D12 = 4, 
        D20 = 5, 
        D100 = 6,
        [Display(Name = "Playing Cards")]
        PlayingCards = 7,
        [Display(Name = "Tarot Cards")]
        TarotCards = 8
    }
}
