using EatsVoiceCompanion.Core.Speech;
using EatsVoiceCompanion.Core.Commands;

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
                ["Air Canada"] = "ACA",
                ["Blue Streak"] = "JIA",
                ["Piedmont"] = "PDT"
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
        "Delta 9 maintain two five zero knots",
        "DAL9 S250")]
    [InlineData(
        "Delta 9 maintain speed two five zero knots or greater",
        "DAL9 S250+")]
    [InlineData(
        "Delta 9 maintain speed two five zero knots or less",
        "DAL9 S250-")]
    [InlineData(
        "Delta 9 maintain Mach point seven six",
        "DAL9 MM76")]
    [InlineData(
        "Delta 9 maintain Mach decimal seven six or greater",
        "DAL9 MM76+")]
    [InlineData(
        "Delta 9 maintain Mach zero point seven six or less",
        "DAL9 MM76-")]
    [InlineData("Delta 9 resume normal speed", "DAL9 RNS")]
    [InlineData("Delta 9 say indicated speed", "DAL9 SI")]
    [InlineData("Delta 9 say airspeed", "DAL9 SI")]
    [InlineData("Delta 9 say Mach number", "DAL9 SM")]
    [InlineData("Delta 9 say normal speed and Mach", "DAL9 SNS")]
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
    [InlineData(
        "Piedmont 5915 climb and maintain flight level two three zero",
        "PDT5915 CM230")]
    [InlineData(
        "Blue Streak 5596 descend via",
        "JIA5596 DV")]
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

    [Theory]
    [InlineData(
        "American 1307 cross OZZZI at and maintain one two thousand at two five zero knots",
        "AAL1307 XOZZZI@120@250K")]
    [InlineData(
        "American 1307 cross OZZZI at 12,000",
        "AAL1307 XOZZZI@120")]
    [InlineData(
        "American 1307 the Atlanta altimeter 29.92",
        "AAL1307 A2992")]
    [InlineData(
        "American 1307 descend via",
        "AAL1307 DV")]
    [InlineData(
        "American 1307 descend via except maintain one two thousand",
        "AAL1307 DVXM120")]
    [InlineData(
        "American 1307 descend via the BANKR Five arrival",
        "AAL1307 DV")]
    [InlineData(
        "American 1307 descend via BANKR five arrival except maintain one two thousand",
        "AAL1307 DVXM120")]
    public void Parse_AcceptsDocumentedEatsCommandFamilies(
        string transcript,
        string expected)
    {
        Assert.Equal(expected, _parser.Parse(transcript).ToEatsCommand());
    }

    [Fact]
    public void Parse_AcceptsCombinedCrossingAndAltimeter()
    {
        ParsedVoiceCommand parsed = _parser.Parse(
            "American 1307 cross OZZZI at and maintain " +
            "one two thousand at two five zero knots, " +
            "the Atlanta altimeter two niner niner two");

        Assert.Equal(2, parsed.Instructions.Count);
        Assert.Equal(
            "AAL1307 XOZZZI@120@250K A2992",
            parsed.ToEatsCommand());
    }

    [Theory]
    [InlineData(
        "American 1307 descend via then comply with speed restrictions at HOMER",
        "AAL1307 DV CWS@HOMER")]
    [InlineData(
        "American 1307 descend via and comply with published speeds at HOMER",
        "AAL1307 DV CWS@HOMER")]
    [InlineData(
        "American 1307 descend via, maintain speed two five zero, " +
        "then resume published speed at HOMER",
        "AAL1307 DV S250 CWS@HOMER")]
    public void Parse_AcceptsPublishedSpeedCompliance(
        string transcript,
        string expected)
    {
        ParsedVoiceCommand parsed = _parser.Parse(transcript);

        Assert.Equal(expected, parsed.ToEatsCommand());
        Assert.Equal(
            expected,
            EatsTransmissionValidator.Validate(parsed.ToEatsCommand()));
    }

    [Theory]
    [InlineData(
        "American 1307 descend at pilot's discretion maintain one two thousand",
        "AAL1307 PD120")]
    [InlineData(
        "American 1307 descend at pilots discretion to flight level two four zero",
        "AAL1307 PD240")]
    [InlineData(
        "American 1307 expedite",
        "AAL1307 EXP")]
    [InlineData(
        "American 1307 expedite descent through flight level two eight zero",
        "AAL1307 EXP280")]
    [InlineData(
        "American 1307 expedite climb to one two thousand",
        "AAL1307 EXP120")]
    [InlineData(
        "American 1307 report leaving flight level two four zero",
        "AAL1307 RL240")]
    [InlineData(
        "American 1307 report reaching one two thousand",
        "AAL1307 RR120")]
    [InlineData(
        "American 1307 say altitude",
        "AAL1307 SA")]
    public void Parse_AcceptsAltitudeOperations(
        string transcript,
        string expected)
    {
        ParsedVoiceCommand parsed = _parser.Parse(transcript);

        Assert.Equal(expected, parsed.ToEatsCommand());
        Assert.Equal(
            expected,
            EatsTransmissionValidator.Validate(parsed.ToEatsCommand()));
    }

    [Fact]
    public void Parse_AcceptsAltitudeCommandFollowedByExpedite()
    {
        ParsedVoiceCommand parsed = _parser.Parse(
            "American 1307 descend and maintain one two thousand " +
            "then expedite");

        Assert.Equal("AAL1307 DM120 EXP", parsed.ToEatsCommand());
        Assert.Equal(
            "AAL1307 DM120 EXP",
            EatsTransmissionValidator.Validate(parsed.ToEatsCommand()));
    }

    [Fact]
    public void Parse_AcceptsMultipleIndependentInstructions()
    {
        ParsedVoiceCommand parsed = _parser.Parse(
            "Delta 123 turn left heading two seven zero, " +
            "then descend and maintain one zero thousand, " +
            "and maintain speed two five zero knots");

        Assert.Equal(3, parsed.Instructions.Count);
        Assert.Equal(
            "DAL123 TLH270 DM100 S250",
            parsed.ToEatsCommand());
    }

    [Theory]
    [InlineData(
        "American 1307 cross OZZZI at and maintain " +
        "one two thousand at two five niner knots")]
    [InlineData(
        "American 1307 cross OZZZI at or above one two thousand")]
    public void Parse_RejectsUnsafeOrUnverifiableClearances(
        string transcript)
    {
        ParsedVoiceCommand? parsed = null;

        Exception exception = Record.Exception(
            () =>
            {
                parsed = _parser.Parse(transcript);
                EatsTransmissionValidator.Validate(
                    parsed.ToEatsCommand());
            });

        Assert.NotNull(exception);
    }

    [Fact]
    public void Parse_PreservesNamedStarForSafetyVerification()
    {
        ParsedVoiceCommand parsed = _parser.Parse(
            "American 1307 descend via the BANKR Five arrival");

        Assert.Equal("BANKR5", parsed.Instructions[0].TextValue);
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
    public void Parse_RejectsEntireSequenceWhenLaterClauseIsUnsupported()
    {
        Assert.Throws<InvalidOperationException>(
            () => _parser.Parse(
                "Delta 123 turn left heading 270 then contact tower"));
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
