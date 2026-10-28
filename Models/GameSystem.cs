using GM_Toolbox.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    public class GameSystem : IOwned
    {
        public int ID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Genre { get; set; } = string.Empty;

        public string? OwnerId { get; set; }

        [Display(Name = "Who can see this")]
        public Visibility Visibility { get; set; }
    }
}