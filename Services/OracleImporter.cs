using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Validates a parsed export file and turns it into Oracle entities ready to add.
    ///
    /// Pure — no DbContext. Returns an error message or null, the same contract
    /// OracleConfigurator.Reconcile uses, so the controller owns ModelState.
    ///
    /// All-or-nothing: any validation failure returns an error and leaves oracles empty.
    /// One bad oracle means nothing is imported, which is what lets Clear All be a safe
    /// step — export, clear, re-import either restores everything or changes nothing.
    /// </summary>
    public static class OracleImporter
    {
        public const int SupportedFormatVersion = 2;

        public static string? TryBuild(OracleExportFile? file, IEnumerable<GameSystem> existingSystems, out List<Oracle> oracles)
        {
            // Two lists on purpose. Work accumulates in `imported`, and `oracles` is only
            // pointed at it once every oracle in the file has validated.
            oracles = new List<Oracle>();
            var imported = new List<Oracle>();
            var systemsByName = existingSystems.ToDictionary(
                g => g.Name, g => g, StringComparer.OrdinalIgnoreCase);

            if (file == null)
                return "That file could not be read as an oracle export.";

            if (file.FormatVersion > SupportedFormatVersion)
                return $"This file is format version {file.FormatVersion}; this app understands version {SupportedFormatVersion}.";

            if (file.Oracles == null || file.Oracles.Count == 0)
                return "The file contains no oracles.";

            for (int i = 0; i < file.Oracles.Count; i++)
            {
                var dto = file.Oracles[i];
                var label = $"Oracle {i + 1}";

                if (string.IsNullOrWhiteSpace(dto.Name))
                    return $"{label}: name is missing.";

                if (dto.Name.Length > 100)
                    return $"{label} (\"{dto.Name}\"): name is longer than 100 characters.";

                if (!Enum.TryParse<OracleSubtype>(dto.Subtype, out var subtype) || !Enum.IsDefined(subtype))
                    return $"{label} (\"{dto.Name}\"): \"{dto.Subtype}\" is not a known oracle type.";

                if (string.IsNullOrWhiteSpace(dto.GameSystem))
                    return $"{label} (\"{dto.Name}\"): game system is missing.";

                if (!systemsByName.TryGetValue(dto.GameSystem, out var system))
                {
                    system = new GameSystem { Name = dto.GameSystem, Genre = "Unknown" };
                    systemsByName[dto.GameSystem] = system;
                }

                bool isDice = subtype.Category() == OracleCategory.Dice;
                int? diceCount = isDice ? dto.DiceCount : null;
                int? diceFaces = isDice ? dto.DiceFaces : null;

                var diceError = ValidateDice(label, dto.Name, subtype, diceCount, diceFaces);
                if (diceError != null)
                    return diceError;

                var spec = DiceSpec.For(subtype, diceCount, diceFaces);

                var error = BuildEntries(label, dto.Name, subtype, spec, dto.Entries ?? new List<OracleEntryDto>(), out var built);
                if (error != null)
                    return error;

                var oracle = new Oracle
                {
                    Name = dto.Name,
                    GameSystem = system,
                    OracleSubtype = subtype,
                    DiceCount = diceCount,
                    DiceFaces = diceFaces
                };

                foreach (var entry in built)
                {
                    oracle.OracleEntries.Add(entry);
                }
                imported.Add(oracle);
            }
            oracles = imported;
            return null;
        }

        private static string? ValidateDice(string label, string name, OracleSubtype subtype,
            int? diceCount, int? diceFaces)
        {
            if (subtype.Category() != OracleCategory.Dice)
                return null;

            if (diceCount == null || diceFaces == null)
                return $"{label} (\"{name}\"): a dice oracle needs both diceCount and diceFaces.";

            if (diceCount < 1 || diceCount > 10)
                return $"{label} (\"{name}\"): diceCount is {diceCount}; it must be between 1 and 10.";

            if (diceFaces < 2 || diceFaces > 100)
                return $"{label} (\"{name}\"): diceFaces is {diceFaces}; it must be between 2 and 100.";

            return null;
        }

        private static string? BuildEntries(string label, string name, OracleSubtype subtype,
            DiceSpec? spec, List<OracleEntryDto> entries, out List<OracleEntry> built)
        {
            built = new List<OracleEntry>();

            foreach (var e in entries)
            {
                if (e.Result != null && e.Result.Length > 500)
                    return $"{label} (\"{name}\"): an outcome is longer than 500 characters.";
            }

            if (subtype.Category() == OracleCategory.Dice)
            {
                if (spec != null && entries.Count > spec.Span)
                    return $"{label} (\"{name}\"): {entries.Count} outcomes on a {spec.Label}, which has only {spec.Span} totals.";

                for (int n = 0; n < entries.Count; n++)
                {
                    built.Add(new OracleEntry
                    {
                        SortOrder = n + 1,
                        Result = entries[n].Result ?? string.Empty,
                        RangeStart = entries[n].RangeStart,
                        RangeEnd = entries[n].RangeEnd
                    });
                }

                return null;
            }

            // Cards: every code must be in the deck, and labels are regenerated from
            // DeckFactory rather than trusted, so a hand-edited file cannot end up with
            // "H-02" labelled "King of Spades".
            var deck = DeckFactory.Build(subtype);
            var byCode = deck.ToDictionary(c => c.CardCode);
            var seen = new HashSet<string>();

            for (int n = 0; n < entries.Count; n++)
            {
                var code = entries[n].CardCode;

                if (code == null || !byCode.TryGetValue(code, out var card))
                    return $"{label} (\"{name}\"): \"{code}\" is not a card in the {subtype} deck.";

                if (!seen.Add(code))
                    return $"{label} (\"{name}\"): \"{code}\" appears more than once.";

                built.Add(new OracleEntry
                {
                    SortOrder = n + 1,
                    Result = entries[n].Result ?? string.Empty,
                    CardCode = card.CardCode,
                    CardLabel = card.CardLabel
                });
            }

            return null;
        }
    }
}
