using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// One outcome row on an oracle.
    ///
    /// Exactly one of the two keying halves below is populated: RangeStart and RangeEnd on a dice oracle,
    /// CardCode and CardLabel on a card oracle. The parent oracle's OracleSubtype decides which; nothing on this
    /// row says. Use KeyLabel() to render whichever half applies as a single column.
    /// </summary>
    public class OracleEntry
    {
        public int ID { get; set; }
        public int OracleID { get; set; }
        public Oracle? Oracle { get; set; }
        public int SortOrder { get; set; }
        [MaxLength(500)]
        public string Result { get; set; } = string.Empty;
        public int? RangeStart { get; set; }
        public int? RangeEnd { get; set; }
        public string? CardCode { get; set; }
        public string? CardLabel { get; set; }
    }
}
