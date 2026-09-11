using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class CrossDirectionNormalizerTests
{
    [Theory]
    [InlineData("north", "N")]
    [InlineData("north east", "NE")]
    [InlineData("NE", "NE")]
    [InlineData("southeast", "SE")]
    [InlineData("south west", "SW")]
    [InlineData("northwest", "NW")]
    public void Normalize_ReturnsEatsDirection(
        string input,
        string expected)
    {
        Assert.Equal(expected, CrossDirectionNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("upwind")]
    [InlineData("north by northwest")]
    public void Normalize_RejectsUnsupportedDirection(string input)
    {
        Assert.Throws<ArgumentException>(
            () => CrossDirectionNormalizer.Normalize(input));
    }
}
