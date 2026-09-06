using GM_Toolbox.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    public class Oracle
    {
        public int ID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // [Range] rather than [Required]: an int FK has no "unset" state. It defaults to 0,
        // which is not a valid key, and [Required] is satisfied by 0. This is what keeps a
        // blank dropdown out of the database.
        [Display(Name = "Game System")]
        [Range(1, int.MaxValue, ErrorMessage = "Pick a game system.")]
        public int GameSystemID { get; set; }
        public GameSystem? GameSystem { get; set; }

        // D4-D100, PlayingCards or TarotCards. Category() derives dice-vs-cards from this,
        // and FaceCount() derives the die size — neither is stored, so they cannot disagree
        // with the subtype. (A DiceSize column existed once and was deleted for that reason.)
        [Display(Name="Oracle Type")]
        public OracleSubtype OracleSubtype { get; set; }

        public ICollection<OracleEntry> OracleEntries { get; set; } = new List<OracleEntry>();
    }
}
