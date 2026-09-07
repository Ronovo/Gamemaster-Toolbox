using GM_Toolbox.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// One row of the oracle list. Carries only what the list renders, plus the two
    /// counts needed to work out whether the oracle is finished.
    /// </summary>
    public class OracleListItem
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Oracle Type")]
        public OracleSubtype OracleSubtype { get; set; }

        public int? DiceCount { get; set; }
        public int? DiceFaces { get; set; }

        public string TypeLabel => DiceSpec.For(OracleSubtype, DiceCount, DiceFaces)?.Label
            ?? OracleSubtype.DisplayName();

        [Display(Name = "Game System")]
        public string GameSystemName { get; set; } = string.Empty;

        public int EntryCount { get; set; }
        public bool HasUnbandedEntries { get; set; }

        /// <summary>
        /// True when the oracle cannot be used yet: either Configure was never run
        /// (no entries at all), or it is a dice oracle whose EditEntries was never
        /// saved (entries exist but no row has a RangeStart).
        /// </summary>
        public bool NeedsSetup
        {
            get
            {
                if (EntryCount == 0)
                {
                    return true;
                }

                if (OracleSubtype.Category() == OracleCategory.Dice && HasUnbandedEntries)
                {
                    return true;
                }

                return false;
            }
        }
    }
}