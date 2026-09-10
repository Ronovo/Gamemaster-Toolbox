using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;
using System.Text.Json;

namespace GM_Toolbox.Services
{
    /// <summary>
    /// Methods to work with the Oracle Roller, such as
    /// - Deck Maintenance 
    /// - Rolling Dice
    /// - Pulling cards from a deck
    /// </summary>
    public static class OracleRoller
    {
        public static string? TryRoll(Oracle oracle, ISession session, out RollOutcome? outcome)
        {
            outcome = null;

            return oracle.OracleSubtype.Category() == OracleCategory.Cards
                ? TryDraw(oracle, session, out outcome)
                : TryRollDice(oracle, out outcome);
        }

        public static int CardsRemaining(Oracle oracle, ISession session)
        {
            var deck = LoadDeck(oracle, session);
            return deck?.Count ?? oracle.OracleEntries.Count;
        }

        public static void Reshuffle(Oracle oracle, ISession session)
        {
            session.Remove(DeckKey(oracle.ID));
        }

        /// <summary>
        /// Rolls a bare spec and returns the total, with the individual dice in `rolled`.
        ///
        /// Split out of TryRollDice so anything needing a number rather than an oracle
        /// outcome -- a loop asking how many times to run -- goes through the same RNG.
        /// </summary>
        public static int RollDice(DiceSpec spec, out int[] rolled)
        {
            rolled = new int[spec.Count];
            int value = 0;

            for (int i = 0; i < spec.Count; i++)
            {
                rolled[i] = Random.Shared.Next(1, spec.Faces + 1);
                value += rolled[i];
            }

            return value;
        }

        /// <summary>
        /// How a roll reads in a caption: "2d6 (3+4)" for several dice, just "d20" for one.
        /// </summary>
        public static string DiceLabel(DiceSpec spec, int[] rolled)
        {
            return spec.Count > 1
                ? $"{spec.Label} ({string.Join("+", rolled)})"
                : spec.Label;
        }

        private static string? TryRollDice(Oracle oracle, out RollOutcome? outcome)
        {
            outcome = null;

            var spec = oracle.Dice()!;

            var value = RollDice(spec, out var rolled);

            var entry = oracle.OracleEntries.FirstOrDefault(
                e => e.RangeStart <= value && value <= e.RangeEnd);

            if (entry == null)
            {
                return $"Rolled {value}, but no outcome covers it. Use Configure to check this oracle's ranges.";
            }

            var dice = DiceLabel(spec, rolled);

            outcome = new RollOutcome
            {
                IsDice = true,
                EntryID = entry.ID,
                Value = value,
                Key = entry.KeyLabel(),
                Caption = $"{dice} · band {entry.KeyLabel()}",
                Result = entry.Result
            };

            return null;
        }

        private static string? TryDraw(Oracle oracle, ISession session, out RollOutcome? outcome)
        {
            outcome = null;

            if (oracle.OracleEntries.Count == 0)
            {
                return "This oracle has no cards to draw from.";
            }

            var deck = LoadDeck(oracle, session);

            bool reshuffled = deck != null && deck.Count == 0;
            if (deck == null || deck.Count == 0)
            {
                deck = BuildShuffledDeck(oracle);
            }

            int entryId = deck[0];
            deck.RemoveAt(0);

            var entry = oracle.OracleEntries.FirstOrDefault(e => e.ID == entryId);
            if (entry == null)
            {
                Reshuffle(oracle, session);
                return "That card is no longer part of this oracle. The deck has been reshuffled, so try again.";
            }

            SaveDeck(oracle, session, deck);

            outcome = new RollOutcome
            {
                IsDice = false,
                EntryID = entry.ID,
                Key = entry.KeyLabel(),
                Result = entry.Result,
                CardsRemaining = deck.Count,
                Reshuffled = reshuffled
            };

            return null;
        }

        private static string DeckKey(int oracleId) => $"oracle-deck-{oracleId}";

        private static List<int> BuildShuffledDeck(Oracle oracle)
        {
            var ids = oracle.OracleEntries.OrderBy(e => e.SortOrder).Select(e => e.ID).ToArray();
            Random.Shared.Shuffle(ids);
            return ids.ToList();
        }

        private static List<int>? LoadDeck(Oracle oracle, ISession session)
        {
            var json = session.GetString(DeckKey(oracle.ID));
            if (json == null)
            {
                return null;
            }

            List<int>? stored;
            try
            {
                stored = JsonSerializer.Deserialize<List<int>>(json);
            }
            catch (JsonException)
            {
                return null;
            }

            if (stored == null)
            {
                return null;
            }

            var live = oracle.OracleEntries.Select(e => e.ID).ToHashSet();
            var filtered = stored.Where(live.Contains).ToList();

            if (filtered.Count == 0 && stored.Count > 0)
            {
                return null;
            }

            return filtered;
        }

        private static void SaveDeck(Oracle oracle, ISession session, List<int> deck)
        {
            session.SetString(DeckKey(oracle.ID), JsonSerializer.Serialize(deck));
        }
    }
}
