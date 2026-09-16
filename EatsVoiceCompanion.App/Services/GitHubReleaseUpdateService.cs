using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EatsVoiceCompanion.App.Services;

public sealed record AppUpdateCheckResult(
    string LatestTag,
    Version LatestVersion,
    Uri ReleasePageUri,
    bool IsUpdateAvailable);

public sealed class GitHubReleaseUpdateService : IDisposable
{
    public static readonly Uri LatestReleaseEndpoint = new(
        "https://api.github.com/repos/gagostin1/" +
        "eATS-Voice-Companion/releases/latest");

    private static readonly Uri ReleasesBaseUri = new(
        "https://github.com/gagostin1/" +
        "eATS-Voice-Companion/releases/tag/");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly Uri _endpoint;

    public GitHubReleaseUpdateService(
        HttpClient? httpClient = null,
        Uri? endpoint = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        _endpoint = endpoint ?? LatestReleaseEndpoint;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public static bool ShouldCheckAutomatically(
        DateTimeOffset? lastCheckUtc,
        DateTimeOffset nowUtc)
    {
        return lastCheckUtc is not DateTimeOffset lastCheck ||
            lastCheck > nowUtc ||
            nowUtc - lastCheck >= TimeSpan.FromDays(1);
    }

    public async Task<AppUpdateCheckResult> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        using HttpRequestMessage request = new(HttpMethod.Get, _endpoint);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(
            "eATS-Voice-Companion",
            NormalizeVersion(currentVersion).ToString(3)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(
            "application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GitHub returned HTTP {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        string json = await response.Content.ReadAsStringAsync(
            cancellationToken);
        LatestReleaseResponse? release;

        try
        {
            release = JsonSerializer.Deserialize<LatestReleaseResponse>(
                json,
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "GitHub returned invalid release information.",
                exception);
        }

        if (release is null ||
            release.Draft ||
            release.Prerelease ||
            !TryParseReleaseVersion(release.TagName, out Version? latest))
        {
            throw new InvalidDataException(
                "GitHub returned invalid stable-release information.");
        }

        string tag = release.TagName.Trim();
        Version latestVersion = latest!;
        Uri releasePage = new(
            ReleasesBaseUri,
            Uri.EscapeDataString(tag));

        return new AppUpdateCheckResult(
            tag,
            latestVersion,
            releasePage,
            NormalizeVersion(latestVersion) > NormalizeVersion(currentVersion));
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static bool TryParseReleaseVersion(
        string? tag,
        out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        string value = tag.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        if (value.Contains('-', StringComparison.Ordinal) ||
            !Version.TryParse(value, out Version? parsed) ||
            parsed.Major < 0 ||
            parsed.Minor < 0 ||
            parsed.Build < 0)
        {
            return false;
        }

        version = NormalizeVersion(parsed);
        return true;
    }

    private static Version NormalizeVersion(Version version) => new(
        version.Major,
        version.Minor,
        Math.Max(version.Build, 0),
        Math.Max(version.Revision, 0));

    private sealed record LatestReleaseResponse(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease);
}
