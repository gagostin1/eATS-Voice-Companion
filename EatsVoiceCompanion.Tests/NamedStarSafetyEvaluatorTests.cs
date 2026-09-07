using EatsVoiceCompanion.Core.Safety;
using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class NamedStarSafetyEvaluatorTests
{
    [Theory]
    [InlineData("BANKR5", "BANKR5")]
    [InlineData("BANKR five", "BANKR5")]
    [InlineData("eagul six", "EAGUL6")]
    public void StarNormalizer_NormalizesSupportedNames(
        string spoken,
        string expected)
    {
        Assert.Equal(expected, StarNameNormalizer.Normalize(spoken));
    }

    [Fact]
    public void Evaluate_VerifiesMatchingAssignedStar()
    {
        CommandSafetyResult result = NamedStarSafetyEvaluator.Evaluate(
            "JIA5588",
            "BANKR5",
            new Dictionary<string, string> { ["JIA5588"] = "BANKR5" });

        Assert.Equal(CommandSafetyState.Verified, result.State);
    }

    [Fact]
    public void Evaluate_BlocksMismatchedSpokenStar()
    {
        CommandSafetyResult result = NamedStarSafetyEvaluator.Evaluate(
            "JIA5588",
            "EAGUL6",
            new Dictionary<string, string> { ["JIA5588"] = "BANKR5" });

        Assert.Equal(CommandSafetyState.Blocked, result.State);
        Assert.Contains("does not match", result.Message);
    }

    [Fact]
    public void Evaluate_UsesPreviewOnlyWhenRouteIsUnavailable()
    {
        CommandSafetyResult result = NamedStarSafetyEvaluator.Evaluate(
            "JIA5588",
            spokenStar: null,
            new Dictionary<string, string>());

        Assert.Equal(CommandSafetyState.PreviewOnly, result.State);
    }
}
