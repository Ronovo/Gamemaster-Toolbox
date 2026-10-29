using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    public class SheetVersion
    {
        public int ID { get; set; }

        public int SheetID { get; set; }
        public Sheet? Sheet { get; set; }

        public int Version { get; set; }

        [Required, MaxLength(64)]
        public string Checksum { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }

        [Required, MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;
    }
}
