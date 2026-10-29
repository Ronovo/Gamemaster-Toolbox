using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Turns loaded Oracle entities into the portable file shape.
    ///
    /// Pure — no DbContext and no database access. The caller loads the oracles
    /// (with their entries) and hands them over.
    /// </summary>
    public class OracleExporter
    {
        public static OracleExportFile Build(List<Oracle> oracles)
        {
            var file = new OracleExportFile
            {
                ExportedUtc = DateTime.UtcNow
            };

            foreach (var oracle in oracles)
            {
                var dto = new OracleDto
                {
                    Name = oracle.Name,
                    GameSystem = oracle.GameSystem!.Name,
                    Subtype = oracle.OracleSubtype.ToString(),
                    DiceCount = oracle.DiceCount,
                    DiceFaces = oracle.DiceFaces
                };

                foreach (var entry in oracle.OracleEntries.OrderBy(e => e.SortOrder))
                {
                    dto.Entries.Add(new OracleEntryDto
                    {
                        SortOrder = entry.SortOrder,
                        Result = entry.Result,
                        RangeStart = entry.RangeStart,
                        RangeEnd = entry.RangeEnd,
                        CardCode = entry.CardCode,
                        CardLabel = entry.CardLabel
                    });
                }

                file.Oracles.Add(dto);
            }
            return file;
        }

        public static List<GameSystemDto> BuildSystems(IEnumerable<GameSystem> systems)
        {
            return systems
                .DistinctBy(s => s.ID)
                .OrderBy(s => s.Name)
                .Select(s => new GameSystemDto
                {
                    Name = s.Name,
                    Genre = s.Genre
                })
                .ToList();
        }
    }
}
