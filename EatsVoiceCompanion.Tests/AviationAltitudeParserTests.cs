using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class AviationAltitudeParserTests
{
    [Theory]
    [InlineData("10000", 10000)]
    [InlineData("one zero thousand", 10000)]
    [InlineData("one seven thousand", 17000)]
    [InlineData("five thousand", 5000)]
    [InlineData("four thousand five hundred", 4500)]
    [InlineData("one zero thousand five hundred", 10500)]
    [InlineData("one 3,000", 13000)]
    [InlineData("one 3000", 13000)]
    [InlineData("1 three 000", 13000)]
    public void Parse_ReturnsExpectedAltitude(
        string input,
        int expected)
    {
        int result =
            AviationAltitudeParser.Parse(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("seventeen thousand")]
    [InlineData("one zero thousand fifty")]
    [InlineData("ten grand")]
    [InlineData("one 30")]
    public void Parse_RejectsUnsupportedPhraseology(
        string input)
    {
        Assert.Throws<ArgumentException>(
            () => AviationAltitudeParser.Parse(input));
    }
}
