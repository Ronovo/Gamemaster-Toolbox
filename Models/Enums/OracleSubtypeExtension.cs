namespace GM_Toolbox.Models.Enums
{
    public static class OracleSubtypeExtension
    {
        /// <summary>Which oracles roll dice and which draw cards.</summary>
        public static OracleCategory Category(this OracleSubtype s)
        {
            switch (s)
            {
                case OracleSubtype.PlayingCards:
                case OracleSubtype.TarotCards:
                    return OracleCategory.Cards;
                default:
                    return OracleCategory.Dice;
            }
        }
    }
}
