using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    public class Sheet : IOwned
    {
        public int ID { get; set; }

        public string? OwnerId { get; set; }

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
