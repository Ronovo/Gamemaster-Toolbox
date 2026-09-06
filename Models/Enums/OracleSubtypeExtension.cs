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
            switch(s)
            {
                case OracleSubtype.PlayingCards:
                case OracleSubtype.TarotCards:
                    return OracleCategory.Cards;
                default:
                    return OracleCategory.Dice;
            }
        }

        /// <summary>
        /// How many faces the die has. THROWS for PlayingCards and TarotCards, which have no
        /// face count — guard with Category() == OracleCategory.Dice before calling.
        /// Deliberately a throw rather than a 0: a card oracle asking its die size is a bug,
        /// and 0 would flow on silently into range checks that then pass everything.
        /// </summary>
        public static int FaceCount(this OracleSubtype s)
        {
            switch(s)
            {
                case OracleSubtype.D4: return 4;
                case OracleSubtype.D6: return 6;
                case OracleSubtype.D8: return 8;
                case OracleSubtype.D10: return 10;
                case OracleSubtype.D12: return 12;
                case OracleSubtype.D20: return 20;
                case OracleSubtype.D100: return 100;
                default:
                    throw new InvalidOperationException($"{s} has no face count");
            }
        }
    }
}
