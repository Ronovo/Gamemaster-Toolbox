namespace GM_Toolbox.Models.Enums
{
    public static class OracleSubtypeExtension
    {
        /// <summary>
        /// The single source of truth for the dice-versus-cards split. Every branch on
        /// "is this a card oracle?" should come through here rather than testing subtypes.
        /// </summary>
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
