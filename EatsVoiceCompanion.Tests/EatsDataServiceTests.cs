using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class EatsDataServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"EatsVoiceCompanionTests-{Guid.NewGuid():N}");

    [Fact]
    public void AirlineService_LoadsConfiguredFile()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "Airlines.txt");
        File.WriteAllLines(path, ["AAL AMERICAN", "UAL UNITED"]);

        IReadOnlyDictionary<string, string> result =
            new EatsAirlineAliasService(path).Load();

        Assert.Equal("AAL", result["AMERICAN"]);
        Assert.Equal("UAL", result["UNITED"]);
    }

    [Fact]
    public void SnapshotService_ReadsConfiguredFileAndTimestamp()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "SnapshotAuto.txt");
        File.WriteAllLines(
            path,
            ["DAL123 N3056.67/W08406.43 314 1101 CRZ"]);

        DateTime timestamp = DateTime.UtcNow.AddMinutes(-1);
        File.SetLastWriteTimeUtc(path, timestamp);

        EatsSnapshotData result = new EatsSnapshotService(path).Load();

        Assert.Equal(["DAL123"], result.Callsigns);
        Assert.InRange(
            result.LastWriteTimeUtc,
            timestamp.AddSeconds(-1),
            timestamp.AddSeconds(1));
    }

    [Fact]
    public void RouteContextService_ResolvesActiveDescendViaStar()
    {
        Directory.CreateDirectory(_directory);
        string logPath = Path.Combine(_directory, "LogDetail.txt");
        string airwaysPath = Path.Combine(_directory, "Airways.txt");
        File.WriteAllLines(
            logPath,
            [
                "Generated IFR 406 JIA5588 KCAE CLT " +
                "KCAE..AGUVE..CRDET.BANKR5.CLT"
            ]);
        File.WriteAllLines(
            airwaysPath,
            ["BANKR5 FIX1 FIX2 *DV120 MORE KCLT"]);

        EatsRouteContextData result = new EatsRouteContextService(
            logPath,
            airwaysPath).Load(["JIA5588"]);

        Assert.Equal("BANKR5", result.ActiveStars["JIA5588"]);
        Assert.Contains("AGUVE", result.ActiveRouteFixes["JIA5588"]);
        Assert.Contains("CRDET", result.ActiveRouteFixes["JIA5588"]);
        Assert.Contains("FIX1", result.ActiveRouteFixes["JIA5588"]);
        Assert.Contains("FIX2", result.ActiveRouteFixes["JIA5588"]);
        Assert.Contains("MORE", result.ActiveRouteFixes["JIA5588"]);
    }

    [Fact]
    public void RouteContextService_ExpandsNonDescendViaArrivalForFixContext()
    {
        Directory.CreateDirectory(_directory);
        string logPath = Path.Combine(_directory, "LogDetail.txt");
        string airwaysPath = Path.Combine(_directory, "Airways.txt");
        File.WriteAllLines(
            logPath,
            [
                "Generated IFR 381 DAL1486 EWR KATL " +
                "EWR./.ODF055041..MHONY.OZZZI1.KATL"
            ]);
        File.WriteAllLines(
            airwaysPath,
            [
                "; expect alt, no DV",
                "OZZZI1 FLASK YEOLD SCHUL MHONY *240- WINNG " +
                "OZZZI *120 *S250 HAARY KATL27L KATL"
            ]);

        EatsRouteContextData result = new EatsRouteContextService(
            logPath,
            airwaysPath).Load(["DAL1486"]);

        Assert.Empty(result.ActiveStars);
        Assert.Contains("MHONY", result.ActiveRouteFixes["DAL1486"]);
        Assert.Contains("WINNG", result.ActiveRouteFixes["DAL1486"]);
        Assert.Contains("OZZZI", result.ActiveRouteFixes["DAL1486"]);
        Assert.Contains("HAARY", result.ActiveRouteFixes["DAL1486"]);
        Assert.DoesNotContain("OZZZI1", result.ActiveRouteFixes["DAL1486"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
