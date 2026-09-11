using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class ActiveCallsignPromptBuilderTests
{
    private static readonly IReadOnlyDictionary<string, string>
        AirlineAliases =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["DELTA"] = "DAL",
                ["UPS"] = "UPS",
                ["PIEDMONT"] = "PDT",
                ["AMERICAN"] = "AAL"
            };

    [Fact]
    public void Build_CreatesPromptForActiveAirlines()
    {
        string[] activeCallsigns =
        {
            "DAL1380",
            "UPS1076",
            "PDT5910"
        };

        string result =
            ActiveCallsignPromptBuilder.Build(
                activeCallsigns,
                AirlineAliases);

        Assert.Equal(
            "Active aircraft callsigns: " +
            "Delta 1380. UPS 1076. Piedmont 5910.",
            result);
    }

    [Fact]
    public void Build_IncludesNNumberCallsignsAndIgnoresUnsupportedValues()
    {
        string[] activeCallsigns =
        {
            "N418GJ",
            "UNKNOWN",
            "",
            "12345"
        };

        string result =
            ActiveCallsignPromptBuilder.Build(
                activeCallsigns,
                AirlineAliases);

        Assert.Equal(
            "Active aircraft callsigns: " +
            "November Four One Eight Golf Juliett.",
            result);
    }

    [Fact]
    public void Build_UsesLongestMatchingDesignator()
    {
        IReadOnlyDictionary<string, string> aliases =
            new Dictionary<string, string>
            {
                ["ALPHA"] = "AA",
                ["AMERICAN"] = "AAL"
            };

        string result =
            ActiveCallsignPromptBuilder.Build(
                new[] { "AAL1256" },
                aliases);

        Assert.Equal(
            "Active aircraft callsigns: American 1256.",
            result);
    }

    [Fact]
    public void Build_RemovesDuplicateCallsigns()
    {
        string[] activeCallsigns =
        {
            "DAL1380",
            "dal1380"
        };

        string result =
            ActiveCallsignPromptBuilder.Build(
                activeCallsigns,
                AirlineAliases);

        Assert.Equal(
            "Active aircraft callsigns: Delta 1380.",
            result);
    }

    [Fact]
    public void Build_RespectsMaximumCallsigns()
    {
        string[] activeCallsigns =
        {
            "DAL1",
            "DAL2",
            "DAL3"
        };

        string result =
            ActiveCallsignPromptBuilder.Build(
                activeCallsigns,
                AirlineAliases,
                maximumCallsigns: 2);

        Assert.Equal(
            "Active aircraft callsigns: Delta 1. Delta 2.",
            result);
    }

    [Fact]
    public void Build_RejectsInvalidMaximum()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ActiveCallsignPromptBuilder.Build(
                Array.Empty<string>(),
                AirlineAliases,
                maximumCallsigns: 0));
    }
}
