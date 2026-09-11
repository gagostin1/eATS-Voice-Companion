using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class AviationDistanceParserTests
{
    [Theory]
    [InlineData("10", 10)]
    [InlineData("one zero", 10)]
    [InlineData("ten", 10)]
    [InlineData("twenty five", 25)]
    [InlineData("one hundred", 100)]
    [InlineData("two hundred fifty", 250)]
    [InlineData("nine hundred ninety nine", 999)]
    public void Parse_AcceptsMileagePhraseology(string input, int expected)
    {
        Assert.Equal(expected, AviationDistanceParser.Parse(input));
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("1000")]
    [InlineData("one thousand")]
    [InlineData("twenty ten")]
    public void Parse_RejectsInvalidDistance(string input)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => AviationDistanceParser.Parse(input));
    }
}
