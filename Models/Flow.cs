using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models
{
    /// <summary>
    /// A chain of oracles, rolled one step at a time.
    /// </summary>
    public class Flow
    {
        public int ID { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Game System")]
        [Range(1, int.MaxValue, ErrorMessage = "Pick a game system.")]
        public int GameSystemID { get; set; }
        public GameSystem? GameSystem { get; set; }

        public ICollection<FlowStep> Steps { get; set; } = new List<FlowStep>();
    }
}
