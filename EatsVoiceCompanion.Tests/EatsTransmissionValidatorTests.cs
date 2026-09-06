using EatsVoiceCompanion.Core.Commands;

namespace EatsVoiceCompanion.Tests;

public sealed class EatsTransmissionValidatorTests
{
    [Theory]
    [InlineData("aal123 fh270", "AAL123 FH270")]
    [InlineData("AAL123 TLH090 CM230 S250", "AAL123 TLH090 CM230 S250")]
    [InlineData("DAL9 ..LOZIT", "DAL9 ..LOZIT")]
    [InlineData("UAL714 R", "UAL714 R")]
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
    public void Validate_RejectsAnythingOutsideAllowlist(string input)
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(input));
    }
}
