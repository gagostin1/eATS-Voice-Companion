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
    [InlineData(
        "AAL123 DV S250 CWS@HOMER",
        "AAL123 DV S250 CWS@HOMER")]
    [InlineData(
        "AAL123 DV ..OZZZI CWS@HOMER",
        "AAL123 DV ..OZZZI CWS@HOMER")]
    [InlineData("AAL123 DVXM120", "AAL123 DVXM120")]
    [InlineData("AAL123 PD120", "AAL123 PD120")]
    [InlineData("AAL123 EXP", "AAL123 EXP")]
    [InlineData("AAL123 DM120 EXP", "AAL123 DM120 EXP")]
    [InlineData("AAL123 EXP280", "AAL123 EXP280")]
    [InlineData("AAL123 RL240 RR120 SA", "AAL123 RL240 RR120 SA")]
    [InlineData("AAL123 S250+", "AAL123 S250+")]
    [InlineData("AAL123 S250-", "AAL123 S250-")]
    [InlineData("AAL123 MM76", "AAL123 MM76")]
    [InlineData("AAL123 MM76+", "AAL123 MM76+")]
    [InlineData("AAL123 MM76-", "AAL123 MM76-")]
    [InlineData("AAL123 RNS SI SM SNS", "AAL123 RNS SI SM SNS")]
    [InlineData("AAL123 PH", "AAL123 PH")]
    [InlineData("AAL123 EILS25L", "AAL123 EILS25L")]
    [InlineData("AAL123 FH220 INTC CA S170", "AAL123 FH220 INTC CA S170")]
    [InlineData("AAL123 SAR", "AAL123 SAR")]
    [InlineData("AAL123 CA S-", "AAL123 CA S-")]
    [InlineData("AAL123 *3237", "AAL123 *3237")]
    [InlineData("AAL123 DM120 *180", "AAL123 DM120 *180")]
    [InlineData("AAL123 *0", "AAL123 *0")]
    [InlineData("AAL123 ?", "AAL123 ?")]
    [InlineData("AAL123 SBY", "AAL123 SBY")]
    [InlineData("AAL123 SQ0421", "AAL123 SQ0421")]
    [InlineData("AAL123 SQ4321 ID", "AAL123 SQ4321 ID")]
    [InlineData("AAL123 SQ4321 SQALT", "AAL123 SQ4321 SQALT")]
    [InlineData("AAL123 SQNORM", "AAL123 SQNORM")]
    [InlineData("AAL123 SQSBY", "AAL123 SQSBY")]
    [InlineData("AAL123 SQVFR", "AAL123 SQVFR")]
    [InlineData("AAL123 STOPALTSQ", "AAL123 STOPALTSQ")]
    [InlineData("AAL123 XOZZZI@120", "AAL123 XOZZZI@120")]
    [InlineData(
        "AAL123 XOZZZI@120@250K A2992",
        "AAL123 XOZZZI@120@250K A2992")]
    [InlineData(
        "AAL123 X10NW.BURGL@330",
        "AAL123 X10NW.BURGL@330")]
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
    [InlineData("AAL123 S250++")]
    [InlineData("AAL123 MM49")]
    [InlineData("AAL123 MM100")]
    [InlineData("AAL123 MM76+-")]
    [InlineData("AAL123 EABC25")]
    [InlineData("AAL123 EILS")]
    [InlineData("AAL123 *")]
    [InlineData("AAL123 *33")]
    [InlineData("AAL123 *3233")]
    [InlineData("AAL123 *3700")]
    [InlineData("AAL123 ??")]
    [InlineData("AAL123 ..A")]
    [InlineData("AAL123 A992")]
    [InlineData("AAL123 DVXM")]
    [InlineData("AAL123 XOZZZI@120@259K")]
    [InlineData("AAL123 XOZZZI@120@250")]
    [InlineData("AAL123 X@120")]
    [InlineData("AAL123 X0NW.BURGL@330")]
    [InlineData("AAL123 X010NW.BURGL@330")]
    [InlineData("AAL123 X10NNW.BURGL@330")]
    [InlineData("AAL123 X10NW.B@330")]
    [InlineData("AAL123 CWS@A")]
    [InlineData("AAL123 PS@HOMER")]
    [InlineData("AAL123 PD0")]
    [InlineData("AAL123 EXP999")]
    [InlineData("AAL123 RL5")]
    [InlineData("AAL123 RR601")]
    [InlineData("AAL123 SQ123")]
    [InlineData("AAL123 SQ1280")]
    [InlineData("AAL123 SQ8888")]
    [InlineData("AAL123 IDENT")]
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

    [Theory]
    [InlineData("AAL123 CWS@HOMER")]
    [InlineData("AAL123 CWS@HOMER DV")]
    [InlineData("AAL123 DV CWS@HOMER S250")]
    [InlineData("AAL123 DV CWS@HOMER ..OZZZI")]
    [InlineData("AAL123 DV CWS@HOMER CWS@EAGUL")]
    public void Validate_RejectsUnsafePublishedSpeedOrdering(string input)
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(input));
    }

    [Theory]
    [InlineData("AAL123 DV EXP")]
    [InlineData("AAL123 EXP DV")]
    [InlineData("AAL123 PD120 EXP")]
    [InlineData("AAL123 EXP120 PD100")]
    [InlineData("AAL123 EXP DM120")]
    [InlineData("AAL123 EXP120 XOZZZI@100")]
    [InlineData("AAL123 DM120 EXP EXP100")]
    public void Validate_RejectsUnsafeExpediteCombinations(string input)
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(input));
    }

    [Theory]
    [InlineData("AAL123 INTC")]
    [InlineData("AAL123 INTC FH220")]
    [InlineData("AAL123 S170 CA")]
    [InlineData("AAL123 MM76 CA")]
    [InlineData("AAL123 CA DV")]
    [InlineData("AAL123 CA EILS25L")]
    [InlineData("AAL123 CA CA")]
    [InlineData("AAL123 EILS25L ERNAV31")]
    [InlineData("AAL123 EXP CA")]
    [InlineData("AAL123 CA EXP")]
    [InlineData("AAL123 S-")]
    [InlineData("AAL123 S- CA")]
    [InlineData("AAL123 CA S- S-")]
    public void Validate_RejectsUnsafeApproachOrdering(string input)
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(input));
    }

    [Theory]
    [InlineData("AAL123 *3237 DM120")]
    [InlineData("AAL123 *3237 *3527")]
    [InlineData("AAL123 *0 *3237")]
    [InlineData("AAL123 ? R")]
    [InlineData("AAL123 R SBY")]
    public void Validate_RejectsUnsafeCommunicationOrdering(string input)
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(input));
    }

    [Theory]
    [InlineData("AAL123 SQ4321 SQ1200")]
    [InlineData("AAL123 SQ4321 SQVFR")]
    [InlineData("AAL123 SQNORM SQSBY")]
    [InlineData("AAL123 SQALT STOPALTSQ")]
    [InlineData("AAL123 ID ID")]
    [InlineData("AAL123 SQSBY ID")]
    public void Validate_RejectsConflictingTransponderInstructions(
        string input)
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(input));
    }

    [Theory]
    [InlineData("AAL123 X10NW.BURGL@330 X5S.OZZZI@120")]
    [InlineData("AAL123 X10NW.BURGL@330 ..OZZZI")]
    public void Validate_RejectsUnsafeCrossDistanceOrdering(string input)
    {
        Assert.Throws<ArgumentException>(
            () => EatsTransmissionValidator.Validate(input));
    }
}
