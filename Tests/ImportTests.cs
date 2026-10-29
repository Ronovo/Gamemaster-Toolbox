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

    [Theory]
    [InlineData("d99999999999")]
    [InlineData("99999999999d6")]
    [InlineData("d1")]
    [InlineData("nonsense")]
    public void Bad_dice_labels_parse_as_nothing(string label)
    {
        Assert.Null(DiceSpec.Parse(label));
    }

    [Fact]
    public void Good_dice_labels_parse()
    {
        Assert.Equal(12, DiceSpec.Parse("2d6")!.Max);
        Assert.Equal(20, DiceSpec.Parse("d20")!.Max);
    }
}
