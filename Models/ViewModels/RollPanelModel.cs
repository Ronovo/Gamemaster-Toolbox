namespace GM_Toolbox.Models.ViewModels
{
    /// <summary>
    /// The shared result panel — the big number, the caption and the outcome text — as rendered
    /// by Views/Shared/_RollPanel.cshtml for both Oracles/Roll and Flows/Run.
    /// </summary>
    public class RollPanelModel
    {
        public string Id { get; set; } = "rollPanel";

        public RollOutcome? Outcome { get; set; }

        public IReadOnlyList<RollOutcome>? Outcomes { get; set; }

        public string? Error { get; set; }

        public string? Label { get; set; }
    }
}
