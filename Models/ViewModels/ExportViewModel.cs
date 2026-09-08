using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GM_Toolbox.Models.ViewModels
{
    public enum ExportScope
    {
        Everything = 0,
        AllOracles = 1,
        SelectedOracles = 2,
        SelectedFlows = 3
    }

    /// <summary>
    /// The Export page: what to export, and the list to pick from.
    /// </summary>
    public class ExportViewModel
    {
        public ExportScope Scope { get; set; } = ExportScope.Everything;

        public List<int> SelectedIds { get; set; } = new List<int>();

        public List<int> SelectedFlowIds { get; set; } = new List<int>();

        public string? ReturnTo { get; set; }

        public string? OracleName { get; set; }

        [Display(Name = "Game System")]
        public int? OracleGameSystemID { get; set; }

        [Display(Name = "Oracle Type")]
        public string? OracleType { get; set; }
        public int OraclePage { get; set; } = 1;

        public string? FlowName { get; set; }

        [Display(Name = "Game System")]
        public int? FlowGameSystemID { get; set; }
        public int FlowPage { get; set; } = 1;

        public PagedList<OracleListItem> Available { get; set; } = new();

        public PagedList<FlowListItem> AvailableFlows { get; set; } = new();

        public int GameSystemCount { get; set; }
        public int OracleCount { get; set; }
        public int FlowCount { get; set; }

        public IEnumerable<SelectListItem> GameSystems { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> Subtypes { get; set; } = Array.Empty<SelectListItem>();

        public bool CatalogEmpty => GameSystemCount == 0 && OracleCount == 0 && FlowCount == 0;

        public Dictionary<string, string?> OracleFilterRoute => new(StringComparer.OrdinalIgnoreCase)
        {
            ["ReturnTo"] = ReturnTo,
            ["OracleName"] = OracleName,
            ["OracleGameSystemID"] = OracleGameSystemID?.ToString(),
            ["OracleType"] = OracleType,
            ["FlowName"] = FlowName,
            ["FlowGameSystemID"] = FlowGameSystemID?.ToString(),
            ["FlowPage"] = FlowPage > 1 ? FlowPage.ToString() : null
        };

        public Dictionary<string, string?> FlowFilterRoute => new(StringComparer.OrdinalIgnoreCase)
        {
            ["ReturnTo"] = ReturnTo,
            ["OracleName"] = OracleName,
            ["OracleGameSystemID"] = OracleGameSystemID?.ToString(),
            ["OracleType"] = OracleType,
            ["OraclePage"] = OraclePage > 1 ? OraclePage.ToString() : null,
            ["FlowName"] = FlowName,
            ["FlowGameSystemID"] = FlowGameSystemID?.ToString()
        };
    }
}
