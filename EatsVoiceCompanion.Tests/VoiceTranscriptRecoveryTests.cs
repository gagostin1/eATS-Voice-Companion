using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class VoiceTranscriptRecoveryTests
{
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ENDEAVOR"] = "EDV",
            ["BLUE STREAK"] = "JIA",
            ["EXECJET"] = "EJA",
            ["PIEDMONT"] = "PDT",
            ["AMERICAN"] = "AAL",
            ["DELTA"] = "DAL"
        };

    [Theory]
    [InlineData(
        "End of our 48-26 Atlanta Center. " +
        "Climate Maintain Flight Level 2-3-0.")]
    [InlineData(
        "Endeavor 4826 at Lenna Center. " +
        "Climb and maintain flight level 230.")]
    public void TryRecover_RepairsCallsignPositionAndClimbPhrase(
        string transcript)
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                transcript,
                ["EDV4826", "AAL1307"],
                Aliases,
                new Dictionary<string, string>());

        Assert.NotNull(result);
        VoiceCommandParser parser = new(Aliases);
        Assert.Equal(
            "EDV4826 CM230",
            parser.Parse(result.RecoveredTranscript).ToEatsCommand());
    }

    [Theory]
    [InlineData(
        "BLUZZHIC 55.96. SET LENNER center to send via " +
        "the Bancher five arrival.")]
    [InlineData(
        "BLUZZTRICK 55.96. DESCEND via the Bancher five arrival.")]
    public void TryRecover_UsesActiveCallsignAndAssignedStar(
        string transcript)
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                transcript,
                ["JIA5596", "AAL1393"],
                Aliases,
                new Dictionary<string, string>
                {
                    ["JIA5596"] = "BANKR5"
                });

        Assert.NotNull(result);
        Assert.Equal(
            "JIA5596 descend via the BANKR five arrival",
            result.RecoveredTranscript);
        Assert.True(result.StarWasCorrected);
    }

    [Fact]
    public void TryRecover_RejectsAmbiguousActiveCallsign()
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                "Endeavor 4826 climate maintain flight level 230",
                ["EDV4825", "EDV4827"],
                Aliases,
                new Dictionary<string, string>());

        Assert.Null(result);
    }

    [Fact]
    public void TryRecover_DoesNotMapUnsupportedInstruction()
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                "Endeavor 4826 contact Atlanta Center",
                ["EDV4826"],
                Aliases,
                new Dictionary<string, string>());

        Assert.Null(result);
    }

    [Theory]
    [InlineData(
        "Exact jet seven fifty-six at Atlanta Center. " +
        "Climbing to maintain flight level two three zero.",
        "EJA756 CM230")]
    [InlineData(
        "PEDMOT 5915. Atlanta Center. Climate Maintain. " +
        "Flight Level 230.",
        "PDT5915 CM230")]
    public void TryRecover_RepairsAdditionalObservedClimbTranscripts(
        string transcript,
        string expectedCommand)
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                transcript,
                ["EJA756", "PDT5915"],
                Aliases,
                new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(
            expectedCommand,
            new VoiceCommandParser(Aliases)
                .Parse(result.RecoveredTranscript)
                .ToEatsCommand());
    }

    [Fact]
    public void TryRecover_RepairsAdditionalObservedDescendViaTranscript()
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                "BLUZZ SRIK 5596 AT LENNA CENTER. " +
                "DESUN via the BANKER five arrival.",
                ["JIA5596"],
                Aliases,
                new Dictionary<string, string>
                {
                    ["JIA5596"] = "BANKR5"
                });

        Assert.NotNull(result);
        Assert.Equal(
            "JIA5596 DV",
            new VoiceCommandParser(Aliases)
                .Parse(result.RecoveredTranscript)
                .ToEatsCommand());
    }

    [Fact]
    public void TryRecover_RepairsSplitAssignedStarName()
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                "American 924 Atlanta Center to send via " +
                "the bank or five arrival",
                ["AAL924"],
                Aliases,
                new Dictionary<string, string>
                {
                    ["AAL924"] = "BANKR5"
                });

        Assert.NotNull(result);
        Assert.True(result.StarWasCorrected);
        Assert.Equal(
            "AAL924 DV",
            new VoiceCommandParser(Aliases)
                .Parse(result.RecoveredTranscript)
                .ToEatsCommand());
    }

    [Fact]
    public void TryRecover_RepairsMangledFlightLevelPhrase()
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                "Delta 1818 Atlanta Center climate maintainer " +
                "for available 330",
                ["DAL1818"],
                Aliases,
                new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(
            "DAL1818 CM330",
            new VoiceCommandParser(Aliases)
                .Parse(result.RecoveredTranscript)
                .ToEatsCommand());
    }

    [Theory]
    [InlineData(
        "November six eight three niner Romeo Atlanta Center " +
        "climate maintain flight level two three zero",
        "N6839R CM230")]
    [InlineData(
        "November three niner Romeo Atlanta Center " +
        "fly heading two seven zero",
        "N6839R FH270")]
    public void TryRecover_UsesActiveNNumber(
        string transcript,
        string expectedCommand)
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                transcript,
                ["N6839R", "DAL123"],
                Aliases,
                new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(
            expectedCommand,
            new VoiceCommandParser(Aliases)
                .Parse(result.RecoveredTranscript)
                .ToEatsCommand());
    }

    [Fact]
    public void TryRecover_RejectsAmbiguousAbbreviatedNNumber()
    {
        const string transcript =
            "November three niner Romeo fly heading two seven zero";

        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                transcript,
                ["N6839R", "N1239R"],
                Aliases,
                new Dictionary<string, string>());
        IReadOnlyList<string> suggestions =
            VoiceTranscriptRecovery.SuggestCallsigns(
                transcript,
                ["N6839R", "N1239R"],
                Aliases);

        Assert.Null(result);
        Assert.Equal(new[] { "N6839R", "N1239R" }, suggestions);
    }

    [Theory]
    [InlineData(
        "Delter 123 descend at pilots discrecion maintain one two thousand",
        "DAL123 PD120")]
    [InlineData(
        "Delter 123 expidite through flight level two eight zero",
        "DAL123 EXP280")]
    [InlineData(
        "Delter 123 report leavin flight level two four zero",
        "DAL123 RL240")]
    public void TryRecover_RepairsAltitudeOperationTranscripts(
        string transcript,
        string expectedCommand)
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                transcript,
                ["DAL123"],
                Aliases,
                new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(
            expectedCommand,
            new VoiceCommandParser(Aliases)
                .Parse(result.RecoveredTranscript)
                .ToEatsCommand());
    }

    [Theory]
    [InlineData(
        "Delter 123 squak four three two one",
        "DAL123 SQ4321")]
    [InlineData(
        "Delter 123 squak ident",
        "DAL123 ID")]
    [InlineData(
        "Delter 123 squak altitood",
        "DAL123 SQALT")]
    public void TryRecover_RepairsTransponderTranscripts(
        string transcript,
        string expectedCommand)
    {
        VoiceTranscriptRecoveryResult? result =
            VoiceTranscriptRecovery.TryRecover(
                transcript,
                ["DAL123"],
                Aliases,
                new Dictionary<string, string>());

        Assert.NotNull(result);
        Assert.Equal(
            expectedCommand,
            new VoiceCommandParser(Aliases)
                .Parse(result.RecoveredTranscript)
                .ToEatsCommand());
    }
}
