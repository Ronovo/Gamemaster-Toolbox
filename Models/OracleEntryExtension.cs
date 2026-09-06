namespace GM_Toolbox.Models
{
    public static class OracleEntryExtension
    {
        /// <summary>
        /// The left-hand column on a list of outcomes: a card's name, a dice band,
        /// or a note that the band hasn't been set yet.
        /// </summary>
        public static string KeyLabel(this OracleEntry entry)
        {
            if (entry.CardLabel != null)
            {
                return entry.CardLabel;
            }

            if (entry.RangeStart == null)
            {
                return "not set";
            }

            if (entry.RangeStart == entry.RangeEnd)
            {
                return entry.RangeStart.Value.ToString();
            }

            return $"{entry.RangeStart}-{entry.RangeEnd}";
        }
    }
}