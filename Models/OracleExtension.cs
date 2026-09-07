using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models
{
    public static class OracleExtension
    {
        public static DiceSpec? Dice(this Oracle oracle)
        {
            return DiceSpec.For(oracle.OracleSubtype, oracle.DiceCount, oracle.DiceFaces);
        }

        public static string TypeLabel(this Oracle oracle)
        {
            return oracle.Dice()?.Label ?? oracle.OracleSubtype.DisplayName();
        }

        public static string? RollBlockedReason(this Oracle oracle)
        {
            if (oracle.OracleEntries.Count == 0)
            {
                return "This oracle has no outcomes yet. Use Configure to create them.";
            }

            if (oracle.OracleSubtype.Category() == OracleCategory.Dice
                && oracle.OracleEntries.Any(e => e.RangeStart == null))
            {
                return "The roll ranges haven't been set yet. Use Configure to finish this oracle.";
            }

            if (oracle.OracleSubtype.Category() == OracleCategory.Dice && oracle.Dice() == null)
            {
                return "This oracle's dice haven't been set. Use Edit to set them.";
            }

            return null;
        }
    }
}
