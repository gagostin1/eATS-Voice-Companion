using EatsVoiceCompanion.Core.Commands;

namespace EatsVoiceCompanion.Tests;

public sealed class EatsCommandFormatterTests
{
    [Theory]
    [InlineData(5, "FH005")]
    [InlineData(90, "FH090")]
    [InlineData(270, "FH270")]
    [InlineData(360, "FH360")]
    public void FlyHeading_FormatsThreeDigits(
        int heading,
        string expected)
    {
        string result = EatsCommandFormatter.FlyHeading(heading);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TurnDirections_ProduceCorrectCommands()
    {
        Assert.Equal(
            "TLH050",
            EatsCommandFormatter.TurnLeftHeading(50));

        Assert.Equal(
            "TRH220",
            EatsCommandFormatter.TurnRightHeading(220));
    }
    [Fact]
    public void Roger_ReturnsRogerCommand()
    {
        string result =
            EatsCommandFormatter.Roger();

        Assert.Equal("R", result);
    }

    [Theory]
    [InlineData(3000, "CM30")]
    [InlineData(8000, "CM80")]
    [InlineData(35000, "CM350")]
    public void ClimbAndMaintain_ConvertsFeetToHundreds(
        int altitudeFeet,
        string expected)
    {
        string result =
            EatsCommandFormatter.ClimbAndMaintain(altitudeFeet);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1500, "DM15")]
    [InlineData(11000, "DM110")]
    [InlineData(24000, "DM240")]
    public void DescendAndMaintain_ConvertsFeetToHundreds(
        int altitudeFeet,
        string expected)
    {
        string result =
            EatsCommandFormatter.DescendAndMaintain(altitudeFeet);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildTransmission_CombinesMultipleInstructions()
    {
        string result = EatsCommandFormatter.BuildTransmission(
            "dal123",
            EatsCommandFormatter.TurnLeftHeading(50),
            EatsCommandFormatter.ClimbAndMaintain(35000),
            EatsCommandFormatter.MaintainSpeed(280));

        Assert.Equal("DAL123 TLH050 CM350 S280", result);
    }

    [Fact]
    public void NewAltitudeCommands_UseDocumentedEatsSyntax()
    {
        Assert.Equal("DV", EatsCommandFormatter.DescendVia());
        Assert.Equal(
            "DVXM120",
            EatsCommandFormatter.DescendViaExceptMaintain(12000));
        Assert.Equal(
            "XOZZZI@120",
            EatsCommandFormatter.CrossAtAltitude("ozzzi", 12000));
        Assert.Equal(
            "XOZZZI@120@250K",
            EatsCommandFormatter.CrossAtAltitudeAndSpeed(
                "ozzzi",
                12000,
                250));
        Assert.Equal("A2992", EatsCommandFormatter.Altimeter(2992));
        Assert.Equal(
            "CWS@HOMER",
            EatsCommandFormatter.ComplyWithPublishedSpeeds("homer"));
        Assert.Equal(
            "PD120",
            EatsCommandFormatter.DescendAtPilotsDiscretion(12000));
        Assert.Equal("EXP", EatsCommandFormatter.Expedite());
        Assert.Equal(
            "EXP280",
            EatsCommandFormatter.ExpediteThroughAltitude(28000));
        Assert.Equal(
            "RL240",
            EatsCommandFormatter.ReportLeavingAltitude(24000));
        Assert.Equal(
            "RR120",
            EatsCommandFormatter.ReportReachingAltitude(12000));
        Assert.Equal("SA", EatsCommandFormatter.SayAltitude());
    }

    [Fact]
    public void ProceedDirect_NormalizesFixName()
    {
        string result = EatsCommandFormatter.ProceedDirect("lozit");

        Assert.Equal("..LOZIT", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ProceedDirect_RejectsMissingFix(string? fix)
    {
        Assert.Throws<ArgumentException>(
            () => EatsCommandFormatter.ProceedDirect(fix!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(361)]
    public void HeadingOutsideRange_IsRejected(int heading)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EatsCommandFormatter.FlyHeading(heading));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(351)]
    public void SpeedOutsideRange_IsRejected(int speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EatsCommandFormatter.MaintainSpeed(speed));
    }

    [Fact]
    public void SpeedOutsideFiveKnotIncrement_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => EatsCommandFormatter.MaintainSpeed(259));
    }

    [Fact]
    public void SpeedAndMachCommands_UseDocumentedEatsSyntax()
    {
        Assert.Equal("S250+", EatsCommandFormatter.MaintainSpeedOrGreater(250));
        Assert.Equal("S250-", EatsCommandFormatter.MaintainSpeedOrLess(250));
        Assert.Equal("MM76", EatsCommandFormatter.MaintainMach(76));
        Assert.Equal("MM76+", EatsCommandFormatter.MaintainMachOrGreater(76));
        Assert.Equal("MM76-", EatsCommandFormatter.MaintainMachOrLess(76));
        Assert.Equal("RNS", EatsCommandFormatter.ResumeNormalSpeed());
        Assert.Equal("SI", EatsCommandFormatter.SayIndicatedSpeed());
        Assert.Equal("SM", EatsCommandFormatter.SayMach());
        Assert.Equal("SNS", EatsCommandFormatter.SayNormalSpeed());
    }

    [Theory]
    [InlineData(49)]
    [InlineData(100)]
    public void MachOutsideRange_IsRejected(int mach)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EatsCommandFormatter.MaintainMach(mach));
    }

    [Fact]
    public void ApproachCommands_UseDocumentedEatsSyntax()
    {
        Assert.Equal("PH", EatsCommandFormatter.FlyPresentHeading());
        Assert.Equal(
            "EILS25L",
            EatsCommandFormatter.ExpectApproach(
                "ILS runway two five left approach"));
        Assert.Equal(
            "INTC",
            EatsCommandFormatter.InterceptFinalApproachCourse());
        Assert.Equal("CA", EatsCommandFormatter.ClearedApproach());
        Assert.Equal("SAR", EatsCommandFormatter.SayApproachRequest());
        Assert.Equal(
            "S-",
            EatsCommandFormatter.ReduceToFinalApproachSpeed());
    }

    [Fact]
    public void PartialHundredsAltitude_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => EatsCommandFormatter.ClimbAndMaintain(35550));
    }

    [Fact]
    public void MissingInstruction_IsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => EatsCommandFormatter.BuildTransmission("DAL123"));
    }
}
