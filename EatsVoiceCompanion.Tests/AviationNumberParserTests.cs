using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class AviationNumberParserTests
{
    [Theory]
    [InlineData("270", 270)]
    [InlineData("270.", 270)]
    [InlineData("two seven zero", 270)]
    [InlineData("TWO SEVEN ZERO", 270)]
    [InlineData("two-seven-zero", 270)]
    [InlineData("tree five zero", 350)]
    [InlineData("fife niner", 59)]
    [InlineData("oh nine zero", 90)]
    [InlineData("2 seven 0", 270)]
    public void Parse_ReturnsExpectedNumber(
        string input,
        int expected)
    {
        int result = AviationNumberParser.Parse(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("two seventy")]
    [InlineData("turn left")]
    public void Parse_RejectsUnsupportedInput(string input)
    {
        Assert.Throws<ArgumentException>(
            () => AviationNumberParser.Parse(input));
    }
}