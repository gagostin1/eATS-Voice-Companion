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
            ("descend at pilot's discretion maintain one two thousand", "PD120"),
            ("expedite", "EXP"),
            ("expedite descent through flight level two eight zero", "EXP280"),
            ("report leaving flight level two four zero", "RL240"),
            ("report reaching one two thousand", "RR120"),
            ("say altitude", "SA"),
            ("proceed direct OZZZI", "..OZZZI"),
            ("roger", "R"),
            ("cross OZZZI at one two thousand", "XOZZZI@120"),
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
}
