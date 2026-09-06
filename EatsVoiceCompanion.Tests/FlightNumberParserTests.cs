using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class FlightNumberParserTests
{
    [Theory]
    [InlineData("714", "714")]
    [InlineData("seven one four", "714")]
    [InlineData("seven fourteen", "714")]
    [InlineData("five twenty one", "521")]
    [InlineData("thirty fifteen", "3015")]
    [InlineData("twenty five", "25")]
    [InlineData("one hundred", "100")]
    [InlineData("one zero one", "101")]
    [InlineData("ten zero four", "1004")]
    [InlineData("zero one zero", "010")]
    [InlineData("tree fife zero", "350")]
    public void Parse_ReturnsExpectedDigits(
        string input,
        string expected)
    {
        string result =
            FlightNumberParser.Parse(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("seven to fourteen")]
    [InlineData("one hundred five")]
    [InlineData("one two three four five")]
    public void Parse_RejectsUnsupportedInput(string input)
    {
        Assert.Throws<ArgumentException>(
            () => FlightNumberParser.Parse(input));
    }
}