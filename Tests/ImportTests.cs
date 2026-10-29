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
}
