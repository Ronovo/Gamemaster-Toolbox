using GM_Toolbox.Models.Enums;

namespace GM_Toolbox.Services
{
    public class CardDefinition
    {
        public string CardCode { get; set; } = string.Empty;
        public string CardLabel { get; set; } = string.Empty;
    }

    // Suit letter plus number / face / major, used by the Edit Entries jump links.
    public class CardClassification
    {
        public string? Suit { get; init; }
        public string RankKind { get; init; } = string.Empty;
    }

    public static class DeckFactory
    {
        public static List<CardDefinition> Build(OracleSubtype subtype)
        {
            switch (subtype)
            {
                case OracleSubtype.PlayingCards:
                    return BuildPlayingCards();
                case OracleSubtype.TarotCards:
                    return BuildTarotCards();
                default:
                    throw new InvalidOperationException($"{subtype} is not a card subtype.");
            }
        }

        // Playing: H-A, H-J. Tarot major: T-MAJ-00. Tarot minor: T-W-KG.
        // Face is Ace/J/Q/K (playing) or Ace/Page/Knight/Queen/King (tarot).
        public static CardClassification? Classify(string? cardCode)
        {
            if (string.IsNullOrEmpty(cardCode)) return null;

            var parts = cardCode.Split('-');

            if (parts.Length == 3 && parts[0] == "T" && parts[1] == "MAJ")
                return new CardClassification { RankKind = "major" };

            if (parts.Length == 3 && parts[0] == "T")
                return new CardClassification { Suit = parts[1], RankKind = RankKindOf(parts[2], tarot: true) };

            if (parts.Length == 2)
                return new CardClassification { Suit = parts[0], RankKind = RankKindOf(parts[1], tarot: false) };

            return null;
        }

        private static string RankKindOf(string rank, bool tarot)
        {
            if (tarot)
                return rank is "A" or "PG" or "KN" or "QN" or "KG" ? "face" : "number";

            return rank is "A" or "J" or "Q" or "K" ? "face" : "number";
        }

        private static List<CardDefinition> BuildPlayingCards()
        {
            var suits = new[]
            {
                (Code: "H", Name: "Hearts"),
                (Code: "D", Name: "Diamonds"),
                (Code: "C", Name: "Clubs"),
                (Code: "S", Name: "Spades"),
            };

            var ranks = new[]
            {
                (Code: "02", Name: "Two"),
                (Code: "03", Name: "Three"),
                (Code: "04", Name: "Four"),
                (Code: "05", Name: "Five"),
                (Code: "06", Name: "Six"),
                (Code: "07", Name: "Seven"),
                (Code: "08", Name: "Eight"),
                (Code: "09", Name: "Nine"),
                (Code: "10", Name: "Ten"),
                (Code: "J", Name: "Jack"),
                (Code: "Q", Name: "Queen"),
                (Code: "K", Name: "King"),
                (Code: "A", Name: "Ace"),
            };

            return BuildSuitedCards(suits, ranks, "");
        }

        private static List<CardDefinition> BuildTarotCards() 
        {
            var major = new[]
            {
                (Code: "00", Name: "The Fool"),
                (Code: "01", Name: "The Magician"),
                (Code: "02", Name: "The High Priestess"),
                (Code: "03", Name: "The Empress"),
                (Code: "04", Name: "The Emperor"),
                (Code: "05", Name: "The Hierophant"),
                (Code: "06", Name: "The Lovers"),
                (Code: "07", Name: "The Chariot"),
                (Code: "08", Name: "Strength"),
                (Code: "09", Name: "The Hermit"),
                (Code: "10", Name: "Wheel of Fortune"),
                (Code: "11", Name: "Justice"),
                (Code: "12", Name: "The Hanged Man"),
                (Code: "13", Name: "Death"),
                (Code: "14", Name: "Temperance"),
                (Code: "15", Name: "The Devil"),
                (Code: "16", Name: "The Tower"),
                (Code: "17", Name: "The Star"),
                (Code: "18", Name: "The Moon"),
                (Code: "19", Name: "The Sun"),
                (Code: "20", Name: "Judgement"),
                (Code: "21", Name: "The World"),
            };

            var suits = new[]
            {
                (Code: "W", Name: "Wands"),
                (Code: "C", Name: "Cups"),
                (Code: "S", Name: "Swords"),
                (Code: "P", Name: "Pentacles"),
            };

            var ranks = new[]
            {
                (Code: "A",  Name: "Ace"),
                (Code: "02", Name: "Two"),
                (Code: "03", Name: "Three"),
                (Code: "04", Name: "Four"),
                (Code: "05", Name: "Five"),
                (Code: "06", Name: "Six"),
                (Code: "07", Name: "Seven"),
                (Code: "08", Name: "Eight"),
                (Code: "09", Name: "Nine"),
                (Code: "10", Name: "Ten"),
                (Code: "PG", Name: "Page"),
                (Code: "KN", Name: "Knight"),
                (Code: "QN", Name: "Queen"),
                (Code: "KG", Name: "King"),
            };

            var deck = new List<CardDefinition>();

            foreach (var card in major)
            {
                deck.Add(new CardDefinition
                {
                    CardCode = $"T-MAJ-{card.Code}",
                    CardLabel = card.Name
                });
            }

            deck.AddRange(BuildSuitedCards(suits, ranks, "T-"));
            return deck;
        }

        private static List<CardDefinition> BuildSuitedCards((string Code, string Name)[] suits, 
            (string Code, string Name)[] ranks, string codePrefix)
        {
            var cards = new List<CardDefinition>();
            foreach (var suit in suits)
            {
                foreach (var rank in ranks)
                {
                    cards.Add(new CardDefinition
                    {
                        CardCode = $"{codePrefix}{suit.Code}-{rank.Code}",
                        CardLabel = $"{rank.Name} of {suit.Name}"
                    });
                }
            }

            return cards;
        }
    }
}
