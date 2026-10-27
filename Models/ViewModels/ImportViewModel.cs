namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// The Import page: every export file found under App_Data/Exports, each importable on its own.
    /// </summary>
    public class ImportViewModel
    {
        public string? ReturnTo { get; set; }

        public List<ImportFileItem> Files { get; set; } = new List<ImportFileItem>();
    }

    public class ImportFileItem
    {
        /// <summary>Relative to App_Data/Exports, with forward slashes. This is what the form posts back.</summary>
        public string Path { get; set; } = string.Empty;

        public string Folder { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime Modified { get; set; }
    }
}
