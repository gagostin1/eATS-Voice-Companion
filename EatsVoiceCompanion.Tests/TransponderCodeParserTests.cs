using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class TransponderCodeParserTests
{
    [Theory]
    [InlineData("4321", "4321")]
    [InlineData("zero four two one", "0421")]
    [InlineData("tree fife six seven", "3567")]
    [InlineData("0 4 2 1", "0421")]
    public void Parse_ReturnsFourOctalDigits(
        string input,
        string expected)
    {
        Assert.Equal(expected, TransponderCodeParser.Parse(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("12345")]
    [InlineData("1280")]
    [InlineData("one two eight zero")]
    [InlineData("four thousand three hundred")]
    public void Parse_RejectsInvalidCode(string input)
    {
        Assert.Throws<ArgumentException>(
            () => TransponderCodeParser.Parse(input));
    }
}
