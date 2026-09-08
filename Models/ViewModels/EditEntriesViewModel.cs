using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Models.ViewModels
{
    public class EditEntriesViewModel
    {
        public int OracleID { get; set; }
        public string? Name { get; set; }
        public bool IsDice { get; set; }
        public OracleSubtype OracleSubtype { get; set; }

        public int MinRoll { get; set; }
        public int MaxRoll { get; set; }
        public List<EntryRow> Rows { get; set; } = new List<EntryRow>();
    }

    public class EntryRow
    {
        public int EntryId { get; set; }
        public string? Label { get; set; } = string.Empty;
        public string? Result { get; set; }
        public int StartsAt { get; set; }
        public string? CardCode { get; set; }
        public string? Suit { get; set; }
        public string? RankKind { get; set; }
    }
}
