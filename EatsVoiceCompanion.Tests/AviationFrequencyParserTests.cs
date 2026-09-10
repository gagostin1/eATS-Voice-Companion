using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class AviationFrequencyParserTests
{
    [Theory]
    [InlineData("one one eight point zero", 11800)]
    [InlineData("one two one point five", 12150)]
    [InlineData("one three two point three seven", 13237)]
    [InlineData("135.275", 13527)]
    [InlineData("132 37", 13237)]
    public void ParseHundredths_AcceptsFaaFrequencyWording(
        string spoken,
        int expected)
    {
        Assert.Equal(
            expected,
            AviationFrequencyParser.ParseHundredths(spoken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("one three two three seven")]
    [InlineData("117.95")]
    [InlineData("137.0")]
    [InlineData("132.33")]
    [InlineData("132.378")]
    public void ParseHundredths_RejectsInvalidOrAmbiguousFrequency(
        string spoken)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => AviationFrequencyParser.ParseHundredths(spoken));
    }
}
