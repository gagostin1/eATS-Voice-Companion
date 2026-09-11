using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class NNumberCallsignParserTests
{
    [Theory]
    [InlineData("N253PZ", "N253PZ")]
    [InlineData("n-6839-r", "N6839R")]
    [InlineData("November two five three papa zulu", "N253PZ")]
    [InlineData("November six eight three niner Romeo", "N6839R")]
    [InlineData("N one two three alpha bravo", "N123AB")]
    [InlineData("November one tree fife golf", "N135G")]
    [InlineData("November one two three x-ray", "N123X")]
    public void Parse_ReturnsCanonicalRegistration(
        string input,
        string expected)
    {
        Assert.Equal(expected, NNumberCallsignParser.Parse(input));
    }

    [Theory]
    [InlineData("N0123")]
    [InlineData("N123456")]
    [InlineData("N12I")]
    [InlineData("N12O")]
    [InlineData("N1ABC")]
    [InlineData("November twelve alpha")]
    [InlineData("one two three alpha")]
    public void Parse_RejectsInvalidOrUnsupportedRegistration(
        string input)
    {
        Assert.Throws<ArgumentException>(
            () => NNumberCallsignParser.Parse(input));
    }

    [Theory]
    [InlineData("N12345", "November One Two Three Four Five")]
    [InlineData("N253PZ", "November Two Five Three Papa Zulu")]
    [InlineData("N6839R", "November Six Eight Three Niner Romeo")]
    [InlineData("N123X", "November One Two Three X-ray")]
    public void ToPromptPhrase_UsesAviationWords(
        string callsign,
        string expected)
    {
        Assert.Equal(
            expected,
            NNumberCallsignParser.ToPromptPhrase(callsign));
    }
}
