using EatsVoiceCompanion.Core.Commands;
using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class CommandPreviewBuilderTests
{
    [Theory]
    [InlineData(VoiceInstructionType.FlyHeading, "270", "DAL123 FH270")]
    [InlineData(VoiceInstructionType.TurnLeftHeading, "5", "DAL123 TLH005")]
    [InlineData(VoiceInstructionType.ClimbAndMaintain, "23000", "DAL123 CM230")]
    [InlineData(VoiceInstructionType.ProceedDirect, "lozit", "DAL123 ..LOZIT")]
    [InlineData(VoiceInstructionType.Roger, "", "DAL123 R")]
    public void Build_ReturnsValidatedTransmission(
        VoiceInstructionType type,
        string value,
        string expected)
    {
        Assert.Equal(
            expected,
            CommandPreviewBuilder.Build("dal123", type, value));
    }

    [Fact]
    public void Build_RejectsInvalidNumericValue()
    {
        Assert.Throws<ArgumentException>(
            () => CommandPreviewBuilder.Build(
                "DAL123",
                VoiceInstructionType.FlyHeading,
                "west"));
    }
}
