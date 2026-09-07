using EatsVoiceCompanion.Core.Data;

namespace EatsVoiceCompanion.Tests;

public sealed class EatsRouteParserTests
{
    [Fact]
    public void GeneratedRouteParser_KeepsLatestRouteForCallsign()
    {
        IReadOnlyDictionary<string, GeneratedAircraftRoute> routes =
            GeneratedRouteLogParser.Parse(
            [
                "Generated IFR 1 JIA5588 KCAE CLT KCAE..OLD1.CLT",
                "ignored line",
                "Generated IFR 406 JIA5588 KCAE CLT " +
                "KCAE..AGUVE..CRDET.BANKR5.CLT"
            ]);

        Assert.Single(routes);
        Assert.Equal(
            "KCAE..AGUVE..CRDET.BANKR5.CLT",
            routes["JIA5588"].Route);
    }

    [Fact]
    public void ProcedureParser_ReturnsOnlyDescendViaProcedures()
    {
        IReadOnlyList<DescendViaProcedure> procedures =
            DescendViaProcedureParser.Parse(
            [
                "; comment",
                "BANKR5 *BYZAB17 TMALE *350- BRRTO *210B280 " +
                "UNCIR *DV80 *S210 KCLT",
                "BANKR5 *BYZAB17 TMALE *350- BRRTO *210B280 " +
                "UNCIR *DV80 *S210 KCLT",
                "OTHER1 FIX1 FIX2 KCLT"
            ]);

        DescendViaProcedure procedure = Assert.Single(procedures);
        Assert.Equal("BANKR5", procedure.Name);
        Assert.Equal("KCLT", procedure.Destination);
    }

    [Fact]
    public void Resolver_JoinsActiveCallsignRouteAndDestinationProcedure()
    {
        IReadOnlyDictionary<string, GeneratedAircraftRoute> routes =
            GeneratedRouteLogParser.Parse(
            [
                "Generated IFR 406 JIA5588 KCAE CLT " +
                "KCAE..AGUVE..CRDET.BANKR5.CLT"
            ]);
        DescendViaProcedure[] procedures =
            [new("BANKR5", "KCLT")];

        IReadOnlyDictionary<string, string> result =
            ActiveStarResolver.Resolve(
                ["JIA5588", "AAL123"],
                routes,
                procedures);

        Assert.Equal("BANKR5", result["JIA5588"]);
        Assert.False(result.ContainsKey("AAL123"));
    }

    [Fact]
    public void Resolver_OmitsAmbiguousRoute()
    {
        IReadOnlyDictionary<string, GeneratedAircraftRoute> routes =
            GeneratedRouteLogParser.Parse(
            [
                "Generated IFR 1 JIA5588 KCAE CLT " +
                "KCAE..BANKR5.OTHER1.CLT"
            ]);
        DescendViaProcedure[] procedures =
            [new("BANKR5", "KCLT"), new("OTHER1", "KCLT")];

        IReadOnlyDictionary<string, string> result =
            ActiveStarResolver.Resolve(["JIA5588"], routes, procedures);

        Assert.Empty(result);
    }
}
