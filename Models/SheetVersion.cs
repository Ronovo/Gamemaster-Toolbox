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

        // Kept per-version (rather than relying solely on Sheet.FileName) so a version's
        // record of what was actually uploaded stays accurate even if filename matching
        // rules ever change.
        [Required, MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;
    }
}
