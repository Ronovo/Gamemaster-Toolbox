using System.Diagnostics.CodeAnalysis;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Http;

namespace GM_Toolbox.Tests;

/// <summary>An ISession kept in memory, like the cookie-backed one a browser gets.</summary>
public sealed class MemorySession : ISession
{
    private readonly Dictionary<string, byte[]> _values = new();

    public bool IsAvailable => true;
    public string Id => "test";
    public IEnumerable<string> Keys => _values.Keys;
    public void Clear() => _values.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _values.Remove(key);
    public void Set(string key, byte[] value) => _values[key] = value;
    public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
}

public class OracleRollerTests
{
    private static Oracle DiceOracle(int count, int faces, params (int start, int end, string result)[] bands)
    {
        var oracle = new Oracle { ID = 1, Name = "Weather", OracleSubtype = OracleSubtype.Dice, DiceCount = count, DiceFaces = faces };
        var id = 1;
        foreach (var (start, end, result) in bands)
        {
            oracle.OracleEntries.Add(new OracleEntry { ID = id, SortOrder = id, RangeStart = start, RangeEnd = end, Result = result });
            id++;
        }
        return oracle;
    }

    private static Oracle CardOracle(int id, int cards)
    {
        var oracle = new Oracle { ID = id, Name = "Fate", OracleSubtype = OracleSubtype.PlayingCards };
        for (var i = 1; i <= cards; i++)
        {
            oracle.OracleEntries.Add(new OracleEntry { ID = i, SortOrder = i, CardCode = $"H-0{i}", CardLabel = $"Card {i}", Result = $"Result {i}" });
        }
        return oracle;
    }

    [Fact]
    public void A_dice_roll_lands_in_the_band_that_covers_it()
    {
        var oracle = DiceOracle(2, 6, (2, 6, "Clear"), (7, 12, "Storm"));

        for (var i = 0; i < 50; i++)
        {
            Assert.Null(OracleRoller.TryRoll(oracle, new MemorySession(), out var outcome));
            Assert.NotNull(outcome);
            Assert.True(outcome.IsDice);
            Assert.InRange(outcome.Value!.Value, 2, 12);
            Assert.Equal(outcome.Value <= 6 ? "Clear" : "Storm", outcome.Result);
            Assert.Equal(outcome.Value <= 6 ? "2-6" : "7-12", outcome.Key);
            Assert.Matches(@"^2d6 \(\d\+\d\) · band (2-6|7-12)$", outcome.Caption);
        }
    }

    [Fact]
    public void A_single_die_caption_has_no_breakdown()
    {
        var oracle = DiceOracle(1, 4, (1, 4, "Anything"));

        OracleRoller.TryRoll(oracle, new MemorySession(), out var outcome);

        Assert.Equal("d4 · band 1-4", outcome!.Caption);
    }

    [Fact]
    public void A_roll_no_band_covers_says_so()
    {
        var oracle = DiceOracle(1, 4, (5, 8, "Never"));

        var error = OracleRoller.TryRoll(oracle, new MemorySession(), out var outcome);

        Assert.Null(outcome);
        Assert.Matches(@"^Rolled [1-4], but no outcome covers it\. Use Configure to check this oracle's ranges\.$", error);
    }

    [Fact]
    public void Rolling_bare_dice_reports_each_die()
    {
        var total = OracleRoller.RollDice(new DiceSpec(3, 6), out var rolled);

        Assert.Equal(3, rolled.Length);
        Assert.All(rolled, die => Assert.InRange(die, 1, 6));
        Assert.Equal(rolled.Sum(), total);
        Assert.Equal("3d6 (1+2+3)", OracleRoller.DiceLabel(new DiceSpec(3, 6), new[] { 1, 2, 3 }));
    }

    [Fact]
    public void A_deck_deals_every_card_once_then_reshuffles()
    {
        var oracle = CardOracle(7, 3);
        var session = new MemorySession();
        Assert.Equal(3, OracleRoller.CardsRemaining(oracle, session));

        var dealt = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            Assert.Null(OracleRoller.TryRoll(oracle, session, out var card));
            Assert.False(card!.IsDice);
            Assert.False(card.Reshuffled);
            Assert.Equal(2 - i, card.CardsRemaining);
            Assert.Equal($"Card {card.EntryID}", card.Key);
            dealt.Add(card.EntryID!.Value);
        }
        Assert.Equal(new[] { 1, 2, 3 }, dealt.Order());
        Assert.Equal(0, OracleRoller.CardsRemaining(oracle, session));

