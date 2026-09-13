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
    public void Interpret_ChoosesAmbiguousAbbreviatedNNumber()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "November three niner romeo fly heading two seven zero",
                null,
                new HashSet<string>(
                    ["N6839R", "N1239R"],
                    StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>());

        Assert.Equal("N1239R FH270", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.BestHypothesis, result.Kind);
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
    public void Interpret_ChoosesHighestRankedCallsignWhenAmbiguous()
    {
        IReadOnlySet<string> activeCallsigns =
            new HashSet<string>(
                ["JIA5595", "JIA5597"],
                StringComparer.OrdinalIgnoreCase);

        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "Blue Streek 5596 climate maintain flight level 230",
                "Atlanta Center",
                activeCallsigns,
                new Dictionary<string, string>());

        Assert.Equal("JIA5595 CM230", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.BestHypothesis, result.Kind);
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
    public void Interpret_UsesUniqueExactFlightNumberWhenAirlineIsMangled()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "Those are 1486, Atlanta Center, cross Aussie at and " +
                "maintain 1-3,000, the Atlanta altimeter 29er 66",
                "Atlanta Center",
                new HashSet<string>(["DAL1486", "SWA1510", "AAL1487"]),
                new Dictionary<string, string>(),
                new Dictionary<string, IReadOnlySet<string>>
                {
                    ["DAL1486"] = new HashSet<string>(
                        ["WINNG", "OZZZI", "HAARY"],
                        StringComparer.OrdinalIgnoreCase)
                });

        Assert.Equal(
            "DAL1486 XOZZZI@130 A2966",
            result.Command.ToEatsCommand());
        Assert.True(result.RouteFixWasCorrected);
    }

    [Fact]
    public void Interpret_RecoversWhisperDisjunctionsBetweenAltimeterDigits()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "Delta 1288, Atlanta Center, cross Aussie at and maintain " +
                "1-3,000, the Atlanta altimeter 2-9 or 9 or 2",
                "Atlanta Center",
                new HashSet<string>(["DAL1288"]),
                new Dictionary<string, string>(),
                new Dictionary<string, IReadOnlySet<string>>
                {
                    ["DAL1288"] = new HashSet<string>(
                        ["WINNG", "OZZZI", "HAARY"],
                        StringComparer.OrdinalIgnoreCase)
                });

        Assert.Equal(
            "DAL1288 XOZZZI@130 A2992",
            result.Command.ToEatsCommand());
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
    public void Interpret_FallsBackWhenFixCannotBeMatched()
    {
        VoiceCommandInterpretation result =
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
                    });

        Assert.Equal("DAL688 R", result.Command.ToEatsCommand());
    }

    [Fact]
    public void Interpret_FallsBackWhenRouteFixIsAmbiguous()
    {
        VoiceCommandInterpretation result =
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
                    });

        Assert.Equal("DAL688 R", result.Command.ToEatsCommand());
    }

    [Fact]
    public void Interpret_ProducesBestHypothesisForSingleAircraft()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "garbled aircraft fly hedding two seven zero",
                null,
                new HashSet<string>(["DAL688"]),
                new Dictionary<string, string>());

        Assert.Equal("DAL688 FH270", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.BestHypothesis, result.Kind);
        Assert.InRange(result.ConfidenceScore, 0.40, 1.0);
    }

    [Fact]
    public void Interpret_UsesSpokenCharactersForRouteFixHypothesis()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "Delta 688 direct oh zee zee zee eye",
                null,
                new HashSet<string>(["DAL688"]),
                new Dictionary<string, string>(),
                new Dictionary<string, IReadOnlySet<string>>
                {
                    ["DAL688"] = new HashSet<string>(
                        ["DGESS", "OZZZI", "HAARY"],
                        StringComparer.OrdinalIgnoreCase)
                });

        Assert.Equal("DAL688 ..OZZZI", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.BestHypothesis, result.Kind);
        Assert.True(result.RouteFixWasCorrected);
    }

    [Fact]
    public void Interpret_ChoosesTopTiedDirectionHypothesis()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                    "Delta 688 turn heading two seven zero",
                    null,
                    new HashSet<string>(["DAL688"]),
                    new Dictionary<string, string>());

        Assert.Equal("DAL688 TLH270", result.Command.ToEatsCommand());
    }

    [Fact]
    public void Interpret_UsesActiveContextToCorrectFlightNumberHypothesis()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "Delta 680 fly heading two seven zero",
                null,
                new HashSet<string>(["DAL688", "AAL123"]),
                new Dictionary<string, string>());

        Assert.Equal("DAL688 FH270", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.BestHypothesis, result.Kind);
    }

    [Fact]
    public void Interpret_FallsBackWhenMandatoryValueIsMissing()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                    "Delta 688 climb and maintain",
                    null,
                    new HashSet<string>(["DAL688"]),
                    new Dictionary<string, string>());

        Assert.Equal("DAL688 R", result.Command.ToEatsCommand());
    }

    [Theory]
    [InlineData("Delta 688 climb and maintain 230")]
    [InlineData("Delta 688 climb and maintain two three zero")]
    public void Interpret_InfersDroppedFlightLevelWordsWhenUnambiguous(
        string transcript)
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                transcript,
                null,
                new HashSet<string>(["DAL688"]),
                new Dictionary<string, string>());

        Assert.Equal("DAL688 CM230", result.Command.ToEatsCommand());
        Assert.NotEqual(VoiceInterpretationKind.Strict, result.Kind);
    }

    [Theory]
    [InlineData(
        "November 4, 7-5, Julia Charley, Atlanta Center, " +
        "CLIMB AND MAINTAIN, FOOT LEVEL 2-3-0.",
        "N475JC CM230")]
    [InlineData(
        "November 4, 7-5, Juliet Charlie, climb and maintain " +
        "flight level 2-3-0.",
        "N475JC CM230")]
    [InlineData(
        "November 5-0-8, Julia Papa, Atlanta Center, Climb and " +
        "Maintain, Flight Level 2-4-0.",
        "N508JP CM240")]
    public void Interpret_RecoversLiveNNumberTranscriptions(
        string transcript,
        string expectedCommand)
    {
        string activeCallsign = expectedCommand.Split(' ')[0];
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                transcript,
                "Atlanta Center",
                new HashSet<string>([activeCallsign]),
                new Dictionary<string, string>());

        Assert.Equal(expectedCommand, result.Command.ToEatsCommand());
    }

    [Fact]
    public void Interpret_RecoversCompactAirlineAndContextualStar()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "AMERICAN1401 AT LINA CENTER, DESCEND VIA THE " +
                "JONES Z5 ARRIVAL.",
                "Atlanta Center",
                new HashSet<string>(["AAL1401"]),
                new Dictionary<string, string>
                {
                    ["AAL1401"] = "JONZE5"
                });

        Assert.Equal("AAL1401 DV", result.Command.ToEatsCommand());
        Assert.True(result.StarWasCorrected);
        Assert.NotEqual(VoiceInterpretationKind.Strict, result.Kind);
    }

    [Theory]
    [InlineData("American 1395, Altimeter 299er-7.")]
    [InlineData("American 1395, Altimeter 2, Niner, Niner 7.")]
    public void Interpret_RecoversBareAltimeterUsingControllerFacility(
        string transcript)
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                transcript,
                "Atlanta Center",
                new HashSet<string>(["AAL1395"]),
                new Dictionary<string, string>());

        Assert.Equal("AAL1395 A2997", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.BestHypothesis, result.Kind);
    }

    [Fact]
    public void Interpret_AlwaysReturnsCommandForNonEmptySpeechWithContext()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "completely unusable speech",
                "Atlanta Center",
                new HashSet<string>(["DAL688"]),
                new Dictionary<string, string>());

        Assert.Equal("DAL688 R", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.BestHypothesis, result.Kind);
        Assert.Equal(0, result.ConfidenceScore);
    }

    [Theory]
    [InlineData(
        "November 9, 2656, Atlanta Center, Climate Maintain, FL240.")]
    [InlineData(
        "November 9, 2656, Atlanta Center, Climb in and maintain FL240.")]
    public void Interpret_UsesBestActiveNNumberForMalformedRegistration(
        string transcript)
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                transcript,
                "Atlanta Center",
                new HashSet<string>(["N9265S", "AAL1395"]),
                new Dictionary<string, string>());

        Assert.Equal("N9265S CM240", result.Command.ToEatsCommand());
        Assert.Equal(VoiceInterpretationKind.BestHypothesis, result.Kind);
    }

    [Fact]
    public void Interpret_RecoversCompactCombinedDescendViaAndAltimeter()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "American1362, Atlanta Center, descend via the Jonesy " +
                "5 arrival, Altimeter 2, Niner, Niner 7.",
                "Atlanta Center",
                new HashSet<string>(["AAL1362"]),
                new Dictionary<string, string>
                {
                    ["AAL1362"] = "JONZE5"
                });

        Assert.Equal("AAL1362 DV A2997", result.Command.ToEatsCommand());
    }

    [Fact]
    public void Interpret_RecoversDescendVWithCombinedAltimeter()
    {
        VoiceCommandInterpretation result =
            new VoiceCommandInterpreter(Aliases).Interpret(
                "BLUESTREAK5143 ATLANTAS CENTER, DESCEND V of the " +
                "JOHNSY5 ARRIVAL, ATLANTA, ALTIMETER, 2976.",
                "Atlanta Center",
                new HashSet<string>(["JIA5143"]),
                new Dictionary<string, string>
                {
                    ["JIA5143"] = "JONZE5"
                });

        Assert.Equal("JIA5143 DV A2976", result.Command.ToEatsCommand());
    }
}
