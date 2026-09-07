using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.Enums
{
    // Persisted as a string via HasConversion<string>(), so the numeric values never reach
    // the database and are free to be tidy. Dice = 0 is what an unset OracleSubtype takes,
    // and a dice oracle is the right default.
    //
    // This says only what kind of oracle it is. A dice oracle's geometry lives in the
    // Oracle.DiceCount / Oracle.DiceFaces columns (every dice oracle, not just "custom"
    // ones); the two card kinds pick which deck DeckFactory builds.
    public enum OracleSubtype
    {
        Dice = 0,
        [Display(Name = "Playing Cards")]
        PlayingCards = 1,
        [Display(Name = "Tarot Cards")]
        TarotCards = 2,
    }
}
