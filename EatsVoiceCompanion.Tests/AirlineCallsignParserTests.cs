using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class AirlineCallsignParserTests
{
    private static readonly IReadOnlyDictionary<string, string>
        AirlineAliases =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Delta"] = "DAL",
                ["United"] = "UAL",
                ["American"] = "AAL",
                ["Air Canada"] = "ACA"
            };

    private readonly AirlineCallsignParser _parser =
        new(AirlineAliases);

    [Theory]
    [InlineData("Delta 123", "DAL123")]
    [InlineData("delta one two three", "DAL123")]
    [InlineData("UNITED 714.", "UAL714")]
    [InlineData("American 45", "AAL45")]
    [InlineData("Air Canada 101", "ACA101")]
    [InlineData("DAL123", "DAL123")]
    [InlineData("ual 714", "UAL714")]
    [InlineData("delta tree fife zero", "DAL350")]
    [InlineData("Delta two seventy", "DAL270")]
    [InlineData("United seven fourteen", "UAL714")]
    [InlineData("American five twenty one", "AAL521")]
    [InlineData("Delta zero one zero", "DAL010")]
    public void Parse_ReturnsExpectedCallsign(
        string input,
        string expected)
    {
        string result = _parser.Parse(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Southwest 123")]
    [InlineData("Delta")]
    public void Parse_RejectsUnknownOrIncompleteCallsign(
        string input)
    {
        Assert.Throws<ArgumentException>(
            () => _parser.Parse(input));
    }
}