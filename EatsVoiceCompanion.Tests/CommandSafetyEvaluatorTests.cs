using EatsVoiceCompanion.Core.Safety;

namespace EatsVoiceCompanion.Tests;

public sealed class CommandSafetyEvaluatorTests
{
    private static readonly string[] ActiveCallsigns =
    {
        "AAL123"
    };

    [Fact]
    public void Evaluate_ReturnsPreviewOnlyWithoutFreshSnapshot()
    {
        CommandSafetyResult result = CommandSafetyEvaluator.Evaluate(
            "AAL123",
            hasFreshSnapshot: false,
            ActiveCallsigns);

        Assert.Equal(CommandSafetyState.PreviewOnly, result.State);
    }

    [Fact]
    public void Evaluate_ReturnsVerifiedForExactActiveCallsign()
    {
        CommandSafetyResult result = CommandSafetyEvaluator.Evaluate(
            "aal123",
            hasFreshSnapshot: true,
            ActiveCallsigns);

        Assert.Equal(CommandSafetyState.Verified, result.State);
    }

    [Fact]
    public void Evaluate_ReturnsBlockedForInactiveCallsign()
    {
        CommandSafetyResult result = CommandSafetyEvaluator.Evaluate(
            "AAL124",
            hasFreshSnapshot: true,
            ActiveCallsigns);

        Assert.Equal(CommandSafetyState.Blocked, result.State);
    }

    [Fact]
    public void NotReady_RequiresReason()
    {
        Assert.Throws<ArgumentException>(
            () => CommandSafetyEvaluator.NotReady(""));
    }
}
