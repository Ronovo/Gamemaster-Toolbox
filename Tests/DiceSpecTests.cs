using GM_Toolbox.Models;

namespace GM_Toolbox.Tests;

public class DiceSpecTests
{
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
