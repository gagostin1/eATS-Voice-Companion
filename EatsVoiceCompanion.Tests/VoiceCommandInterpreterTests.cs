using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class VoiceCommandInterpreterTests
{
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["American"] = "AAL",
            ["Delta"] = "DAL",
            ["United"] = "UAL",
            ["Blue Streak"] = "JIA",
            ["Piedmont"] = "PDT",
            ["Brickyard"] = "RPA",
            ["Endeavor"] = "EDV",
            ["SkyWest"] = "SKW"
        };

    public static IEnumerable<object[]> StrictScenarioCases()
    {
        (string Spoken, string Designator)[] airlines =
        [
            ("American", "AAL"),
            ("Delta", "DAL"),
            ("United", "UAL"),
            ("Blue Streak", "JIA"),
            ("Piedmont", "PDT"),
            ("Brickyard", "RPA"),
            ("Endeavor", "EDV"),
            ("SkyWest", "SKW")
        ];
        string[] flightNumbers = ["123", "924", "1818", "5596"];
        string[] positions =
        [
            "Atlanta Center",
            "Jacksonville Center",
            "Denver Center",
            "Memphis Center"
        ];
        string[] stars =
        [
            "BANKR5",
            "CHSLY6",
            "JJEDI3",
            "PARQR4",
            "JONZE5",
            "SWFFT3",
            "EAGUL6",
            "HOBTT2"
        ];
        (string Phrase, string Token)[] instructions =
        [
            ("fly heading two seven zero", "FH270"),
            ("turn left heading one eight zero", "TLH180"),
            ("turn right heading three six zero", "TRH360"),
            ("climb and maintain flight level two three zero", "CM230"),
            ("descend and maintain one two thousand", "DM120"),
            ("maintain speed two five zero", "S250"),
            ("maintain speed two five zero or greater", "S250+"),
            ("maintain speed two five zero or less", "S250-"),
            ("maintain mach point seven six", "MM76"),
            ("maintain mach point seven six or greater", "MM76+"),
            ("maintain mach point seven six or less", "MM76-"),
            ("resume normal speed", "RNS"),
            ("say indicated speed", "SI"),
            ("say mach", "SM"),
            ("say normal speed and mach", "SNS"),
            ("fly present heading", "PH"),
            ("expect ILS runway two five left approach", "EILS25L"),
            ("fly heading two two zero then intercept final approach course", "FH220 INTC"),
            ("cleared for the approach", "CA"),
            ("say approach request", "SAR"),
            ("cleared for the approach then reduce to final approach speed", "CA S-"),
            ("contact Jacksonville Center one three two point three seven", "*3237"),
            ("remain this frequency", "*0"),
            ("say again", "?"),
            ("stand by", "SBY"),
            ("squawk four three two one", "SQ4321"),
            ("squawk zero four two one then ident", "SQ0421 ID"),
            ("squawk altitude", "SQALT"),
            ("squawk normal", "SQNORM"),
            ("squawk standby", "SQSBY"),
            ("squawk VFR", "SQVFR"),
            ("stop altitude squawk", "STOPALTSQ"),
            ("descend at pilot's discretion maintain one two thousand", "PD120"),
            ("expedite", "EXP"),
            ("expedite descent through flight level two eight zero", "EXP280"),
            ("report leaving flight level two four zero", "RL240"),
            ("report reaching one two thousand", "RR120"),
            ("say altitude", "SA"),
            ("proceed direct OZZZI", "..OZZZI"),
            ("roger", "R"),
            ("cross OZZZI at one two thousand", "XOZZZI@120"),
            ("cross ten miles northwest of BURGL at flight level three three zero", "X10NW.BURGL@330"),
            ("the Atlanta altimeter two niner niner two", "A2992")
        ];

        int scenarioIndex = 0;

        foreach ((string spoken, string designator) in airlines)
        {
            foreach (string flightNumber in flightNumbers)
            {
                string position = positions[scenarioIndex % positions.Length];
                string star = stars[scenarioIndex % stars.Length];

                foreach ((string phrase, string token) in instructions)
                {
                    string callsign = designator + flightNumber;

                    yield return
                    [
                        $"{spoken} {flightNumber}, {position}, {phrase}",
                        position,
                        callsign,
                        $"{callsign} {token}",
                        star
                    ];
                }

                string starPhrase = StarNameNormalizer.ToPromptPhrase(star);

                yield return
                [
                    $"{spoken} {flightNumber}, {position}, descend via " +
                    $"the {starPhrase} arrival",
                    position,
                    designator + flightNumber,
                    $"{designator}{flightNumber} DV",
                    star
                ];

                yield return
                [
                    $"{spoken} {flightNumber}, {position}, descend via " +
                    $"the {starPhrase} arrival except maintain " +
                    "one two thousand",
                    position,
                    designator + flightNumber,
                    $"{designator}{flightNumber} DVXM120",
                    star
                ];

                yield return
                [
                    $"{spoken} {flightNumber}, {position}, descend via " +
                    $"the {starPhrase} arrival then comply with speed " +
                    "restrictions at HOMER",
                    position,
                    designator + flightNumber,
                    $"{designator}{flightNumber} DV CWS@HOMER",
                    star
                ];

                scenarioIndex++;
            }
        }
    }

    public static IEnumerable<object[]> GeneralAviationScenarioCases()
    {
        (string Spoken, string Callsign)[] registrations =
        [
            ("November two five three papa zulu", "N253PZ"),
            ("November six eight three niner romeo", "N6839R"),
            ("November five seven seven kilo charlie", "N577KC"),
            ("November six three one sierra foxtrot", "N631SF"),
            ("November three five zero papa charlie", "N350PC"),
            ("November one niner three papa papa", "N193PP")
        ];
        (string Phrase, string Token)[] instructions =
        [
            ("fly heading two seven zero", "FH270"),
            ("climb and maintain flight level two three zero", "CM230"),
            ("descend and maintain one two thousand", "DM120"),
            ("maintain speed two five zero", "S250"),
            ("proceed direct OZZZI", "..OZZZI"),
            ("contact Atlanta Center one two five point one", "*251"),
            ("squawk four three two one", "SQ4321"),
            ("squawk ident", "ID"),
            ("say altitude", "SA"),
            ("say again", "?"),
            ("cross five miles south of OZZZI at one two thousand", "X5S.OZZZI@120"),
            ("roger", "R")
        ];

        foreach ((string spoken, string callsign) in registrations)
        {
            foreach ((string phrase, string token) in instructions)
            {
                yield return
                [
                    $"{spoken}, Atlanta Center, {phrase}",
                    callsign,
                    $"{callsign} {token}"
                ];
            }
        }
    }

    [Theory]
    [MemberData(nameof(GeneralAviationScenarioCases))]
    public void Interpret_HandlesGeneralAviationScenarioMatrix(
        string transcript,
        string callsign,
        string expectedCommand)
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                transcript,
                "Atlanta Center",
                new HashSet<string>(
                    [callsign, "N999AB", "DAL123"],
                    StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>());

        Assert.Equal(expectedCommand, result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.Strict, result.Kind);
    }

    [Fact]
    public void Interpret_ResolvesUniqueAbbreviatedNNumberFromSnapshot()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "November three niner romeo fly heading two seven zero",
                null,
                new HashSet<string>(
                    ["N6839R", "N253PZ"],
                    StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>());

        Assert.Equal("N6839R FH270", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.Recovered, result.Kind);
    }

    [Fact]
    public void Interpret_RejectsAmbiguousAbbreviatedNNumber()
    {
        VoiceInterpretationException exception = Assert.Throws<
            VoiceInterpretationException>(
                () => new VoiceCommandInterpreter(Aliases).Interpret(
                    "November three niner romeo fly heading two seven zero",
                    null,
                    new HashSet<string>(
                        ["N6839R", "N1239R"],
                        StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>()));

        Assert.Equal(
            new[] { "N6839R", "N1239R" },
            exception.CandidateCallsigns);
    }

    [Fact]
    public void Interpret_PrefersExactActiveShortNNumber()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "November three niner romeo fly heading two seven zero",
                null,
                new HashSet<string>(
                    ["N39R", "N6839R"],
                    StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>());

        Assert.Equal("N39R FH270", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.Strict, result.Kind);
    }

    [Theory]
    [MemberData(nameof(StrictScenarioCases))]
    public void Interpret_HandlesGeneratedScenarioMatrix(
        string transcript,
        string position,
        string callsign,
        string expectedCommand,
        string star)
    {
        IReadOnlySet<string> activeCallsigns =
            new HashSet<string>(
                [callsign, "AAL999", "DAL998"],
                StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> stars =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [callsign] = star
            };

        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                transcript,
                position,
                activeCallsigns,
                stars);

        Assert.Equal(expectedCommand, result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.Strict, result.Kind);
    }

    public static IEnumerable<object[]> RecoveryScenarioCases()
    {
        foreach ((string spoken, string designator) in new[]
                 {
                     ("American", "AAL"),
                     ("Delta", "DAL"),
                     ("United", "UAL"),
                     ("Blue Streak", "JIA"),
                     ("Piedmont", "PDT"),
                     ("Brickyard", "RPA"),
                     ("Endeavor", "EDV"),
                     ("SkyWest", "SKW")
                 })
        {
            foreach (string flightNumber in new[]
                     {
                         "123",
                         "924",
                         "1818",
                         "5596"
                     })
            {
                yield return
                [
                    $"{spoken} {flightNumber} at Lenna Center " +
                    "climate maintain flight level two three zero",
                    designator + flightNumber
                ];
            }
        }
    }

    [Theory]
    [MemberData(nameof(RecoveryScenarioCases))]
    public void Interpret_RecoversAcrossAirlinesAndFlightNumbers(
        string transcript,
        string callsign)
    {
        IReadOnlySet<string> activeCallsigns =
            new HashSet<string>(
                [callsign, "AAL997", "DAL998"],
                StringComparer.OrdinalIgnoreCase);

        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                transcript,
                "Atlanta Center",
                activeCallsigns,
                new Dictionary<string, string>());

        Assert.Equal(VoiceInterpretationKind.Recovered, result.Kind);
        Assert.Equal($"{callsign} CM230", result.Command.ToEatsCommand());
    }

    [Fact]
    public void Interpret_ReportsCandidatesWhenRecoveryIsAmbiguous()
    {
        IReadOnlySet<string> activeCallsigns =
            new HashSet<string>(
                ["JIA5595", "JIA5597"],
                StringComparer.OrdinalIgnoreCase);

        VoiceInterpretationException exception = Assert.Throws<
            VoiceInterpretationException>(() =>
                new VoiceCommandInterpreter(Aliases).Interpret(
                    "Blue Streek 5596 climate maintain flight level 230",
                    "Atlanta Center",
                    activeCallsigns,
                    new Dictionary<string, string>()));

        Assert.Equal(2, exception.CandidateCallsigns.Count);
        Assert.Contains("JIA5595", exception.Message);
        Assert.Contains("JIA5597", exception.Message);
    }

    [Fact]
    public void Interpret_UsesAircraftRouteToCorrectPhoneticFix()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "Delta 688 Atlanta Center cross Aussie at and maintain " +
                "one 3,000 at 250 knots",
                "Atlanta Center",
                new HashSet<string>(["DAL688"]),
                new Dictionary<string, string>(),
                new Dictionary<string, IReadOnlySet<string>>
                {
                    ["DAL688"] = new HashSet<string>(
                        ["DGESS", "OZZZI", "HAARY"],
                        StringComparer.OrdinalIgnoreCase)
                });

        Assert.Equal("DAL688 XOZZZI@130@250K", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.Recovered, result.Kind);
        Assert.True(result.RouteFixWasCorrected);
    }

    [Fact]
    public void Interpret_RecoversClearedDirectAndAircraftRouteFix()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "Delta 688 at Lenis Center clear direct Aussie",
                "Atlanta Center",
                new HashSet<string>(["DAL688"]),
                new Dictionary<string, string>(),
                new Dictionary<string, IReadOnlySet<string>>
                {
                    ["DAL688"] = new HashSet<string>(
                        ["DGESS", "OZZZI", "HAARY"],
                        StringComparer.OrdinalIgnoreCase)
                });

        Assert.Equal("DAL688 ..OZZZI", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.Recovered, result.Kind);
        Assert.True(result.RouteFixWasCorrected);
    }

    [Fact]
    public void Interpret_BlocksUnmatchedFixWhenAircraftRouteIsAvailable()
    {
        VoiceInterpretationException exception = Assert.Throws<
            VoiceInterpretationException>(() =>
                new VoiceCommandInterpreter(Aliases).Interpret(
                    "Delta 688 proceed direct unknown",
                    null,
                    new HashSet<string>(["DAL688"]),
                    new Dictionary<string, string>(),
                    new Dictionary<string, IReadOnlySet<string>>
                    {
                        ["DAL688"] = new HashSet<string>(
                            ["DGESS", "OZZZI", "HAARY"],
                            StringComparer.OrdinalIgnoreCase)
                    }));

        Assert.Contains("could not be matched uniquely", exception.Message);
    }

    [Fact]
    public void Interpret_BlocksAmbiguousPhoneticRouteFix()
    {
        VoiceInterpretationException exception = Assert.Throws<
            VoiceInterpretationException>(() =>
                new VoiceCommandInterpreter(Aliases).Interpret(
                    "Delta 688 proceed direct Aussie",
                    null,
                    new HashSet<string>(["DAL688"]),
                    new Dictionary<string, string>(),
                    new Dictionary<string, IReadOnlySet<string>>
                    {
                        ["DAL688"] = new HashSet<string>(
                            ["OZZZI", "OSSEY"],
                            StringComparer.OrdinalIgnoreCase)
                    }));

        Assert.Contains("could not be matched uniquely", exception.Message);
    }
}
