namespace GM_Toolbox.Models.Transfer
{
    /// <summary>
    /// The whole export file — one of these is what gets serialised to the .json
    /// the user downloads, and what an uploaded .json is read back into.
    ///
    /// Deliberately carries no database IDs and no owner. See OracleDto.
    /// </summary>
    public class OracleExportFile
    {
        public int FormatVersion { get; set; } = 1;
        public DateTime ExportedUtc { get; set; }
        public List<OracleDto> Oracles { get; set; } = new List<OracleDto>();
    }

    /// <summary>
    /// One oracle, as it appears in the file.
    ///
    /// GameSystem and Subtype are strings, not enums, on purpose: an unknown value
    /// like "D7" should come back as a readable error naming the oracle, not as a
    /// deserialisation crash. The importer parses them itself.
    ///
    /// MULTI-USER: no owner field belongs here, now or later. Ownership is a fact
    /// about a database row, not about a portable file — importing someone else's
    /// export must stamp it with the importing account, not honour a baked-in id.
    /// </summary>
    public class OracleDto
    {
        public string Name { get; set; } = string.Empty;
        public string GameSystem { get; set; } = string.Empty;
        public string Subtype { get; set; } = string.Empty;
        public List<OracleEntryDto> Entries { get; set; } = new List<OracleEntryDto>();
    }

    /// <summary>
    /// One outcome. The dice half (RangeStart/RangeEnd) and the card half
    /// (CardCode/CardLabel) are nullable because exactly one of them applies.
    /// </summary>
    public class OracleEntryDto
    {
        public int SortOrder { get; set; }
        public string Result { get; set; } = string.Empty;
        public int? RangeStart { get; set; }
        public int? RangeEnd { get; set; }
        public string? CardCode { get; set; }
        public string? CardLabel { get; set; }
    }
}
