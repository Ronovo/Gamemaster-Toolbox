using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;
using GM_Toolbox.Services;

namespace GM_Toolbox.Tests;

public class ImportTests
{
    private static OracleExportFile FileWith(params OracleDto?[] oracles) =>
        new() { FormatVersion = OracleExportFile.CurrentFormatVersion, Oracles = oracles.ToList()! };

    private static OracleDto D6(params OracleEntryDto?[] entries) => new()
    {
        Name = "Weather", Subtype = nameof(OracleSubtype.Dice), GameSystem = "Ironsworn",
        DiceCount = 1, DiceFaces = 6, Entries = entries.ToList()!
    };

    [Fact]
    public void An_existing_game_system_is_found_whatever_its_capitals()
    {
        var yours = new GameSystem { ID = 2, Name = "ironsworn", OwnerId = "you" };

        var lookup = OracleImporter.BuildSystemLookup([yours]);

        Assert.Same(yours, lookup["Ironsworn"]);
    }

    [Fact]
    public void Empty_entries_in_the_file_are_errors_not_crashes()
    {
        var systems = OracleImporter.BuildSystemLookup([]);

        Assert.Contains("is empty", OracleImporter.TryBuild(FileWith((OracleDto?)null), systems, out _));
        Assert.Contains("outcome is empty", OracleImporter.TryBuild(FileWith(D6((OracleEntryDto?)null)), systems, out _));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 7)]
    [InlineData(5, 2)]
    public void Ranges_must_fit_the_dice_and_run_forwards(int start, int end)
    {
        var file = FileWith(D6(new OracleEntryDto { Result = "Rain", RangeStart = start, RangeEnd = end }));

        var error = OracleImporter.TryBuild(file, OracleImporter.BuildSystemLookup([]), out var oracles);

        Assert.Contains("range", error);
        Assert.Empty(oracles);
    }

    [Fact]
    public void A_good_dice_oracle_imports()
    {
        var file = FileWith(D6(new OracleEntryDto { Result = "Rain", RangeStart = 1, RangeEnd = 3 },
                               new OracleEntryDto { Result = "Sun", RangeStart = 4, RangeEnd = 6 }));

        Assert.Null(OracleImporter.TryBuild(file, OracleImporter.BuildSystemLookup([]), out var oracles));
        Assert.Equal(2, oracles.Single().OracleEntries.Count);
    }

    [Fact]
    public void Only_the_current_format_imports()
    {
        var file = FileWith(D6(new OracleEntryDto { Result = "Rain", RangeStart = 1, RangeEnd = 6 }));
        file.FormatVersion = OracleExportFile.CurrentFormatVersion - 1;

        Assert.Contains("format version", OracleImporter.TryBuild(file, OracleImporter.BuildSystemLookup([]), out _));
    }

    [Fact]
    public void A_long_game_system_name_is_an_error()
    {
        var oracle = D6(new OracleEntryDto { Result = "Rain", RangeStart = 1, RangeEnd = 6 });
        oracle.GameSystem = new string('x', 101);

        Assert.Contains("game system name is longer", OracleImporter.TryBuild(FileWith(oracle), OracleImporter.BuildSystemLookup([]), out _));
    }

    [Fact]
    public void Oracle_names_and_systems_that_run_together_stay_apart()
    {
        var system = new GameSystem { Name = "bc" };
        var existing = new Oracle { Name = "a", GameSystem = system };
        var flow = new FlowDto
        {
            Name = "Travel", GameSystem = "bc",
            Steps = [new FlowStepDto { Id = 1, Kind = "Roll", Oracle = new OracleRefDto { Name = "ab", GameSystem = "c" } }]
        };

        var error = FlowImporter.TryBuild([flow], OracleImporter.BuildSystemLookup([system]), [], [existing], out _);

        Assert.Contains("neither in this file nor in your toolbox", error);
    }

    [Fact]
    public void A_step_without_an_id_is_an_error()
    {
        var flow = new FlowDto { Name = "Travel", GameSystem = "Ironsworn", Steps = [new FlowStepDto { Kind = "Text", Question = "Where?" }] };

        Assert.Contains("has no id", FlowImporter.TryBuild([flow], OracleImporter.BuildSystemLookup([]), [], [], out _));
    }

    private static OracleDto Cards(params OracleEntryDto[] entries) => new()
    {
        Name = "Fate", Subtype = nameof(OracleSubtype.PlayingCards), GameSystem = "Ironsworn", Entries = entries.ToList(),
    };

    [Fact]
    public void A_card_oracle_takes_its_card_names_from_the_deck_not_the_file()
    {
        var deck = DeckFactory.Build(OracleSubtype.PlayingCards);
        var file = FileWith(Cards(
            new OracleEntryDto { CardCode = deck[0].CardCode, CardLabel = "King of Spades", Result = "Love" },
            new OracleEntryDto { CardCode = deck[1].CardCode }));

        Assert.Null(OracleImporter.TryBuild(file, OracleImporter.BuildSystemLookup([]), out var oracles));
        var entries = oracles.Single().OracleEntries.ToList();
        Assert.Equal([deck[0].CardLabel, deck[1].CardLabel], entries.Select(e => e.CardLabel).ToList());
        Assert.Equal(["Love", ""], entries.Select(e => e.Result).ToList());
    }

    [Fact]
    public void A_card_that_is_not_in_the_deck_or_comes_twice_is_refused()
    {
        var code = DeckFactory.Build(OracleSubtype.PlayingCards)[0].CardCode;
        var systems = OracleImporter.BuildSystemLookup([]);

        Assert.Contains("\"Z-99\" is not a card in the PlayingCards deck.",
            OracleImporter.TryBuild(FileWith(Cards(new OracleEntryDto { CardCode = "Z-99" })), systems, out _));
        Assert.Contains("is not a card in the PlayingCards deck.",
            OracleImporter.TryBuild(FileWith(Cards(new OracleEntryDto())), systems, out _));
        Assert.Contains($"\"{code}\" appears more than once.", OracleImporter.TryBuild(
            FileWith(Cards(new OracleEntryDto { CardCode = code }, new OracleEntryDto { CardCode = code })), systems, out _));
    }

    [Fact]
    public void Each_oracle_field_is_checked()
    {
        var systems = OracleImporter.BuildSystemLookup([]);
        string? Error(Action<OracleDto> change)
        {
            var dto = D6(new OracleEntryDto { Result = "Rain", RangeStart = 1, RangeEnd = 6 });
            change(dto);
            return OracleImporter.TryBuild(FileWith(dto), systems, out _);
        }

        Assert.Contains("name is missing.", Error(d => d.Name = " "));
        Assert.Contains("name is longer than 100 characters.", Error(d => d.Name = new string('n', 101)));
        Assert.Contains("\"Runes\" is not a known oracle type.", Error(d => d.Subtype = "Runes"));
        Assert.Contains("\"7\" is not a known oracle type.", Error(d => d.Subtype = "7"));
        Assert.Contains("game system is missing.", Error(d => d.GameSystem = ""));
        Assert.Contains("a dice oracle needs both diceCount and diceFaces.", Error(d => d.DiceFaces = null));
        Assert.Contains("diceCount is 11; it must be between 1 and 10.", Error(d => d.DiceCount = 11));
        Assert.Contains("diceFaces is 1; it must be between 2 and 100.", Error(d => d.DiceFaces = 1));
        Assert.Contains("an outcome is longer than 500 characters.", Error(d => d.Entries[0].Result = new string('r', 501)));
    }

    [Fact]
    public void The_file_itself_and_its_game_systems_are_checked()
    {
        var systems = OracleImporter.BuildSystemLookup([]);
        Assert.Equal("That file could not be read as an oracle export.", OracleImporter.TryBuild(null, systems, out _));
        Assert.Contains("this app reads version", OracleImporter.TryBuild(
            new OracleExportFile { FormatVersion = OracleExportFile.CurrentFormatVersion + 1 }, systems, out _));
        Assert.Null(OracleImporter.TryBuild(new OracleExportFile { Oracles = null! }, systems, out var none));
        Assert.Empty(none);

        string? Error(GameSystemDto? system) => OracleImporter.TryBuild(
            new OracleExportFile { GameSystems = [system!] }, OracleImporter.BuildSystemLookup([]), out _);
        Assert.Contains("is empty.", Error(null));
        Assert.Contains("name is missing.", Error(new GameSystemDto { Name = "" }));
        Assert.Contains("name is longer than 100 characters.", Error(new GameSystemDto { Name = new string('s', 101) }));
        Assert.Contains("genre is longer than 100 characters.", Error(new GameSystemDto { Name = "Cairn", Genre = new string('g', 101) }));
        Assert.Null(OracleImporter.TryBuild(new OracleExportFile { GameSystems = [new() { Name = "Cairn" }, new() { Name = "cairn" }] },
            OracleImporter.BuildSystemLookup([]), out _));
    }

    private static readonly GameSystem Ironsworn = new() { ID = 1, Name = "Ironsworn" };
    private static readonly OracleRefDto WeatherRef = new() { Name = "Weather", GameSystem = "Ironsworn" };

    private static Oracle Weather() => new()
    {
        Name = "Weather", GameSystem = Ironsworn, OracleSubtype = OracleSubtype.Dice, DiceCount = 1, DiceFaces = 6,
        OracleEntries = [new OracleEntry { SortOrder = 1, Result = "Rain" }, new OracleEntry { SortOrder = 2, Result = "Sun" }],
    };

    private static string? FlowError(Action<FlowDto> change, List<Oracle>? existing = null, List<Oracle>? imported = null)
    {
        var flow = new FlowDto { Name = "Journey", GameSystem = "Ironsworn", Steps = [new() { Id = 1, Kind = "Roll", Oracle = WeatherRef }] };
        change(flow);
        return FlowImporter.TryBuild([flow], OracleImporter.BuildSystemLookup([Ironsworn]), imported ?? [], existing ?? [Weather()], out _);
    }

    [Fact]
    public void A_flow_with_every_kind_of_step_imports_and_links_its_steps()
    {
        var flow = new FlowDto
        {
            Name = "Journey", GameSystem = "Ironsworn", Steps =
            [
                new() { Id = 10, Kind = "Choice", Question = "Which way?", Options =
                    [new() { Label = "North", Oracle = WeatherRef, GoToStep = 30 }, new() { Label = "Home", EndsFlow = true }] },
                new() { Id = 20, Kind = "Text", Question = "Your name?" },
                new() { Id = 30, Kind = "RollChosen", PickedAtStep = 10 },
                new() { Id = 40, Oracle = WeatherRef, Branches = [new() { WhenOutcome = 1, GoToStep = 20 }, new() { WhenOutcome = 2, EndsFlow = true }] },
            ],
        };
        Assert.Null(FlowImporter.TryBuild([flow], OracleImporter.BuildSystemLookup([Ironsworn]), [], [Weather()], out var flows));
        var steps = flows.Single().Steps.OrderBy(s => s.SortOrder).ToList();
        Assert.Equal([FlowStepKind.Choice, FlowStepKind.Text, FlowStepKind.RollChosen, FlowStepKind.Roll], steps.Select(s => s.Kind).ToList());
        Assert.Same(steps[0], steps[2].SourceStep);
        Assert.Same(steps[2], steps[0].Options.First().TargetStep);
        Assert.Null(FlowImporter.TryBuild(null, OracleImporter.BuildSystemLookup([]), [], [], out var none));
        Assert.Empty(none);
    }

    [Fact]
    public void Each_flow_and_step_field_is_checked()
    {
        Assert.Contains("is empty.", FlowImporter.TryBuild([null!], OracleImporter.BuildSystemLookup([]), [], [], out _));
        Assert.Contains("name is missing.", FlowError(f => f.Name = " "));
        Assert.Contains("name is longer than 100 characters.", FlowError(f => f.Name = new string('n', 101)));
        Assert.Contains("description is longer than 500 characters.", FlowError(f => f.Description = new string('d', 501)));
        Assert.Contains("game system is missing.", FlowError(f => f.GameSystem = ""));
        Assert.Contains("step #1 is empty.", FlowError(f => f.Steps = [null!]));
        Assert.Contains("two steps share the id 1.", FlowError(f => f.Steps.Add(new() { Id = 1, Kind = "Text", Question = "Again?" })));
        Assert.Contains("\"Dance\" is not a known kind of step.", FlowError(f => f.Steps[0].Kind = "Dance"));
        Assert.Contains("step 1", FlowError(f => f.Steps[0] = new() { Id = 1, Kind = "Text" }));
    }

    [Fact]
    public void A_step_label_longer_than_the_editor_allows_is_refused()
    {
        Assert.Contains("step 1: A step label cannot be longer than 100 characters.",
            FlowError(f => f.Steps[0].Label = new string('l', 101)));
    }

    [Fact]
    public void A_roll_on_another_oracle_needs_an_earlier_pick_to_roll()
    {
        Assert.Contains("rolls what an earlier step picks, but does not say which one.", FlowError(f => f.Steps.Add(new() { Id = 2, Kind = "RollChosen" })));
        Assert.Contains("points at step 9, which is not in this flow.", FlowError(f => f.Steps.Add(new() { Id = 2, Kind = "RollChosen", PickedAtStep = 9 })));
        Assert.Contains("neither a dropdown question nor a roll.", FlowError(f =>
        {
            f.Steps.Add(new() { Id = 2, Kind = "Text", Question = "Why?" });
            f.Steps.Add(new() { Id = 3, Kind = "RollChosen", PickedAtStep = 2 });
        }));
        Assert.Contains("takes its oracle from a step that comes after it.", FlowError(f =>
            f.Steps.Insert(0, new() { Id = 2, Kind = "RollChosen", PickedAtStep = 1 })));
    }

    [Fact]
    public void Dropdown_options_and_branches_are_checked()
    {
        FlowStepDto Choice(List<FlowChoiceOptionDto>? options) => new() { Id = 2, Kind = "Choice", Question = "Which?", Options = options };
        Assert.Contains("is a dropdown question with no options.", FlowError(f => f.Steps.Add(Choice(null))));
        Assert.Contains("has an empty option.", FlowError(f => f.Steps.Add(Choice([null!]))));
        Assert.Contains("has an option with no label.", FlowError(f => f.Steps.Add(Choice([new() { Label = " " }]))));
        Assert.Contains("step 2", FlowError(f => f.Steps.Add(Choice([new() { Label = new string('o', 500) }]))));

        Assert.Contains("has an empty branch.", FlowError(f => f.Steps[0].Branches = [null!]));
        Assert.Contains("branches on outcome 5, which \"Weather\" does not have.", FlowError(f => f.Steps[0].Branches = [new() { WhenOutcome = 5 }]));
        Assert.Contains("has more than one branch on outcome 1.", FlowError(f => f.Steps[0].Branches = [new() { WhenOutcome = 1 }, new() { WhenOutcome = 1 }]));
    }

    [Fact]
    public void An_oracle_named_by_a_step_must_be_found_exactly_once()
    {
        Assert.Contains("does not name an oracle and its game system.", FlowError(f => f.Steps[0].Oracle = null));
        Assert.Contains("which is neither in this file nor in your toolbox.", FlowError(_ => { }, existing: []));
        Assert.Contains("this file carries more than one oracle by that name.", FlowError(_ => { }, imported: [Weather(), Weather()]));
        Assert.Contains("you already have more than one oracle by that name.", FlowError(_ => { }, existing: [Weather(), Weather()]));
        Assert.Null(FlowError(_ => { }, existing: [new() { Name = "Weather" }, Weather()]));
    }
}
