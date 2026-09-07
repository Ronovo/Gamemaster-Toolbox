using GM_Toolbox.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// The Core Oracle Object
    /// </summary>
    public class Oracle : IValidatableObject
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

        [Display(Name="Oracle Type")]
        public OracleSubtype OracleSubtype { get; set; }

        [Display(Name = "Number of dice")]
        [Range(1, 10, ErrorMessage = "Roll between 1 and 10 dice.")]
        public int? DiceCount { get; set; }

        [Display(Name = "Dice faces")]
        [Range(2, 100, ErrorMessage = "A die needs between 2 and 100 faces.")]
        public int? DiceFaces { get; set; }

        public ICollection<OracleEntry> OracleEntries { get; set; } = new List<OracleEntry>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (OracleSubtype.Category() != OracleCategory.Dice)
            {
                yield break;
            }

            if (DiceCount == null || DiceFaces == null)
            {
                yield return new ValidationResult(
                    "Set both the number of dice and the number of faces.",
                    new[] { nameof(DiceCount), nameof(DiceFaces) });
            }
        }
    }
}
