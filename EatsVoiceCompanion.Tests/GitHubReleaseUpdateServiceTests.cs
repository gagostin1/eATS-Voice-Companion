using System.Net;
using System.Net.Http;
using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class GitHubReleaseUpdateServiceTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(23, false)]
    [InlineData(24, true)]
    [InlineData(25, true)]
    public void ShouldCheckAutomatically_EnforcesDailyThrottle(
        int? hoursAgo,
        bool expected)
    {
        DateTimeOffset now = new(
            2026,
            9,
            15,
            18,
            0,
            0,
            TimeSpan.Zero);
        DateTimeOffset? lastCheck = hoursAgo switch
        {
            null => null,
            -1 => now.AddHours(1),
            _ => now.AddHours(-hoursAgo.Value)
        };

        Assert.Equal(
            expected,
            GitHubReleaseUpdateService.ShouldCheckAutomatically(
                lastCheck,
                now));
    }

    [Fact]
    public async Task CheckAsync_ReportsNewerStableRelease()
    {
        RecordingHandler handler = new(_ => JsonResponse(
            """
            {
              "tag_name": "v0.3.1",
              "draft": false,
              "prerelease": false
            }
            """));
        using HttpClient httpClient = new(handler);
        using GitHubReleaseUpdateService service = new(
            httpClient,
            new Uri("https://api.example/releases/latest"));

        AppUpdateCheckResult result = await service.CheckAsync(
            new Version(0, 3, 0, 0));

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("v0.3.1", result.LatestTag);
        Assert.Equal(new Version(0, 3, 1, 0), result.LatestVersion);
        Assert.Equal(
            "https://github.com/gagostin1/eATS-Voice-Companion/" +
            "releases/tag/v0.3.1",
            result.ReleasePageUri.AbsoluteUri);
        Assert.Contains(
            handler.Request!.Headers.UserAgent,
            value => value.Product?.Name == "eATS-Voice-Companion");
    }

    [Theory]
    [InlineData("v0.3.0")]
    [InlineData("0.3.0.0")]
    [InlineData("V0.2.9")]
    public async Task CheckAsync_DoesNotReportSameOrOlderRelease(string tag)
    {
        using HttpClient httpClient = new(new RecordingHandler(_ =>
            JsonResponse($$"""
            {
              "tag_name": "{{tag}}",
              "draft": false,
              "prerelease": false
            }
            """)));
        using GitHubReleaseUpdateService service = new(
            httpClient,
            new Uri("https://api.example/releases/latest"));

        AppUpdateCheckResult result = await service.CheckAsync(
            new Version(0, 3, 0, 0));

        Assert.False(result.IsUpdateAvailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v0.4")]
    [InlineData("v0.4.0-beta.1")]
    public async Task CheckAsync_RejectsInvalidStableRelease(string tag)
    {
        using HttpClient httpClient = new(new RecordingHandler(_ =>
            JsonResponse($$"""
            {
              "tag_name": "{{tag}}",
              "draft": false,
              "prerelease": false
            }
            """)));
        using GitHubReleaseUpdateService service = new(
            httpClient,
            new Uri("https://api.example/releases/latest"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckAsync(new Version(0, 3, 0, 0)));
    }

    [Fact]
    public async Task CheckAsync_RejectsDraftOrPrereleaseResponse()
    {
        using HttpClient httpClient = new(new RecordingHandler(_ =>
            JsonResponse(
                """
                {
                  "tag_name": "v0.4.0",
                  "draft": false,
                  "prerelease": true
                }
                """)));
        using GitHubReleaseUpdateService service = new(
            httpClient,
            new Uri("https://api.example/releases/latest"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.CheckAsync(new Version(0, 3, 0, 0)));
    }

    [Fact]
    public async Task CheckAsync_ReportsHttpFailure()
    {
        using HttpClient httpClient = new(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using GitHubReleaseUpdateService service = new(
            httpClient,
            new Uri("https://api.example/releases/latest"));

        HttpRequestException exception = await Assert.ThrowsAsync<
            HttpRequestException>(() =>
            service.CheckAsync(new Version(0, 3, 0, 0)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(
        HttpStatusCode.OK)
    {
        Content = new StringContent(json)
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}
