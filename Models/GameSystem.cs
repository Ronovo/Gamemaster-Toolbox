using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    public class GameSystem
    {
        public int ID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Genre { get; set; } = string.Empty;
    }
}