        OracleRoller.TryRoll(oracle, session, out var next);
        Assert.True(next!.Reshuffled);
        Assert.Equal(2, next.CardsRemaining);
    }

    [Fact]
    public void Reshuffling_starts_a_full_deck()
    {
        var oracle = CardOracle(7, 3);
        var session = new MemorySession();
        OracleRoller.TryRoll(oracle, session, out _);

        OracleRoller.Reshuffle(oracle, session);

        Assert.Equal(3, OracleRoller.CardsRemaining(oracle, session));
    }

    [Fact]
    public void Each_card_oracle_keeps_its_own_deck()
    {
        var first = CardOracle(1, 3);
        var second = CardOracle(2, 3);
        var session = new MemorySession();

        OracleRoller.TryRoll(first, session, out _);

        Assert.Equal(2, OracleRoller.CardsRemaining(first, session));
        Assert.Equal(3, OracleRoller.CardsRemaining(second, session));
    }

    [Fact]
    public void Cards_deleted_since_the_shuffle_leave_the_deck()
    {
        var oracle = CardOracle(7, 3);
        var session = new MemorySession();
        OracleRoller.TryRoll(oracle, session, out var first);
        var gone = oracle.OracleEntries.First(e => e.ID != first!.EntryID);
        oracle.OracleEntries.Remove(gone);

        Assert.Equal(1, OracleRoller.CardsRemaining(oracle, session));
        OracleRoller.TryRoll(oracle, session, out var second);
        Assert.NotEqual(gone.ID, second!.EntryID);
        Assert.False(second.Reshuffled);
    }

    [Fact]
    public void A_deck_whose_cards_were_all_deleted_starts_again_without_calling_it_a_reshuffle()
    {
        var oracle = CardOracle(7, 2);
        var session = new MemorySession();
        OracleRoller.TryRoll(oracle, session, out _);
        oracle.OracleEntries.Clear();
        oracle.OracleEntries.Add(new OracleEntry { ID = 9, CardLabel = "New card", Result = "New" });

        OracleRoller.TryRoll(oracle, session, out var card);

        Assert.Equal(9, card!.EntryID);
        Assert.False(card.Reshuffled);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    public void A_deck_in_the_session_that_cannot_be_read_starts_again(string stored)
    {
        var oracle = CardOracle(7, 2);
        var session = new MemorySession();
        session.SetString("oracle-deck-7", stored);

        Assert.Equal(2, OracleRoller.CardsRemaining(oracle, session));
        Assert.Null(OracleRoller.TryRoll(oracle, session, out _));
    }

    [Fact]
    public void A_card_oracle_with_no_cards_says_so()
    {
        var error = OracleRoller.TryRoll(CardOracle(7, 0), new MemorySession(), out var outcome);

        Assert.Null(outcome);
        Assert.Equal("This oracle has no cards to draw from.", error);
    }
}

public class DeckFactoryTests
{
    [Fact]
    public void A_playing_deck_has_52_named_cards()
    {
        var deck = DeckFactory.Build(OracleSubtype.PlayingCards);

        Assert.Equal(52, deck.Count);
        Assert.Equal(52, deck.Select(card => card.CardCode).Distinct().Count());
        Assert.Contains(deck, card => card is { CardCode: "H-A", CardLabel: "Ace of Hearts" });
        Assert.Contains(deck, card => card is { CardCode: "S-02", CardLabel: "Two of Spades" });
    }

    [Fact]
    public void A_tarot_deck_has_22_major_and_56_minor_arcana()
    {
        var deck = DeckFactory.Build(OracleSubtype.TarotCards);

        Assert.Equal(78, deck.Count);
        Assert.Equal(22, deck.Count(card => card.CardCode.StartsWith("T-MAJ-")));
        Assert.Equal(("T-MAJ-00", "The Fool"), (deck[0].CardCode, deck[0].CardLabel));
        Assert.Contains(deck, card => card is { CardCode: "T-P-KG", CardLabel: "King of Pentacles" });
    }

    [Fact]
    public void Dice_are_not_a_deck()
    {
        Assert.Throws<InvalidOperationException>(() => DeckFactory.Build(OracleSubtype.Dice));
    }

    [Theory]
    [InlineData("T-MAJ-13", null, "major")]
    [InlineData("T-W-KN", "W", "face")]
    [InlineData("T-C-07", "C", "number")]
    [InlineData("H-K", "H", "face")]
    [InlineData("S-09", "S", "number")]
    public void Every_card_code_classifies_by_suit_and_rank(string code, string? suit, string rankKind)
    {
        var classified = DeckFactory.Classify(code)!;

        Assert.Equal(suit, classified.Suit);
        Assert.Equal(rankKind, classified.RankKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("A-B-C-D")]
    public void Anything_else_does_not_classify(string? code)
    {
        Assert.Null(DeckFactory.Classify(code));
    }

    [Fact]
    public void Every_card_in_both_decks_classifies()
    {
        var cards = DeckFactory.Build(OracleSubtype.PlayingCards).Concat(DeckFactory.Build(OracleSubtype.TarotCards));

        Assert.All(cards, card => Assert.NotNull(DeckFactory.Classify(card.CardCode)));
    }
}
