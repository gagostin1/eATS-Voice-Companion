using EatsVoiceCompanion.Core.Data;

namespace EatsVoiceCompanion.Tests;

public sealed class AirlineAliasFileParserTests
{
    [Fact]
    public void Parse_ReturnsAliasesFromValidEntries()
    {
        string[] lines =
        {
            "; AIRLINES.TXT",
            "",
            "AAL AMERICAN",
            "ACA AIR_CANADA",
            "AB  AIR_BRAVO",
            "AER ACE_AIR"
        };

        IReadOnlyDictionary<string, string> result =
            AirlineAliasFileParser.Parse(lines);

        Assert.Equal("AAL", result["AMERICAN"]);
        Assert.Equal("ACA", result["AIR CANADA"]);
        Assert.Equal("AB", result["AIR BRAVO"]);
        Assert.Equal("AER", result["ACE AIR"]);
    }

    [Fact]
    public void Parse_IsCaseInsensitive()
    {
        string[] lines =
        {
            "aal american"
        };

        IReadOnlyDictionary<string, string> result =
            AirlineAliasFileParser.Parse(lines);

        Assert.Equal("AAL", result["American"]);
    }

    [Fact]
    public void Parse_SupportsInlineComments()
    {
        string[] lines =
        {
            "UAL UNITED ; example comment"
        };

        IReadOnlyDictionary<string, string> result =
            AirlineAliasFileParser.Parse(lines);

        Assert.Equal("UAL", result["UNITED"]);
    }

    [Fact]
    public void Parse_AcceptsTrailingFlightNumberRanges()
    {
        string[] lines =
        {
            "JIA BLUE_STREAK 5001 5699",
            "PDT PIEDMONT 5901 6199"
        };

        IReadOnlyDictionary<string, string> result =
            AirlineAliasFileParser.Parse(lines);

        Assert.Equal("JIA", result["BLUE STREAK"]);
        Assert.Equal("PDT", result["PIEDMONT"]);
    }

    [Fact]
    public void Parse_IgnoresMalformedEntries()
    {
        string[] lines =
        {
            "THIS IS NOT VALID",
            "AAL",
            "TOOLONG INVALID",
            "###",
            "; comment"
        };

        IReadOnlyDictionary<string, string> result =
            AirlineAliasFileParser.Parse(lines);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_RemovesAmbiguousSpokenNames()
    {
        string[] lines =
        {
            "ABC EXAMPLE",
            "XYZ EXAMPLE"
        };

        IReadOnlyDictionary<string, string> result =
            AirlineAliasFileParser.Parse(lines);

        Assert.False(result.ContainsKey("EXAMPLE"));
    }

    [Fact]
    public void Parse_AllowsRepeatedIdenticalEntries()
    {
        string[] lines =
        {
            "AAL AMERICAN",
            "AAL AMERICAN"
        };

        IReadOnlyDictionary<string, string> result =
            AirlineAliasFileParser.Parse(lines);

        Assert.Single(result);
        Assert.Equal("AAL", result["AMERICAN"]);
    }
}
