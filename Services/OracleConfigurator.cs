using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;

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
            var existing = oracle.OracleEntries.OrderBy(e => e.SortOrder).ToList();

            switch (oracle.OracleSubtype.Category())
            {
                case OracleCategory.Dice:
                    int faceCount = oracle.OracleSubtype.FaceCount();
                    if (outcomeCount == null || outcomeCount < 1 || outcomeCount > faceCount)
                        return $"Enter a number between 1 and {faceCount}.";

                    ReconcileDice(oracle, existing, outcomeCount.Value);
                    break;

                case OracleCategory.Cards:
                    ReconcileCards(oracle, existing);
                    break;
            }

            return null;
        }

        private void ReconcileDice(Oracle oracle, List<OracleEntry> existing, int desired)
        {
            // Trim extra rows off the end, keeping the survivors' text.
            for (int i = existing.Count - 1; i >= desired; i--)
                _context.OracleEntries.Remove(existing[i]);

            // Append new blank rows
            for (int n = existing.Count + 1; n <= desired; n++)
                _context.OracleEntries.Add(new OracleEntry
                {
                    OracleID = oracle.ID,
                    SortOrder = n,
                    Result = string.Empty
                });
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
                        Result = string.Empty
                    });
                }
            }
        }
    }
}