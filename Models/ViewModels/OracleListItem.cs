using GM_Toolbox.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
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