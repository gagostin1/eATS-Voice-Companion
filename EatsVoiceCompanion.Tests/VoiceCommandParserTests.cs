using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class VoiceCommandParserTests
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

    private readonly VoiceCommandParser _parser =
        new(AirlineAliases);

    [Theory]
    [InlineData(
        "Delta 123 turn left heading 270.",
        "DAL123 TLH270")]
    [InlineData(
        "United 714 climb and maintain flight level 230.",
        "UAL714 CM230")]
    [InlineData(
        "DAL45 fly heading 090",
        "DAL45 FH090")]
    [InlineData(
        "American one two descend and maintain 10000",
        "AAL12 DM100")]
    [InlineData(
        "Delta 9 maintain speed 250",
        "DAL9 S250")]
    [InlineData(
        "Delta 123 proceed direct to LOZIT",
        "DAL123 ..LOZIT")]
    [InlineData(
        "United 714 turn right heading three six zero",
        "UAL714 TRH360")]
    [InlineData(
        "American four five descend and maintain one zero thousand",
        "AAL45 DM100")]
    [InlineData(
        "American four five descend and maintain one seven thousand",
        "AAL45 DM170")]
    [InlineData(
        "American four five descend and maintain one zero thousand five hundred",
        "AAL45 DM105")]
    public void Parse_BuildsExpectedEatsCommand(
        string transcript,
        string expected)
    {
        ParsedVoiceCommand parsed =
            _parser.Parse(transcript);

        string result = parsed.ToEatsCommand();

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(
        "American thirteen oh seven, Atlanta Center, climb and maintain flight level two three zero",
        "AAL1307 CM230")]
    [InlineData(
        "American thirteen oh seven, Atlanta Center, welcome",
        "AAL1307 R")]
    [InlineData(
        "American thirteen oh seven, Atlanta Center, roger",
        "AAL1307 R")]
    public void Parse_AcceptsControllerIdentification(
        string transcript,
        string expected)
    {
        ParsedVoiceCommand parsed =
            _parser.Parse(
                transcript,
                controllerPosition: "Atlanta Center");

        Assert.Equal(
            expected,
            parsed.ToEatsCommand());
    }

    [Fact]
    public void Parse_DoesNotIgnoreUnconfiguredPosition()
    {
        Assert.Throws<ArgumentException>(
            () => _parser.Parse(
                "American thirteen oh seven, " +
                "Atlanta Center, " +
                "climb and maintain flight level two three zero",
                controllerPosition: "Jacksonville Center"));
    }

    [Theory]
    [InlineData("Delta 123 contact tower")]
    [InlineData("Delta 123 maintain present heading")]
    public void Parse_RejectsUnsupportedInstructions(
        string transcript)
    {
        Assert.Throws<InvalidOperationException>(
            () => _parser.Parse(transcript));
    }

    [Fact]
    public void Parse_RejectsUnsupportedNumberWording()
    {
        Assert.Throws<ArgumentException>(
            () => _parser.Parse(
                "Delta 123 turn left heading two seventy"));
    }

    [Fact]
    public void Parse_RejectsUnknownAirline()
    {
        Assert.Throws<ArgumentException>(
            () => _parser.Parse(
                "Southwest 123 turn left heading 270"));
    }

    [Theory]
    [InlineData(
        "Delta 123 turn left heading 999")]
    [InlineData(
        "Delta 123 maintain speed 500")]
    public void ToEatsCommand_RejectsUnsafeValues(
        string transcript)
    {
        ParsedVoiceCommand parsed =
            _parser.Parse(transcript);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => parsed.ToEatsCommand());
    }
}