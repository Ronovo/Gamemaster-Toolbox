using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    public class Sheet
    {
        public int ID { get; set; }

        [Required]
        public string OwnerId { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public int? GameSystemID { get; set; }
        public GameSystem? GameSystem { get; set; }

        public DateTime DateUploaded { get; set; }
        public DateTime DateUpdated { get; set; }

        public int CurrentVersion { get; set; } = 1;

        public ICollection<SheetVersion> Versions { get; set; } = new List<SheetVersion>();
    }
}
