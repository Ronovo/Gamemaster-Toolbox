using System.ComponentModel.DataAnnotations;
using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models
{
    public class GameSystem
    {
        public int ID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Genre { get; set; } = string.Empty;

        // Stored and editable, but nothing reads it yet. Intended as the pre-selected subtype
        // when creating an oracle for this system. Not a wiring bug — just not wired up.
        [Display(Name = "Primary Oracle Type")]
        public OracleSubtype? PrimaryOracleType { get; set; }
    }
}