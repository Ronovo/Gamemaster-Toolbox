namespace GM_Toolbox.Models.ViewModels
{
    public class RollOutcome
    {
        public bool IsDice { get; set; }

        public int? EntryID { get; set; }

        public int? Value { get; set; }

        public string Key { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public string Result { get; set; } = string.Empty;

        public int? CardsRemaining { get; set; }

        public bool Reshuffled { get; set; }
    }
}
