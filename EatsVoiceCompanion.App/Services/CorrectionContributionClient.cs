using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace EatsVoiceCompanion.App.Services;

public sealed record CorrectionContributionSendResult(
    string ReceiptId,
    string Status);

public sealed class CorrectionContributionClient : IDisposable
{
    public static readonly Uri ProductionEndpoint = new(
        "https://eats-voice-contributions.gustavoagostinho4.workers.dev/");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public CorrectionContributionClient(
        HttpClient? httpClient = null,
        Uri? endpoint = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        _httpClient.BaseAddress = endpoint ?? ProductionEndpoint;
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<CorrectionContributionSendResult> SendAsync(
        CorrectionContributionOutboxItem item,
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            "v1/contributions");
        request.Headers.Add("X-Installation-Id", installationId.ToString("D"));
        request.Content = new StringContent(
            item.Json,
            Encoding.UTF8,
            "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        string responseJson = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"The contribution service returned HTTP " +
                $"{(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }

        ContributionResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<ContributionResponse>(
                responseJson,
                SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The contribution service returned an invalid receipt.",
                exception);
        }

        if (result is null ||
            string.IsNullOrWhiteSpace(result.ReceiptId) ||
            result.ReceiptId.Length != 64 ||
            !result.ReceiptId.All(Uri.IsHexDigit) ||
            result.Status is not ("accepted" or "duplicate"))
        {
            throw new InvalidDataException(
                "The contribution service returned an invalid receipt.");
        }

        return new CorrectionContributionSendResult(
            result.ReceiptId.ToLowerInvariant(),
            result.Status);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed record ContributionResponse(
        string ReceiptId,
        string Status);
}
