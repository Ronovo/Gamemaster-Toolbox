using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
    public class EditEntriesViewModel
    {
        public int OracleID { get; set; }
        public string? Name { get; set; }
        public bool IsDice { get; set; }

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
    }
}
