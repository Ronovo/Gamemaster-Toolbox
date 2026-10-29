using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// One row of the sheet list.
    /// </summary>
    public class SheetListItem
    {
        public int ID { get; set; }
        public string FileName { get; set; } = string.Empty;

        [Display(Name = "Game System")]
        public string? GameSystemName { get; set; }

        [Display(Name = "Date Uploaded")]
        public DateTime DateUploaded { get; set; }

        [Display(Name = "Date Updated")]
        public DateTime DateUpdated { get; set; }

        public int CurrentVersion { get; set; }
        public string CurrentChecksum { get; set; } = string.Empty;
    }
}
