using System.Net;
using System.Net.Http;
using System.Text.Json;
using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class CorrectionContributionClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.Accepted, "accepted")]
    [InlineData(HttpStatusCode.OK, "duplicate")]
    public async Task SendAsync_AcceptsConfirmedServerReceipt(
        HttpStatusCode responseStatus,
        string contributionStatus)
    {
        string receiptId = new('b', 64);
        RecordingHandler handler = new(_ => new HttpResponseMessage(responseStatus)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    receiptId,
                    status = contributionStatus
                }))
        });
        using HttpClient httpClient = new(handler);
        using CorrectionContributionClient client = new(
            httpClient,
            new Uri("https://contributions.example/"));
        CorrectionContributionOutboxItem item = CreateItem();

        CorrectionContributionSendResult result = await client.SendAsync(
            item,
            Guid.Parse("63bd5c25-0c57-4f5d-8235-dcbf5ee0e44c"));

        Assert.Equal(receiptId, result.ReceiptId);
        Assert.Equal(contributionStatus, result.Status);
        Assert.Equal(
            "63bd5c25-0c57-4f5d-8235-dcbf5ee0e44c",
            handler.Request!.Headers.GetValues("X-Installation-Id").Single());
        Assert.Equal(
            "application/json",
            handler.Request.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task SendAsync_RejectsUnconfirmedResponse()
    {
        using HttpClient httpClient = new(new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"receiptId\":\"bad\",\"status\":\"accepted\"}")
            }));
        using CorrectionContributionClient client = new(
            httpClient,
            new Uri("https://contributions.example/"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.SendAsync(CreateItem(), Guid.NewGuid()));
    }

    private static CorrectionContributionOutboxItem CreateItem()
    {
        CorrectionRegressionCase regressionCase = new(
            2,
            "0.3.0",
            DateTime.Parse(
                "2026-09-13T20:00:00Z",
                null,
                System.Globalization.DateTimeStyles.AdjustToUniversal),
            "DAL123",
            new Dictionary<string, string> { ["DELTA"] = "DAL" },
            "Delta 123 clear direct Aussie.",
            "DAL123 R",
            true,
            "Delta 123 cleared direct OZZZI.",
            "DAL123 ..OZZZI",
            "Atlanta Center",
            null,
            ["OZZZI"]);
        return new CorrectionContributionOutboxItem(
            "pending.json",
            regressionCase,
            "{\"SchemaVersion\":2}");
    }

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
