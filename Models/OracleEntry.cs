using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// One outcome row on an oracle.
    ///
    /// Exactly one of the two keying halves below is populated, and which one is decided by
    /// the parent oracle's OracleSubtype — nothing on this row says which shape it is. Use
    /// KeyLabel() to render whichever half applies as a single column.
    /// </summary>
    public class OracleEntry
    {
        public int ID { get; set; }
        public int OracleID { get; set; }
        public int SortOrder { get; set; }
        [MaxLength(500)]
        public string Result { get; set; } = string.Empty;
        // Dice Keying (Null on Card Oracles)
        public int? RangeStart { get; set; }
        public int? RangeEnd { get; set; }
        // Card Keying (Null on Dice Oracles)
        public string? CardCode { get; set; }
        public string? CardLabel { get; set; }
    }
}
