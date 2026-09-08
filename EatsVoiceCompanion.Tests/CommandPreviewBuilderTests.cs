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

    [Theory]
    [InlineData(
        VoiceInstructionType.CrossAtAltitude,
        "OZZZI 12000",
        "DAL123 XOZZZI@120")]
    [InlineData(
        VoiceInstructionType.CrossAtAltitudeAndSpeed,
        "OZZZI 12000 250",
        "DAL123 XOZZZI@120@250K")]
    [InlineData(
        VoiceInstructionType.DescendVia,
        "",
        "DAL123 DV")]
    [InlineData(
        VoiceInstructionType.DescendViaExceptMaintain,
        "12000",
        "DAL123 DVXM120")]
    [InlineData(
        VoiceInstructionType.Altimeter,
        "2992",
        "DAL123 A2992")]
    [InlineData(
        VoiceInstructionType.ComplyWithPublishedSpeeds,
        "HOMER",
        "DAL123 DV CWS@HOMER")]
    public void Build_ReturnsNewCommandFamilies(
        VoiceInstructionType type,
        string value,
        string expected)
    {
        Assert.Equal(
            expected,
            CommandPreviewBuilder.Build("DAL123", type, value));
    }

    [Fact]
    public void BuildCombined_ValidatesEveryToken()
    {
        Assert.Equal(
            "DAL123 DV S250 CWS@HOMER",
            CommandPreviewBuilder.BuildCombined(
                "DAL123",
                "DV S250 CWS@HOMER"));

        Assert.Throws<ArgumentException>(
            () => CommandPreviewBuilder.BuildCombined(
                "DAL123",
                "DV CWS@HOMER BAD"));
    }
}
