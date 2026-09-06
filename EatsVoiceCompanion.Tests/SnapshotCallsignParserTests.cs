using EatsVoiceCompanion.Core.Data;

namespace EatsVoiceCompanion.Tests;

public sealed class SnapshotCallsignParserTests

{
    [Fact]
    public void Parse_ExtractsAircraftCallsigns()
    {
        string[] lines =
        {
            "SNAPSHOT at 16:45:01 paint 3",
            "Range 55 display H 2554",
            "",
            "DAL123 N3056.67/W08406.43 314 1101 CRZ",
            "Live 364 32000 MCPFL 320 ACT STL",
            "N418GJ N3735.54/W08340.48 760 4001 CRZ",
            "Live 351 21000 MCPFL 210 ACT GZG",
            "UPS1076 N3402.45/W08128.76 302 702 CLIMB",
            "Live 352 8898 MCPFL 220 ACT GRD"
        };

        IReadOnlyList<string> result =
            SnapshotCallsignParser.Parse(lines);

        Assert.Equal(
            new[] { "DAL123", "N418GJ", "UPS1076" },
            result);
    }

    [Fact]
    public void Parse_RemovesDuplicateCallsigns()
    {
        string[] lines =
        {
            "DAL123 N3056.67/W08406.43 314 1101 CRZ",
            "dal123 N3057.00/W08407.00 315 1101 CRZ"
        };

        IReadOnlyList<string> result =
            SnapshotCallsignParser.Parse(lines);

        Assert.Single(result);
        Assert.Equal("DAL123", result[0]);
    }

    [Fact]
    public void Parse_IgnoresHeadersAndLiveLines()
    {
        string[] lines =
        {
            "SNAPSHOT at 16:45:01 paint 29",
            "Range 55 display H 2554",
            "Live 364 32000 MCPFL 320",
            "This is not an aircraft line"
        };

        IReadOnlyList<string> result =
            SnapshotCallsignParser.Parse(lines);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SupportsSouthernAndEasternCoordinates()
    {
        string[] lines =
        {
            "QFA12 S3351.25/E15110.50 123 456 CRZ"
        };

        IReadOnlyList<string> result =
            SnapshotCallsignParser.Parse(lines);

        Assert.Equal(
            new[] { "QFA12" },
            result);
    }
}