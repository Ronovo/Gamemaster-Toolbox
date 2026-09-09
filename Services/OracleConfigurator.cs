using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace GM_Toolbox.Services
{
    public class OracleConfigurator
    {
        private readonly ApplicationDbContext _context;

        public OracleConfigurator(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Adds or removes entries so the oracle's entry set matches the request.
        /// Returns an error message if the request is invalid, otherwise null.
        /// The caller is responsible for calling SaveChanges.
        /// </summary>
        public string? Reconcile(Oracle oracle, int? outcomeCount)
        {
            var existing = oracle.OracleEntries
                .Where(e => _context.Entry(e).State != EntityState.Deleted)
                .OrderBy(e => e.SortOrder)
                .ToList();

            switch (oracle.OracleSubtype.Category())
            {
                case OracleCategory.Dice:
                    var spec = oracle.Dice();
                    if (spec == null)
                        return "Set this oracle's dice before choosing outcomes.";

                    if (outcomeCount == null || outcomeCount < 1 || outcomeCount > spec.Span)
                        return $"Enter a number between 1 and {spec.Span}.";

                    ReconcileDice(oracle, existing, spec, outcomeCount.Value);
                    break;

                case OracleCategory.Cards:
                    ReconcileCards(oracle, existing);
                    break;
            }

            return null;
        }

        private void ReconcileDice(Oracle oracle, List<OracleEntry> existing, DiceSpec spec, int desired)
        {
            // Trim extra rows off the end, keeping the survivors' text.
            for (int i = existing.Count - 1; i >= desired; i--)
                _context.OracleEntries.Remove(existing[i]);

            // Append new rows numbered 1, 2, 3… so a table of three is already 1, 2, 3.
            for (int n = existing.Count + 1; n <= desired; n++)
            {
                oracle.OracleEntries.Add(new OracleEntry
                {
                    OracleID = oracle.ID,
                    SortOrder = n,
                    Result = n.ToString()
                });
            }

            ApplyDefaultBands(oracle, spec);
        }
        
        private void ApplyDefaultBands(Oracle oracle, DiceSpec spec)
        {
            var entries = oracle.OracleEntries
                .Where(e => _context.Entry(e).State != EntityState.Deleted)
                .OrderBy(e => e.SortOrder)
                .ToList();

            int count = entries.Count;
            int span = spec.Span;
            int baseWidth = count == 0 ? 0 : span / count;
            int extra = count == 0 ? 0 : span % count;
            int nextStart = spec.Min;

            for (int i = 0; i < count; i++)
            {
                // Later bands take the leftover pips so the last one always ends at Max.
                int width = baseWidth + (i >= count - extra ? 1 : 0);
                int end = nextStart + width - 1;
                entries[i].SortOrder = i + 1;
                entries[i].RangeStart = nextStart;
                entries[i].RangeEnd = end;
                nextStart = end + 1;
            }
        }

        private void ReconcileCards(Oracle oracle, List<OracleEntry> existing)
        {
            var deck = DeckFactory.Build(oracle.OracleSubtype);
            var deckCodes = deck.Select(c => c.CardCode).ToHashSet();
            var existingByCode = existing
                .Where(e => e.CardCode != null)
                .ToDictionary(e => e.CardCode!);

            // Drop entries whose card is no longer in the deck.
            foreach (var entry in existing)
                if (entry.CardCode == null || !deckCodes.Contains(entry.CardCode))
                    _context.OracleEntries.Remove(entry);

            // Add the net entries to the deck
            for (int n = 0; n < deck.Count; n++)
            {
                if (existingByCode.TryGetValue(deck[n].CardCode, out var entry))
                {
                    entry.SortOrder = n + 1;
                    entry.CardLabel = deck[n].CardLabel;
                }
                else
                {
                    _context.OracleEntries.Add(new OracleEntry
                    {
                        OracleID = oracle.ID,
                        SortOrder = n + 1,
                        CardCode = deck[n].CardCode,
                        CardLabel = deck[n].CardLabel,
                        Result = deck[n].CardLabel
                    });
                }
            }
        }
    }
}