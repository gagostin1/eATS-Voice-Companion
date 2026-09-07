using EatsVoiceCompanion.Core.Commands;

namespace EatsVoiceCompanion.Tests;

public sealed class EatsTransmissionValidatorTests
{
    [Theory]
    [InlineData("aal123 fh270", "AAL123 FH270")]
    [InlineData("AAL123 TLH090 CM230 S250", "AAL123 TLH090 CM230 S250")]
    [InlineData("DAL9 ..LOZIT", "DAL9 ..LOZIT")]
    [InlineData("UAL714 R", "UAL714 R")]
    [InlineData("AAL123 A2992", "AAL123 A2992")]
    [InlineData("AAL123 DV S250", "AAL123 DV S250")]
    [InlineData("AAL123 DVXM120", "AAL123 DVXM120")]
    [InlineData("AAL123 XOZZZI@120", "AAL123 XOZZZI@120")]
    [InlineData(
        "AAL123 XOZZZI@120@250K A2992",
        "AAL123 XOZZZI@120@250K A2992")]
    public void Validate_AcceptsSupportedGrammar(
        string input,
        string expected)
    {
        Assert.Equal(expected, EatsTransmissionValidator.Validate(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AAL123")]
    [InlineData("AAL123 FH270\r\nUAL456 FH180")]
    [InlineData("AAL123\tFH270")]
    [InlineData("AAL123 FH270;")]
    [InlineData("AAL123 DELETE")]
    [InlineData("AAL123 FH27")]
    [InlineData("AAL123 FH000")]
    [InlineData("AAL123 FH361")]
    [InlineData("AAL123 CM999")]
    [InlineData("AAL123 S351")]
    [InlineData("AAL123 ..A")]
    [InlineData("AAL123 A992")]
    [InlineData("AAL123 DVXM")]
    [InlineData("AAL123 XOZZZI@120@259K")]
    [InlineData("AAL123 XOZZZI@120@250")]
    [InlineData("AAL123 X@120")]
    public void Validate_RejectsAnythingOutsideAllowlist(string input)
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(input));
    }

    [Fact]
    public void Validate_RejectsSpeedBeforeDescendVia()
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(
                "AAL123 S250 DV"));
    }
}
