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
    public void ProceedDirect_NormalizesFixName()
    {
        string result = EatsCommandFormatter.ProceedDirect("lozit");

        Assert.Equal("..LOZIT", result);
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