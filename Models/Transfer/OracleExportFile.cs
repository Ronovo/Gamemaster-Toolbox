using System.Text.Json.Serialization;

namespace GM_Toolbox.Models.Transfer
{
    /// <summary>
    /// The whole export file — one of these is what gets serialised to the .json
    /// the user downloads, and what an uploaded .json is read back into.
    ///
    /// Deliberately carries no database IDs and no owner. See OracleDto.
    /// </summary>
    public class OracleExportFile
    {
        public int FormatVersion { get; set; } = 10;
        public DateTime ExportedUtc { get; set; }

        public List<GameSystemDto> GameSystems { get; set; } = new List<GameSystemDto>();

        public List<OracleDto> Oracles { get; set; } = new List<OracleDto>();

        public List<FlowDto> Flows { get; set; } = new List<FlowDto>();
    }

    /// <summary>
    /// One game system, as it appears in the file.
    /// </summary>
    public class GameSystemDto
    {
        public string Name { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
    }

    /// <summary>
    /// One oracle, as it appears in the file.
    ///
    /// </summary>
    public class OracleDto
    {
        public string Name { get; set; } = string.Empty;
        public string GameSystem { get; set; } = string.Empty;
        public string Subtype { get; set; } = string.Empty;

        public int? DiceCount { get; set; }
        public int? DiceFaces { get; set; }

        public List<OracleEntryDto> Entries { get; set; } = new List<OracleEntryDto>();
    }

    /// <summary>
    /// One outcome. The dice half (RangeStart/RangeEnd) and the card half
    /// (CardCode/CardLabel) are nullable because exactly one of them applies.
    /// </summary>
    public class OracleEntryDto
    {
        public int SortOrder { get; set; }
        public string Result { get; set; } = string.Empty;
        public int? RangeStart { get; set; }
        public int? RangeEnd { get; set; }
        public string? CardCode { get; set; }
        public string? CardLabel { get; set; }
    }

    /// <summary>
    /// One flow, as it appears in the file.
    /// </summary>
    public class FlowDto
    {
        public string Name { get; set; } = string.Empty;
        public string GameSystem { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<FlowStepDto> Steps { get; set; } = new List<FlowStepDto>();
    }

    public class FlowStepDto
    {
        /// <summary>
        /// A per-file id, its position in the flow, unique within the flow. PickedAtStep and
        /// GoToStep reference it. Older files number steps by SortOrder instead, and the
        /// importer maps those across.
        /// </summary>
        public int? Id { get; set; }

        /// <summary>Position in the flow. Informational (order is document order).</summary>
        public int SortOrder { get; set; }

        public string? Kind { get; set; }

        public string? Label { get; set; }

        public OracleRefDto? Oracle { get; set; }

        public string? Question { get; set; }

        /// <summary>The step a RollChosen step takes its oracle from: its Id, or SortOrder in older files.</summary>
        public int? PickedAtStep { get; set; }

        /// <summary>Read only, from older files: the labels on a retired Fields step (now a Text question).</summary>
        public List<string>? Fields { get; set; }

        /// <summary>Read only, from older files: steps nested inside a retired Loop (now kept in sequence).</summary>
        public List<FlowStepDto>? Steps { get; set; }

        public List<FlowChoiceOptionDto>? Options { get; set; }

        public List<FlowBranchDto>? Branches { get; set; }
    }

    /// <summary>
    /// One entry in a dropdown question, as it appears in the file.
    /// </summary>
    public class FlowChoiceOptionDto
    {
        public int SortOrder { get; set; }

        /// <summary>The step this option routes to: its Id, or SortOrder in older files. Null carries on.</summary>
        public int? GoToStep { get; set; }

        public bool? EndsFlow { get; set; }
        public string Label { get; set; } = string.Empty;
        public OracleRefDto? Oracle { get; set; }
    }

    /// <summary>
    /// How a step names the oracle it rolls.
    /// </summary>
    public class OracleRefDto
    {
        public string Name { get; set; } = string.Empty;
        public string GameSystem { get; set; } = string.Empty;
    }

    public class FlowBranchDto
    {
        /// <summary>The SortOrder of the outcome that fires this branch, on the step's own oracle.</summary>
        public int WhenOutcome { get; set; }

        /// <summary>The table this outcome picks for a later RollChosen step.</summary>
        public OracleRefDto? PicksOracle { get; set; }

        /// <summary>Advanced: the step to jump to -- its Id, or SortOrder in older files. Null carries on.</summary>
        public int? GoToStep { get; set; }

        /// <summary>Advanced: this outcome ends the run.</summary>
        public bool? EndsFlow { get; set; }
    }
}
